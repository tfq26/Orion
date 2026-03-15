using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public class HealthMonitorWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<HealthMonitorWorker> _logger;

        public HealthMonitorWorker(IServiceProvider serviceProvider, ILogger<HealthMonitorWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ANTI-GRAVITY] Health Monitor Engine Started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IMetadataService>();
                    var scaleService = scope.ServiceProvider.GetRequiredService<IScaleService>();

                    var peers = await db.GetPeersAsync();
                    var threshold = DateTime.UtcNow.AddSeconds(-45);

                    foreach (var peer in peers.Where(p => p.Status == "Online" && p.LastSeen < threshold))
                    {
                        _logger.LogWarning($"[ANTI-GRAVITY] Peer {peer.Name} (IP: {peer.IpAddress}) missed heartbeats. Marking Offline.");
                        
                        await db.UpdatePeerStatusAsync(peer.Id, "Offline");

                        // Trigger Evacuation
                        _logger.LogInformation($"[ANTI-GRAVITY] Evacuating workloads from {peer.Name}...");
                        var allInstances = await db.GetActiveInstancesAsync();
                        var instancesOnLostNode = allInstances.Where(i => i.ContainerName.Contains(peer.Name.ToLower())).ToList();

                        foreach (var instance in instancesOnLostNode)
                        {
                            _logger.LogInformation($"[ANTI-GRAVITY] Rescheduling instance {instance.Id} of App {instance.AppId}");
                            
                            // 1. Mark the old instance as lost/deleted in DB
                            await db.DeleteInstanceAsync(instance.Id);

                            // 2. Trigger a scale re-sync to find a new home
                            // This will essentially "re-deploy" the missing replica
                            await scaleService.ScaleAsync(instance.AppId, 0); // Hack to trigger refresh? 
                            // Better: Trigger a "Reconcile" method on scale service.
                            // For now, we'll just call ScaleAsync with the current desired count.
                            var apps = await db.GetAppsAsync();
                            var app = apps.FirstOrDefault(a => a.Id == instance.AppId);
                            if (app != null)
                            {
                                // Re-fetch all instances to get the current actual count after deletion
                                var currentInstances = await db.GetActiveInstancesAsync();
                                var desiredCount = currentInstances.Count(i => i.AppId == app.Id) + 1;
                                await scaleService.ScaleAsync(app.Id, desiredCount);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in HealthMonitorWorker");
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }
        }
    }
}
