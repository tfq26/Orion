using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface IMeshService
    {
        Task InitializeAsync();
        Task<IEnumerable<Peer>> GetPeersAsync();
        Task<string> GetAuthKeyAsync();
    }

    public class MeshService : IMeshService
    {
        private readonly ILogger<MeshService> _logger;
        private readonly IMetadataService _db;
        private readonly INodeServiceClient _nodeClient;
        private string _masterUrl;
        private Guid _nodeId = Guid.NewGuid();

        public MeshService(ILogger<MeshService> logger, IMetadataService db, INodeServiceClient nodeClient)
        {
            _logger = logger;
            _db = db;
            _nodeClient = nodeClient;
            _headscaleUrl = Environment.GetEnvironmentVariable("HEADSCALE_URL") ?? "http://localhost:8080";
            _apiKey = Environment.GetEnvironmentVariable("HEADSCALE_API_KEY") ?? "mock_key";
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("MeshService initializing...");
            var peers = await _db.GetPeersAsync();
            if (!System.Linq.Enumerable.Any(peers))
            {
                // Register local node if nothing exists (Seed/Master node)
                var localNode = new Peer
                {
                    Name = Environment.MachineName.ToLower(),
                    IpAddress = "100.64.0.1", // Master mesh IP
                    Status = "Online",
                    Tags = "master, control-plane",
                    LastSeen = DateTime.UtcNow
                };
                await _db.CreatePeerAsync(localNode);
                _logger.LogInformation($"[MESH] Initialized as Master Node: {localNode.Name}");
            }
        }

        public async Task<bool> JoinClusterAsync(string masterUrl, string authKey)
        {
            _logger.LogInformation($"[MESH] Attempting to join cluster via master: {masterUrl}");
            
            // 1. Join Mesh (Simulation: Connect to Headscale)
            _logger.LogInformation($"[MESH] Joining virtual private network via {authKey}");
            var meshIp = "100.64.0." + new Random().Next(2, 254);

            // 2. Register with Orion Master via gRPC
            var request = new JoinRequest
            {
                NodeId = _nodeId.ToString(),
                NodeName = Environment.MachineName.ToLower(),
                IpAddress = meshIp,
                CpuCores = Environment.ProcessorCount,
                MemoryMb = 8192 // Simulated RAM
            };

            var response = await _nodeClient.JoinAsync(masterUrl, request);
            if (response.Success)
            {
                _logger.LogInformation($"[MESH] Successfully joined cluster! Message: {response.Message}");
                _masterUrl = masterUrl;
                return true;
            }

            _logger.LogError($"[MESH] Failed to join cluster: {response.Message}");
            return false;
        }

        public async Task<IEnumerable<Peer>> GetPeersAsync()
        {
            return await _db.GetPeersAsync();
        }

        public Task<string> GetAuthKeyAsync()
        {
            return Task.FromResult("mkey:bootstrap-auth-token-simulated-" + Guid.NewGuid().ToString()[..8]);
        }
    }
}
