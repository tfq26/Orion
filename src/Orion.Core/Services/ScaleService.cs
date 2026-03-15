using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface IScaleService
    {
        Task ScaleAsync(Guid appId, int targetReplicaCount);
    }

    public class ScaleService : IScaleService
    {
        private readonly IMetadataService _db;
        private readonly IContainerService _containerService;
        private readonly ISecretService _secretService;
        private readonly ILogger<ScaleService> _logger;

        public ScaleService(
            IMetadataService db, 
            IContainerService containerService, 
            ISecretService secretService,
            ILogger<ScaleService> logger)
        {
            _db = db;
            _containerService = containerService;
            _secretService = secretService;
            _logger = logger;
        }

        public async Task ScaleAsync(Guid appId, int targetReplicaCount)
        {
            var apps = await _db.GetAppsAsync();
            var app = apps.FirstOrDefault(a => a.Id == appId);
            if (app == null) throw new ArgumentException("App not found");

            var allInstances = await _db.GetActiveInstancesAsync();
            var currentInstances = allInstances.Where(i => i.AppId == appId).ToList();
            int currentCount = currentInstances.Count;

            _logger.LogInformation($"Scaling {app.Name} from {currentCount} to {targetReplicaCount} replicas");

            if (targetReplicaCount > currentCount)
            {
                // Scale up
                int toAdd = targetReplicaCount - currentCount;
                var latestDeployment = (await _db.GetDeploymentsAsync(appId))
                    .OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefault(d => d.Status == DeploymentStatus.Running);

                if (latestDeployment == null)
                {
                    _logger.LogWarning($"Cannot scale up {app.Name} because no running deployment was found.");
                    return;
                }

                var secrets = await _secretService.GetSecretsAsync(appId, decrypt: true);

                for (int i = 0; i < toAdd; i++)
                {
                    var imageTag = latestDeployment.ImageTag ?? $"orion/{app.Name.ToLower()}:latest";
                    var instanceName = $"orion-{app.Name.ToLower()}-replica-{Guid.NewGuid().ToString()[..8]}";
                    
                    var result = await _containerService.StartContainerAsync(imageTag, instanceName, secrets);
                    
                    var instance = new Instance
                    {
                        AppId = appId,
                        DeploymentId = latestDeployment.Id,
                        ContainerName = instanceName,
                        Port = result.Port,
                        ProcessId = result.ProcessId,
                        Status = "Running"
                    };
                    await _db.CreateInstanceAsync(instance);
                    _logger.LogInformation($"Scaled up: Added instance {instance.Id} on port {result.Port} (PID: {result.ProcessId})");
                }
            }
            else if (targetReplicaCount < currentCount && targetReplicaCount >= 0)
            {
                // Scale down
                int toRemove = currentCount - targetReplicaCount;
                var instancesToRemove = currentInstances.OrderBy(i => i.CreatedAt).Take(toRemove).ToList();

                foreach (var instance in instancesToRemove)
                {
                    _logger.LogInformation($"Scaling down: Stopping instance {instance.Id} ({instance.ContainerName})");
                    await _containerService.StopContainerAsync(instance.ContainerName);
                    await _db.DeleteInstanceAsync(instance.Id);
                }
            }
        }
    }
}
