using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using Orion.Core.Models;
using Orion.Core.Services;

namespace Orion.Core.Serialization
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(App))]
    [JsonSerializable(typeof(IEnumerable<App>))]
    [JsonSerializable(typeof(Deployment))]
    [JsonSerializable(typeof(IEnumerable<Deployment>))]
    [JsonSerializable(typeof(LogEntry))]
    [JsonSerializable(typeof(IEnumerable<LogEntry>))]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    [JsonSerializable(typeof(AppMetrics))]
    [JsonSerializable(typeof(DashboardSummary))]
    [JsonSerializable(typeof(AppSummary))]
    [JsonSerializable(typeof(List<AppSummary>))]
    [JsonSerializable(typeof(DeploymentAssessmentReport))]
    [JsonSerializable(typeof(NodeTelemetrySnapshot))]
    [JsonSerializable(typeof(NodeTelemetrySample))]
    [JsonSerializable(typeof(List<NodeTelemetrySample>))]
    [JsonSerializable(typeof(Peer))]
    [JsonSerializable(typeof(IEnumerable<Peer>))]
    [JsonSerializable(typeof(ExploreRequest))]
    [JsonSerializable(typeof(List<string>))]
    public partial class OrionJsonContext : JsonSerializerContext
    {
    }
}
