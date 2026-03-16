using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;
using Orion.Core.Grpc;

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
        private readonly ISchedulerService _scheduler;
        private readonly INodeServiceClient _nodeClient;
        private readonly ILogger<ScaleService> _logger;

        public ScaleService(
            IMetadataService db, 
            IContainerService containerService, 
            ISecretService secretService,
            ISchedulerService scheduler,
            INodeServiceClient nodeClient,
            ILogger<ScaleService> logger)
        {
            _db = db;
            _containerService = containerService;
            _secretService = secretService;
            _scheduler = scheduler;
            _nodeClient = nodeClient;
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
                    // 1. Ask Scheduler where to put this
                    var targetNode = await _scheduler.ScheduleAsync(app);
                    var instanceName = $"{app.Name.ToLower()}-{Guid.NewGuid().ToString()[..8]}-{targetNode.Name.ToLower()}";
                    var imageTag = latestDeployment.ImageTag ?? $"orion/{app.Name.ToLower()}:latest";

                    int port = 0;
                    int pid = 0;

                    if (targetNode.IpAddress == "100.64.0.1") // Local (Master)
                    {
                        var result = await _containerService.StartContainerAsync(
                            imageTag, 
                            instanceName, 
                            secrets, 
                            app.RequiredCpuCores, 
                            app.RequiredMemoryMb);
                        port = result.Port;
                        pid = result.ProcessId ?? 0;
                    }
                    else // Remote Worker
                    {
                        var workerUrl = $"http://{targetNode.IpAddress}:5031";
                        var request = new WorkloadRequest
                        {
                            AppId = appId.ToString(),
                            DeploymentId = latestDeployment.Id.ToString(),
                            ImageTag = imageTag,
                            ContainerName = instanceName,
                            CpuCores = app.RequiredCpuCores ?? 0,
                            MemoryMb = app.RequiredMemoryMb ?? 0
                        };
                        foreach (var s in secrets) request.EnvVars.Add(s.Key, s.Value);

                        var response = await _nodeClient.StartWorkloadAsync(workerUrl, request);
                        if (!response.Success)
                        {
                            _logger.LogError($"Failed to start remote workload on {targetNode.Name}: {response.Message}");
                            continue;
                        }
                        port = response.Port;
                        pid = response.ProcessId;
                    }
                    
                    var instance = new Instance
                    {
                        AppId = appId,
                        DeploymentId = latestDeployment.Id,
                        ContainerName = instanceName,
                        Port = port,
                        ProcessId = pid,
                        AssignedCpuCores = app.RequiredCpuCores,
                        AssignedMemoryMb = app.RequiredMemoryMb,
                        Status = "Running",
                        OwnerId = app.OwnerId
                    };
                    await _db.CreateInstanceAsync(instance);
                    _logger.LogInformation($"Scaled up: Added instance {instance.Id} on node {targetNode.Name} (Port: {port})");
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
