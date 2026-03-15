using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public class PilotWorker : BackgroundService
    {
        private readonly ILogger<PilotWorker> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PilotWorker(ILogger<PilotWorker> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Orion AI Pilot Worker starting...");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var pilot = scope.ServiceProvider.GetRequiredService<IPilotService>();
                        var report = await pilot.AnalyzeHealthAsync();
                        
                        if (!report.IsHealthy)
                        {
                            _logger.LogWarning($"[PILOT] System unhealthy! Issues: {string.Join(", ", report.Issues)}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error in AI Pilot Worker: {ex.Message}");
                }

                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            }
        }
    }
}
