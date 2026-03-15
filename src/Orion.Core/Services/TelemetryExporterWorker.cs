using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public class TelemetryExporterWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TelemetryExporterWorker> _logger;

        public TelemetryExporterWorker(IServiceProvider serviceProvider, ILogger<TelemetryExporterWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Draco Telemetry Exporter Worker started.");

            // Wait a bit for Draco to potentially start up if running concurrently
            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<IMetadataService>();
                    var metricsService = scope.ServiceProvider.GetRequiredService<IMetricsService>();
                    var exporter = scope.ServiceProvider.GetRequiredService<IDracoTelemetryExporter>();
                    var telemetry = scope.ServiceProvider.GetRequiredService<ITelemetryService>();

                    var apps = await db.GetAppsAsync();
                    foreach (var app in apps)
                    {
                        var metrics = await metricsService.GetMetricsAsync(app.Id);
                        
                        // 1. Export to external Draco plane
                        await exporter.ExportMetricsAsync(metrics, app.Name);

                        // 2. Persist to local DuckDB for dashboard visibility
                        await telemetry.CreateMetricAsync(app.Id, app.OwnerId, metrics.CpuUsage, metrics.MemoryUsageMb);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error in Telemetry Exporter Worker");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }

            _logger.LogInformation("Draco Telemetry Exporter Worker stopped.");
        }
    }
}
