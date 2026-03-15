using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface ISchedulerService
    {
        Task<Peer> ScheduleAsync(App app);
    }

    public class SchedulerService : ISchedulerService
    {
        private readonly IMetadataService _db;
        private readonly ILogger<SchedulerService> _logger;

        public SchedulerService(IMetadataService db, ILogger<SchedulerService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<Peer> ScheduleAsync(App app)
        {
            _logger.LogInformation($"[SCHEDULER] Finding node for app {app.Name} (Needs: {app.RequiredCpuCores} cores, {app.RequiredMemoryMb} MB)");

            var peers = await _db.GetPeersAsync();
            var activePeers = peers.Where(p => p.Status == "Online").ToList();

            if (!activePeers.Any())
            {
                throw new Exception("No online nodes available in the cluster.");
            }

            // Simple Least-Loaded / Bin-Packing Logic
            // In a real cloud, we'd check actual telemetry (CPU usage, etc.)
            // Here, we'll pick the node with the fewest active instances for now.
            var allInstances = await _db.GetActiveInstancesAsync();
            
            var nodeLoads = activePeers.Select(p => new 
            {
                Peer = p,
                InstanceCount = allInstances.Count(i => i.ContainerName.Contains(p.Name.ToLower())) // Assuming naming convention
            })
            .OrderBy(n => n.InstanceCount)
            .ToList();

            var selected = nodeLoads.First();
            _logger.LogInformation($"[SCHEDULER] Selected node {selected.Peer.Name} (Active instances: {selected.InstanceCount})");
            
            return selected.Peer;
        }
    }
}
