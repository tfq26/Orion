using System.Diagnostics;
using System.Net.NetworkInformation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface INodeTelemetryService
    {
        Task<NodeTelemetrySnapshot> GetSnapshotAsync();
    }

    public class NodeTelemetryService : BackgroundService, INodeTelemetryService
    {
        private readonly ILogger<NodeTelemetryService> _logger;
        private readonly INodeTelemetryHistoryStore _historyStore;
        private readonly string _nodeName = Environment.MachineName;
        private readonly string _architecture = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture.ToString();
        private TimeSpan _previousCpuTime = Process.GetCurrentProcess().TotalProcessorTime;
        private DateTime _previousCpuTimestamp = DateTime.UtcNow;
        private long _previousNetworkBytes;
        private DateTime _previousNetworkTimestamp = DateTime.UtcNow;

        public NodeTelemetryService(ILogger<NodeTelemetryService> logger, INodeTelemetryHistoryStore historyStore)
        {
            _logger = logger;
            _historyStore = historyStore;
            _previousNetworkBytes = ReadTotalNetworkBytes();
        }

        public async Task<NodeTelemetrySnapshot> GetSnapshotAsync()
        {
            var rawSamples = await _historyStore.GetRecentRawSamplesAsync(288);
            var hourlySamples = await _historyStore.GetHourlySamplesAsync(168);
            var dailySamples = await _historyStore.GetDailySamplesAsync(90);

            return new NodeTelemetrySnapshot
            {
                NodeName = _nodeName,
                Architecture = _architecture,
                Samples = rawSamples,
                HourlySamples = hourlySamples,
                DailySamples = dailySamples
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CaptureSampleAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[NODE] Failed to capture telemetry sample.");
                }

                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }

        private async Task CaptureSampleAsync()
        {
            var now = DateTime.UtcNow;
            using var process = Process.GetCurrentProcess();

            var currentCpuTime = process.TotalProcessorTime;
            var cpuWindowMs = Math.Max(1, (now - _previousCpuTimestamp).TotalMilliseconds);
            var cpuUsedMs = (currentCpuTime - _previousCpuTime).TotalMilliseconds;
            var cpuUsage = (cpuUsedMs / (Environment.ProcessorCount * cpuWindowMs)) * 100;
            cpuUsage = Math.Clamp(cpuUsage, 0, 100);
            _previousCpuTime = currentCpuTime;
            _previousCpuTimestamp = now;

            var memoryUsageGb = process.WorkingSet64 / 1024d / 1024d / 1024d;
            var totalMemoryGb = GetTotalPhysicalMemoryGb();
            var memoryUsagePercent = totalMemoryGb <= 0 ? 0 : Math.Clamp((memoryUsageGb / totalMemoryGb) * 100, 0, 100);

            var applicationStorageBytes = GetTrackedApplicationStorageBytes();
            var applicationStorageGb = applicationStorageBytes / 1024d / 1024d / 1024d;
            var storageSoftBudgetGb = 5d;
            var storageUsagePercent = Math.Clamp((applicationStorageGb / storageSoftBudgetGb) * 100, 0, 100);

            var currentNetworkBytes = ReadTotalNetworkBytes();
            var networkWindowSeconds = Math.Max(1, (now - _previousNetworkTimestamp).TotalSeconds);
            var networkBytesPerSecond = Math.Max(0, currentNetworkBytes - _previousNetworkBytes) / networkWindowSeconds;
            var networkTrafficMbps = (networkBytesPerSecond * 8) / 1_000_000d;
            _previousNetworkBytes = currentNetworkBytes;
            _previousNetworkTimestamp = now;

            var sample = new NodeTelemetrySample
            {
                Timestamp = now,
                CpuUsage = cpuUsage,
                MemoryUsagePercent = memoryUsagePercent,
                MemoryUsageGb = Math.Round(memoryUsageGb, 2),
                StorageUsagePercent = storageUsagePercent,
                StorageUsageGb = Math.Round(applicationStorageGb, 3),
                NetworkTrafficMbps = Math.Round(networkTrafficMbps, 2)
            };

            await _historyStore.AppendRawSampleAsync(_nodeName, _architecture, sample);
            await _historyStore.RollupAsync(now);
        }

        private static long ReadTotalNetworkBytes()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(iface => iface.OperationalStatus == OperationalStatus.Up && iface.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Sum(iface =>
                {
                    var stats = iface.GetIPv4Statistics();
                    return (long)stats.BytesReceived + stats.BytesSent;
                });
        }

        private static double GetTotalPhysicalMemoryGb()
        {
            try
            {
                if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux())
                {
                    var totalBytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                    if (totalBytes > 0)
                    {
                        return totalBytes / 1024d / 1024d / 1024d;
                    }
                }
            }
            catch
            {
            }

            return 0;
        }

        private static long GetTrackedApplicationStorageBytes()
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var baseDirectory = AppContext.BaseDirectory;
            var trackedPaths = new[]
            {
                Path.Combine(currentDirectory, "orion_telemetry.db"),
                Path.Combine(currentDirectory, "orion_telemetry.db.wal"),
                Path.Combine(currentDirectory, "orion_telemetry.db-journal"),
                Path.Combine(currentDirectory, "orion_metadata.db"),
                Path.Combine(currentDirectory, "orion_metadata.db.wal"),
                Path.Combine(currentDirectory, "orion_metadata.db-journal"),
                Path.Combine(currentDirectory, "keys"),
                Path.Combine(currentDirectory, "build_tmp"),
                Path.Combine(baseDirectory, ".orion_storage")
            };

            return trackedPaths.Distinct(StringComparer.OrdinalIgnoreCase).Sum(GetPathSizeBytes);
        }

        private static long GetPathSizeBytes(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    return new FileInfo(path).Length;
                }

                if (Directory.Exists(path))
                {
                    return Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories)
                        .Select(file =>
                        {
                            try
                            {
                                return new FileInfo(file).Length;
                            }
                            catch
                            {
                                return 0L;
                            }
                        })
                        .Sum();
                }
            }
            catch
            {
            }

            return 0;
        }
    }
}
