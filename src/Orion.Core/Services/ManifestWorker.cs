using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public class ManifestWorker : BackgroundService
    {
        private readonly ILogger<ManifestWorker> _logger;
        private readonly IManifestService _manifestService;

        public ManifestWorker(ILogger<ManifestWorker> logger, IManifestService manifestService)
        {
            _logger = logger;
            _manifestService = manifestService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Orion Manifest Controller started.");

            // Wait a bit for other services to initialize
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogDebug("Synchronizing manifest desired state...");
                    await _manifestService.SynchronizeAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during manifest synchronization.");
                }

                // Check manifest every 10 seconds for GitOps-style responsiveness
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
