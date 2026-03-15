using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public class ResourceMonitorService : BackgroundService
    {
        private readonly ILogger<ResourceMonitorService> _logger;
        private readonly IMetadataService _db;
        private readonly PerformanceCounter _cpuCounter;

        public ResourceMonitorService(ILogger<ResourceMonitorService> logger, IMetadataService db)
        {
            _logger = logger;
            _db = db;
            
            try 
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            }
            catch 
            {
                _logger.LogWarning("Performance counters not available on this platform.");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var instances = await _db.GetActiveInstancesAsync();
                int totalAssignedCores = 0;
                int totalAssignedMem = 0;

                foreach (var inst in instances)
                {
                    totalAssignedCores += inst.AssignedCpuCores ?? 0;
                    totalAssignedMem += inst.AssignedMemoryMb ?? 0;
                }

                float systemCpu = _cpuCounter?.NextValue() ?? 0;
                
                _logger.LogInformation($"[RESOURCES] System Load: {systemCpu:F1}% | Cluster Reservation: {totalAssignedCores} Cores, {totalAssignedMem} MB RAM");

                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
