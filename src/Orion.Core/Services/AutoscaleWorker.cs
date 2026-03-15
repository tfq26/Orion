using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public class AutoscaleWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AutoscaleWorker> _logger;

        public AutoscaleWorker(IServiceProvider serviceProvider, ILogger<AutoscaleWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Autoscale Worker started.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IMetadataService>();
                    var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
                    var scaleService = scope.ServiceProvider.GetRequiredService<IScaleService>();

                    var apps = await db.GetAppsAsync();
                    foreach (var app in apps)
                    {
                        var metrics = await metricsService.GetMetricsAsync(app.Id);
                        var activeInstances = await db.GetActiveInstancesAsync();
                        int currentCount = activeInstances.Count(i => i.AppId == app.Id);

                        if (currentCount == 0) continue; // No running instances to scale

                        _logger.LogDebug($"[AUTOSCALE] App: {app.Name}, CPU: {metrics.CpuUsage:F1}%, Replicas: {currentCount}");

                        if (metrics.CpuUsage > 70 && currentCount < 10)
                        {
                            _logger.LogInformation($"[AUTOSCALE] High CPU detected ({metrics.CpuUsage:F1}%). Scaling up {app.Name}...");
                            await scaleService.ScaleAsync(app.Id, currentCount + 1);
                        }
                        else if (metrics.CpuUsage < 20 && currentCount > 1)
                        {
                            _logger.LogInformation($"[AUTOSCALE] Low CPU detected ({metrics.CpuUsage:F1}%). Scaling down {app.Name}...");
                            await scaleService.ScaleAsync(app.Id, currentCount - 1);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Autoscale Worker");
                }

                await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            }

            _logger.LogInformation("Autoscale Worker stopped.");
        }
    }
}
