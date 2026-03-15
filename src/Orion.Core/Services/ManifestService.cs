using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;
using Orion.Core.Jobs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Orion.Core.Services
{
    public interface IManifestService
    {
        Task ApplyManifestAsync(string body);
        Task SynchronizeAsync();
    }

    public class ManifestService : IManifestService
    {
        private readonly ILogger<ManifestService> _logger;
        private readonly IMetadataService _db;
        private readonly IScaleService _scaleService;
        private readonly ISecretService _secretService;
        private readonly IJobDispatcher _dispatcher;
        private readonly string _manifestPath;

        public ManifestService(
            ILogger<ManifestService> logger,
            IMetadataService db,
            IScaleService scaleService,
            ISecretService secretService,
            IJobDispatcher dispatcher)
        {
            _logger = logger;
            _db = db;
            _scaleService = scaleService;
            _secretService = secretService;
            _dispatcher = dispatcher;
            _manifestPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orion.yaml");
        }

        public async Task ApplyManifestAsync(string body)
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var manifest = deserializer.Deserialize<OrionManifest>(body);
            _logger.LogInformation($"Applying manifest version {manifest.Version} with {manifest.Workloads.Count} workloads.");

            var existingApps = (await _db.GetAppsAsync()).ToList();

            foreach (var workload in manifest.Workloads)
            {
                var app = existingApps.FirstOrDefault(a => a.Name.Equals(workload.Name, StringComparison.OrdinalIgnoreCase));
                
                if (app == null)
                {
                    _logger.LogInformation($"[MANIFEST] Creating new app: {workload.Name}");
                    app = new App { Name = workload.Name, RepoUrl = workload.Wasm }; // Using RepoUrl to store WASM path for now
                    await _db.CreateAppAsync(app);
                }

                // Ensure there is at least one Running deployment to scale from
                var deployments = await _db.GetDeploymentsAsync(app.Id);
                var latestRunning = deployments.OrderByDescending(d => d.CreatedAt)
                    .FirstOrDefault(d => d.Status == DeploymentStatus.Running);

                if (latestRunning == null)
                {
                    // Check if there's already a build in progress
                    var hasBuild = deployments.Any(d => d.Status == DeploymentStatus.Building || d.Status == DeploymentStatus.Pending);
                    if (!hasBuild)
                    {
                        _logger.LogInformation($"[MANIFEST] Triggering initial build for {workload.Name}");
                        var bootstrapDeployment = new Deployment
                        {
                            AppId = app.Id,
                            Status = DeploymentStatus.Pending,
                            ImageTag = workload.Wasm,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _db.CreateDeploymentAsync(bootstrapDeployment);

                        await _dispatcher.EnqueueAsync(new BuildJob
                        {
                            Id = bootstrapDeployment.Id,
                            AppId = app.Id,
                            AppName = app.Name,
                            RepoUrl = app.RepoUrl
                        });
                    }
                }

                // Sync Secrets/Env
                if (workload.Env != null)
                {
                    foreach (var kvp in workload.Env)
                    {
                        await _secretService.SetSecretAsync(app.Id, kvp.Key, kvp.Value);
                    }
                }

                // Sync Scaling
                var instances = (await _db.GetActiveInstancesAsync()).Where(i => i.AppId == app.Id).ToList();
                if (instances.Count != workload.Replicas)
                {
                    _logger.LogInformation($"[MANIFEST] Reconciling scale for {workload.Name}: {instances.Count} -> {workload.Replicas}");
                    await _scaleService.ScaleAsync(app.Id, workload.Replicas);
                }
            }
            
            _logger.LogInformation("[MANIFEST] Apply completed.");
        }

        public async Task SynchronizeAsync()
        {
            if (!File.Exists(_manifestPath))
            {
                _logger.LogWarning($"Manifest file not found at {_manifestPath}. Creating default.");
                var defaultManifest = @"version: '1.0'
workloads: []
";
                await File.WriteAllTextAsync(_manifestPath, defaultManifest);
            }

            var body = await File.ReadAllTextAsync(_manifestPath);
            await ApplyManifestAsync(body);
        }
    }
}
