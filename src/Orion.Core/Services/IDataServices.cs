using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface IMetadataService
    {
        Task InitializeAsync();
        Task<IEnumerable<App>> GetAppsAsync(string? userId = null);
        Task CreateAppAsync(App app);
        Task UpdateAppAsync(App app);
        Task DeleteAppAsync(Guid appId);
        Task<App?> GetAppByNameAsync(string name, string? userId = null);
        Task<IEnumerable<Deployment>> GetDeploymentsAsync(Guid appId, string? userId = null);
        Task CreateDeploymentAsync(Deployment deployment);
        Task UpdateDeploymentStatusAsync(Guid deploymentId, DeploymentStatus status, string? imageTag = null, int? port = null);
        Task<Deployment?> GetDeploymentByIdAsync(Guid deploymentId, string? userId = null);
        Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync(string? userId = null);
        Task<Dictionary<string, string>> GetSecretsAsync(Guid appId, string? userId = null);
        Task SaveSecretAsync(Guid appId, string key, string encryptedValue);
        Task DeleteSecretAsync(Guid appId, string key);
        Task CreateInstanceAsync(Instance instance);
        Task DeleteInstanceAsync(Guid instanceId);
        Task<IEnumerable<Instance>> GetActiveInstancesAsync(string? userId = null);
        Task<IEnumerable<Instance>> GetInstancesByDeploymentIdAsync(Guid deploymentId, string? userId = null);
        Task<IEnumerable<Peer>> GetPeersAsync();
        Task CreatePeerAsync(Peer peer);
        Task UpdatePeerStatusAsync(Guid peerId, string status, string? ip = null);
    }

    public interface ITelemetryService
    {
        Task InitializeAsync();
        Task CreateLogAsync(LogEntry log);
        Task<IEnumerable<LogEntry>> GetLogsAsync(Guid appId, Guid? deploymentId = null, string? userId = null);
        Task CreateMetricAsync(Guid appId, string? userId, double cpu, int memory);
        Task<IEnumerable<dynamic>> GetMetricsAsync(Guid appId, string? userId = null, int limit = 100);
        Task DeleteAppTelemetryAsync(Guid appId, string? userId = null);
    }
}
