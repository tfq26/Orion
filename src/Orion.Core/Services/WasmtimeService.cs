using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Wasmtime;

namespace Orion.Core.Services
{
    public class WasmtimeService : IContainerService
    {
        private readonly ILogger<WasmtimeService> _logger;
        private readonly ILogService _logService;
        private readonly IStorageService _storage;
        private readonly Engine _engine;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _runningModules = new();

        public WasmtimeService(ILogger<WasmtimeService> logger, ILogService logService, IStorageService storage)
        {
            _logger = logger;
            _logService = logService;
            _storage = storage;
            _engine = new Engine();
        }

        public async Task<InstanceResult> StartContainerAsync(string imageTag, string containerName, IDictionary<string, string>? envVars = null)
        {
            string wasmPath = imageTag;
            byte[]? wasmBytes = null;
            
            if (!File.Exists(wasmPath))
            {
                wasmPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "wasm-store", $"{imageTag.Replace("/", "_")}.wasm");
                
                if (!File.Exists(wasmPath))
                {
                    // Phase 11/Registry: Try pulling from distributed storage (SeaweedFS)
                    var canonicalName = imageTag.Contains(":") ? imageTag.Split(":")[0] : imageTag;
                    var hash = ComputeHash(canonicalName);
                    var blobId = $"cache/blobs/{hash}.wasm";
                    _logger.LogInformation($"[WASM] Local file missing. Checking storage for {imageTag} (Hash: {hash}) at {blobId}");
                    
                    if (await _storage.ExistsAsync(blobId))
                    {
                        _logger.LogInformation($"[STORAGE] Cache Hit! Pulling module {imageTag} ({hash}) from distributed storage.");
                        wasmBytes = await _storage.GetBlobAsync(blobId);
                    }
                    else
                    {
                        _logger.LogWarning($"WASM file not found: {imageTag}. Falling back to simulation.");
                    }
                }
            }

            int hostPort = GetAvailablePort();
            _logger.LogInformation($"[WASM] Starting module {containerName} on port {hostPort}");

            var cts = new CancellationTokenSource();
            _runningModules[containerName] = cts;

            _ = Task.Run(async () =>
            {
                try
                {
                    if (wasmBytes == null && !File.Exists(wasmPath))
                    {
                        await RunSimulatedWasmServer(containerName, hostPort, envVars, cts.Token);
                        return;
                    }

                    using var linker = new Linker(_engine);
                    linker.DefineWasi();
                    using var store = new Store(_engine);
                    var config = new WasiConfiguration().WithInheritedStandardOutput().WithInheritedStandardError();
                    if (envVars != null) foreach (var kvp in envVars) config.WithEnvironmentVariable(kvp.Key, kvp.Value);
                    store.SetWasiConfiguration(config);

                    using var module = wasmBytes != null 
                        ? Module.FromBytes(_engine, imageTag, wasmBytes)
                        : Module.FromFile(_engine, wasmPath);

                    var instance = linker.Instantiate(store, module);
                    var run = instance.GetFunction("_start");
                    run?.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"[WASM] Module {containerName} crashed.");
                }
                finally
                {
                    _runningModules.TryRemove(containerName, out _);
                }
            }, cts.Token);

            return new InstanceResult { Port = hostPort, ProcessId = Environment.ProcessId };
        }

        public Task<bool> StopContainerAsync(string containerName)
        {
            if (_runningModules.TryRemove(containerName, out var cts))
            {
                cts.Cancel();
                return Task.FromResult(true);
            }
            return Task.FromResult(false);
        }

        public Task<bool> IsContainerRunningAsync(string containerName)
        {
            return Task.FromResult(_runningModules.ContainsKey(containerName));
        }

        private async Task RunSimulatedWasmServer(string name, int port, IDictionary<string, string>? envVars, CancellationToken token)
        {
            try
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://*:{port}/");
                listener.Start();

                while (!token.IsCancellationRequested)
                {
                    var context = await listener.GetContextAsync();
                    var response = context.Response;

                    string responseString = $@"
                    <html>
                        <head>
                            <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;700;800&display=swap' rel='stylesheet'>
                            <style>
                                body {{ font-family: 'Inter', sans-serif; background: #0a0a0c; color: #f1f5f9; display: flex; align-items: center; justify-content: center; height: 100vh; margin: 0; overflow: hidden; }}
                                body::before {{ content: ''; position: fixed; top: -50%; left: -50%; width: 200%; height: 200%; background: radial-gradient(circle at 50% 0%, rgba(0, 230, 243, 0.05) 0%, transparent 40%); z-index: -1; pointer-events: none; }}
                                .card {{ background: rgba(255,255,255,0.02); border: 1px solid rgba(255,255,255,0.05); padding: 4rem; border-radius: 2rem; width: 600px; box-shadow: 0 40px 100px -20px rgba(0,0,0,0.8); backdrop-filter: blur(20px); animation: fadeInUp 0.8s ease forwards; }}
                                .logo {{ width: 48px; height: 48px; background: linear-gradient(to bottom right, #00e6f3, #b911fe); border-radius: 1rem; margin-bottom: 2.5rem; box-shadow: 0 0 30px rgba(0,230,243,0.3); }}
                                h1 {{ font-size: 2.5rem; font-weight: 800; letter-spacing: -0.05em; margin: 0 0 0.75rem 0; color: #fff; }}
                                .status {{ display: inline-flex; align-items: center; padding: 0.35rem 1rem; background: rgba(0,247,167,0.05); color: #00f7a7; border: 1px solid rgba(0,247,167,0.2); border-radius: 9999px; font-size: 0.7rem; font-weight: 800; text-transform: uppercase; letter-spacing: 0.1em; margin-bottom: 2.5rem; }}
                                .detail {{ color: #94a3b8; font-size: 0.9rem; margin-bottom: 1.25rem; display: flex; justify-content: space-between; border-bottom: 1px solid rgba(255,255,255,0.03); padding-bottom: 0.75rem; }}
                                .detail b {{ color: #fff; font-weight: 600; }}
                                .env-tag {{ display: inline-block; background: rgba(0,230,243,0.05); padding: 0.4rem 0.75rem; border-radius: 0.5rem; font-family: monospace; font-size: 0.7rem; margin-right: 0.5rem; color: #00e6f3; border: 1px solid rgba(0,230,243,0.1); }}
                                @keyframes fadeInUp {{ from {{ opacity: 0; transform: translateY(30px); }} to {{ opacity: 1; transform: translateY(0); }} }}
                            </style>
                        </head>
                        <body>
                            <div class='card'>
                                <div class='logo'></div>
                                <div class='status'><span style='width: 6px; height: 6px; background: #00f7a7; border-radius: 50%; margin-right: 8px; display: inline-block; box-shadow: 0 0 10px #00f7a7;'></span>Active Cluster Sandbox</div>
                                <h1>{name.Split('-')[1].ToUpper()}</h1>
                                <div style='margin-bottom: 3rem; margin-top: 2rem;'>
                                    <div class='detail'><span>Infrastructure</span> <b>Orion WASM Edge</b></div>
                                    <div class='detail'><span>Architecture</span> <b>ARM64-Native SVE</b></div>
                                    <div class='detail'><span>Instance ID</span> <b style='font-family: monospace; font-size: 0.8rem;'>{name}</b></div>
                                </div>
                                <div style='margin-top: 2rem;'>
                                    <div style='font-size: 0.65rem; color: #475569; text-transform: uppercase; font-weight: 900; letter-spacing: 0.15em; margin-bottom: 1.25rem;'>Sandbox Capabilities</div>
                                    <span class='env-tag'>fs:read</span>
                                    <span class='env-tag'>net:tcp</span>
                                    <span class='env-tag'>mesh:p2p</span>
                                </div>
                            </div>
                        </body>
                    </html>";

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(responseString);
                    response.ContentLength64 = buffer.Length;
                    using var output = response.OutputStream;
                    await output.WriteAsync(buffer, 0, buffer.Length);
                }
            }
            catch {}
        }

        private int GetAvailablePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private string ComputeHash(string input)
        {
            using var sha = System.Security.Cryptography.SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(input);
            var hashBytes = sha.ComputeHash(bytes);
            return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
        }
    }
}
