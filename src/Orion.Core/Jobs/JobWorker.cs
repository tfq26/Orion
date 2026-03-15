using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;
using Orion.Core.Services;

namespace Orion.Core.Jobs
{
    public class JobWorker : BackgroundService
    {
        private readonly IJobDispatcher _dispatcher;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<JobWorker> _logger;

        public JobWorker(IJobDispatcher dispatcher, IServiceProvider serviceProvider, ILogger<JobWorker> logger)
        {
            _dispatcher = dispatcher;
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("JobWorker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var job = await _dispatcher.DequeueAsync(stoppingToken);
                    _logger.LogInformation($"Processing job {job.Id}...");

                    using var scope = _serviceProvider.CreateScope();
                    var buildService = scope.ServiceProvider.GetRequiredService<IBuildService>();
                    var dbService = scope.ServiceProvider.GetRequiredService<IMetadataService>();

                    if (job is BuildJob buildJob)
                    {
                        // 1. Update Database (Building)
                        await dbService.UpdateDeploymentStatusAsync(buildJob.Id, DeploymentStatus.Building);
                        
                        // 2. Perform Build
                        var success = await buildService.BuildAsync(buildJob.AppId, buildJob.AppName, buildJob.RepoUrl, buildJob.Id, buildJob.OwnerId, buildJob.BuildCommand, buildJob.RunCommand, buildJob.BuildFolder);
                        
                        if (success)
                        {
                            // 3. Fetch and Decrypt Secrets
                            var secretService = scope.ServiceProvider.GetRequiredService<ISecretService>();
                            var secrets = await secretService.GetSecretsAsync(buildJob.AppId, decrypt: true, userId: buildJob.OwnerId);

                            // 4. Pull module from storage if needed
                            var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
                            var blobId = $"modules/{buildJob.AppName.ToLower()}/{buildJob.Id}.wasm";
                            if (await storage.ExistsAsync(blobId))
                            {
                                _logger.LogInformation($"[STORAGE] Pulled module {blobId} from SeaweedFS.");
                            }

                            // 5. Deploy Container
                            var containerService = scope.ServiceProvider.GetRequiredService<IContainerService>();
                            var imageTag = $"orion/{buildJob.AppName.ToLower()}:latest";
                            var containerName = $"orion-{buildJob.AppName.ToLower()}-{buildJob.Id.ToString()[..8]}";
                            
                            var result = await containerService.StartContainerAsync(imageTag, containerName, secrets);
                            
                            // 6. Record Instance
                            var instance = new Instance
                            {
                                AppId = buildJob.AppId,
                                DeploymentId = buildJob.Id,
                                OwnerId = buildJob.OwnerId,
                                ContainerName = containerName,
                                Port = result.Port,
                                ProcessId = result.ProcessId,
                                Status = "Running"
                            };
                            await dbService.CreateInstanceAsync(instance);

                            // 6. Update Database (Running)
                            await dbService.UpdateDeploymentStatusAsync(buildJob.Id, DeploymentStatus.Running, imageTag, result.Port);
                            _logger.LogInformation($"Job {buildJob.Id} finished successfully. (Port: {result.Port})");
                        }
                        else
                        {
                            // 3. Update Database (Failed)
                            await dbService.UpdateDeploymentStatusAsync(buildJob.Id, DeploymentStatus.Failed);
                        }
                         _logger.LogInformation($"Job {job.Id} finished (Success: {success})");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing job.");
                }
            }

            _logger.LogInformation("JobWorker stopped.");
        }
    }
}
