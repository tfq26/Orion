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
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public enum DeploymentStatus
    {
        Pending,
        Building,
        Deploying,
        Running,
        Failed
    }

    public class Deployment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid AppId { get; set; }
        public string OwnerId { get; set; } = string.Empty;
        public DeploymentStatus Status { get; set; } = DeploymentStatus.Pending;
        public string? ImageTag { get; set; }
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

    public class ExploreRequest
    {
        public string RepoUrl { get; set; } = string.Empty;
    }
}
