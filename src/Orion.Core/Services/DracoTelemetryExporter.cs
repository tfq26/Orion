using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Orion.Core.Services
{
    public interface IDracoTelemetryExporter
    {
        Task ExportMetricsAsync(AppMetrics metrics, string appName);
    }

    public class DracoTelemetryExporter : IDracoTelemetryExporter
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<DracoTelemetryExporter> _logger;
        private const string DracoApiUrl = "http://localhost:5020/api/telemetry/metrics";

        public DracoTelemetryExporter(HttpClient httpClient, ILogger<DracoTelemetryExporter> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task ExportMetricsAsync(AppMetrics metrics, string appName)
        {
            try
            {
                var dracoMetrics = new List<object>
                {
                    new
                    {
                        ResourceId = $"orion/apps/{appName.ToLower()}",
                        MetricName = "CPUUsage",
                        Value = metrics.CpuUsage,
                        Unit = "Percent",
                        Timestamp = DateTimeOffset.UtcNow,
                        Dimensions = new Dictionary<string, string> { { "AppId", metrics.AppId.ToString() } }
                    },
                    new
                    {
                        ResourceId = $"orion/apps/{appName.ToLower()}",
                        MetricName = "MemoryUsage",
                        Value = (double)metrics.MemoryUsageMb,
                        Unit = "Megabytes",
                        Timestamp = DateTimeOffset.UtcNow,
                        Dimensions = new Dictionary<string, string> { { "AppId", metrics.AppId.ToString() } }
                    }
                };

                var response = await _httpClient.PostAsJsonAsync(DracoApiUrl, dracoMetrics);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug($"Successfully exported metrics for {appName} to Draco.");
                }
                else
                {
                    _logger.LogWarning($"Failed to export metrics for {appName} to Draco: {response.StatusCode}");
                }
            }
            catch (HttpRequestException)
            {
                // Draco is offline, ignore the exception to keep logs clean
                _logger.LogTrace($"Failed to connect to Draco at {DracoApiUrl}. Is it running?");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error exporting metrics for {appName} to Draco.");
            }
        }
    }
}
