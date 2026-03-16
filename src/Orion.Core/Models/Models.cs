using System;

namespace Orion.Core.Models
{
    public class App
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string RepoUrl { get; set; } = string.Empty;
        public string OwnerId { get; set; } = string.Empty;
        public string? BuildCommand { get; set; }
        public string? RunCommand { get; set; }
        public string? BuildFolder { get; set; }
        public int? RequiredCpuCores { get; set; }
        public int? RequiredMemoryMb { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum DeploymentStatus
    {
        Pending,
        Building,
        Deploying,
        Running,
        Failed,
        Paused
    }

    public class Deployment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AppId { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
        public string? ImageTag { get; set; }
        public string? SourceVersion { get; set; }
        public int? Port { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LogEntry
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid? AppId { get; set; }
        public Guid? DeploymentId { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public string Level { get; set; } = "Info";
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class Instance
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid DeploymentId { get; set; }
        public Guid AppId { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public string ContainerName { get; set; } = string.Empty;
        public int Port { get; set; }
        public int? ProcessId { get; set; }
        public int? AssignedCpuCores { get; set; }
        public int? AssignedMemoryMb { get; set; }
        public string Status { get; set; } = "Running";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class Peer
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string IpAddress { get; set; } = string.Empty;
        public string Status { get; set; } = "Offline";
        public string Tags { get; set; } = string.Empty;
        public DateTime LastSeen { get; set; } = DateTime.UtcNow;
    }

    public class AppSummary
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string LatestBuildStatus { get; set; } = "Unknown";
        public DateTime? LatestBuildAt { get; set; }
        public string Stability { get; set; } = "Unknown";
        public int ActiveReplicas { get; set; }
        public double CpuUsage { get; set; }
        public int MemoryUsageMb { get; set; }
    }

    public class DashboardSummary
    {
        public int TotalApps { get; set; }
        public int TotalInstances { get; set; }
        public int ConnectedPeers { get; set; }
        public string ControlPlaneArch { get; set; } = "Unknown";
        public List<AppSummary> Apps { get; set; } = new();
        public string PilotStatus { get; set; } = "Observing";
    }

    public class NodeTelemetrySample
    {
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public double CpuUsage { get; set; }
        public double MemoryUsagePercent { get; set; }
        public double MemoryUsageGb { get; set; }
        public double StorageUsagePercent { get; set; }
        public double StorageUsageGb { get; set; }
        public double NetworkTrafficMbps { get; set; }
    }

    public class NodeTelemetrySnapshot
    {
        public string NodeName { get; set; } = "Current Node";
        public string Architecture { get; set; } = "Unknown";
        public List<NodeTelemetrySample> Samples { get; set; } = new();
        public List<NodeTelemetrySample> HourlySamples { get; set; } = new();
        public List<NodeTelemetrySample> DailySamples { get; set; } = new();
    }

    public class ExploreRequest
    {
        public string RepoUrl { get; set; } = string.Empty;
    }

    public class DeploymentAssessmentReport
    {
        public Guid AppId { get; set; }
        public string AppName { get; set; } = string.Empty;
        public string Stability { get; set; } = "Unknown";
        public string RecommendedAction { get; set; } = "Hold";
        public int CurrentReplicas { get; set; }
        public int RecommendedReplicas { get; set; }
        public double CpuUsage { get; set; }
        public int MemoryUsageMb { get; set; }
        public int AllocatedCpuCores { get; set; }
        public int AllocatedMemoryMb { get; set; }
        public string Review { get; set; } = string.Empty;
        public List<string> Findings { get; set; } = new();
    }
}
