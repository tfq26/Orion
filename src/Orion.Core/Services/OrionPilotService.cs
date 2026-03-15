using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public class OrionPilotService : IPilotService
    {
        private readonly ILogger<OrionPilotService> _logger;
        private readonly IMetadataService _metadata;
        private readonly IMeshService _mesh;
        private readonly IStorageService _storage;
        private readonly IContainerService _container;

        public OrionPilotService(
            ILogger<OrionPilotService> logger,
            IMetadataService metadata,
            IMeshService mesh,
            IStorageService storage,
            IContainerService container)
        {
            _logger = logger;
            _metadata = metadata;
            _mesh = mesh;
            _storage = storage;
            _container = container;
        }

        public async Task<PilotReport> AnalyzeHealthAsync()
        {
            var report = new PilotReport { IsHealthy = true };
            _logger.LogInformation("[PILOT] Starting system health analysis...");

            // 1. Check Mesh State
            var peers = await _metadata.GetPeersAsync();
            var offlinePeers = peers.Where(p => p.Status != "Online").ToList();
            if (offlinePeers.Any())
            {
                report.IsHealthy = false;
                report.Issues.Add($"Mesh Degraded: {offlinePeers.Count} peers offline.");
            }

            // 2. Check Instance Consistency (Self-Healing Trigger)
            var activeInstances = await _metadata.GetActiveInstancesAsync();
            foreach (var instance in activeInstances)
            {
                // In a real system, we'd check if the process/container actually exists
                // For the simulation, we'll simulate a 5% chance of "ghost instances"
                if (new Random().Next(0, 100) < 5)
                {
                    _logger.LogWarning($"[PILOT] Found ghost instance {instance.Id} for app {instance.AppId}. Triggering recovery...");
                    await TriggerRecoveryAsync($"Ghost Instance: {instance.Id}", async () => {
                        await _metadata.DeleteInstanceAsync(instance.Id);
                        return true;
                    });
                    report.ActionsTaken.Add($"Pruned ghost instance {instance.Id}");
                }
            }

            // 3. Check Storage Connectivity
            try
            {
                bool storageOk = await _storage.ExistsAsync("health_check.txt");
                if (!storageOk)
                {
                    await _storage.SaveBlobAsync("health_check.txt", System.Text.Encoding.UTF8.GetBytes("OK"));
                }
            }
            catch (Exception ex)
            {
                report.IsHealthy = false;
                report.Issues.Add($"Storage Connectivity Failed: {ex.Message}");
            }

            report.PilotStatus = report.IsHealthy ? "Observing" : "Fixing";
            _logger.LogInformation($"[PILOT] Analysis complete. Healthy: {report.IsHealthy}. Actions: {report.ActionsTaken.Count}");
            
            return report;
        }

        public async Task<bool> TriggerRecoveryAsync(string reason, Func<Task<bool>> recoveryAction)
        {
            _logger.LogWarning($"[PILOT] RECOVERY TRIGGERED: {reason}");
            try
            {
                return await recoveryAction();
            }
            catch (Exception ex)
            {
                _logger.LogError($"[PILOT] Recovery failed: {ex.Message}");
                return false;
            }
        }
    }
}
