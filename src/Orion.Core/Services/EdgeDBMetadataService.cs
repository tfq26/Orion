using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EdgeDB;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public class EdgeDBMetadataService : IMetadataService
    {
        private readonly EdgeDBClient _client;
        private readonly ILogger<EdgeDBMetadataService> _logger;

        public EdgeDBMetadataService(EdgeDBClient client, ILogger<EdgeDBMetadataService> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("EdgeDBMetadataService initializing... Connection check.");
            try 
            {
                // 5-second connection check to prevent hanging the whole boot process
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _client.QueryAsync<long>("select 1", token: cts.Token);
                _logger.LogInformation("EdgeDB connection successful.");
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"EdgeDB connection failed: {ex.Message}. Cluster will run in degraded mode.");
            }
        }

        public async Task<IEnumerable<App>> GetAppsAsync(string? userId = null)
        {
            var query = "SELECT App { id, name, repo_url, build_command, run_command, build_folder, required_cpu_cores, required_memory_mb, owner_id }";
            if (!string.IsNullOrEmpty(userId))
            {
                query += " FILTER .owner_id = <str>$userId";
            }
            
            var results = await _client.QueryAsync<App>(query, new Dictionary<string, object?> { { "userId", userId } });
            return results.Where(a => a != null)!;
        }

        public async Task CreateAppAsync(App app)
        {
            var query = @"
                INSERT App {
                    name := <str>$name,
                    repo_url := <str>$repoUrl,
                    build_command := <str>$buildCommand,
                    run_command := <str>$runCommand,
                    build_folder := <str>$buildFolder,
                    required_cpu_cores := <int32>$cpu,
                    required_memory_mb := <int32>$mem,
                    owner_id := <str>$owner
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "name", app.Name },
                { "repoUrl", app.RepoUrl },
                { "buildCommand", app.BuildCommand },
                { "runCommand", app.RunCommand },
                { "buildFolder", app.BuildFolder },
                { "cpu", app.RequiredCpuCores },
                { "mem", app.RequiredMemoryMb },
                { "owner", app.OwnerId }
            });
        }

        public async Task UpdateAppAsync(App app)
        {
            var query = @"
                UPDATE App
                FILTER .id = <uuid>$id
                SET {
                    name := <str>$name,
                    repo_url := <str>$repoUrl,
                    build_command := <str>$buildCommand,
                    run_command := <str>$runCommand,
                    build_folder := <str>$buildFolder,
                    required_cpu_cores := <int32>$cpu,
                    required_memory_mb := <int32>$mem
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "id", app.Id },
                { "name", app.Name },
                { "repoUrl", app.RepoUrl },
                { "buildCommand", app.BuildCommand },
                { "runCommand", app.RunCommand },
                { "buildFolder", app.BuildFolder },
                { "cpu", app.RequiredCpuCores },
                { "mem", app.RequiredMemoryMb }
            });
        }

        public async Task DeleteAppAsync(Guid appId)
        {
            var query = "DELETE App FILTER .id = <uuid>$id";
            await _client.ExecuteAsync(query, new Dictionary<string, object?> { { "id", appId } });
        }

        public async Task<App?> GetAppByNameAsync(string name, string? userId = null)
        {
            var query = "SELECT App { id, name, repo_url, build_command, run_command, build_folder, required_cpu_cores, required_memory_mb, owner_id } FILTER .name = <str>$name";
            if (!string.IsNullOrEmpty(userId))
            {
                query += " AND .owner_id = <str>$userId";
            }

            return await _client.QuerySingleAsync<App>(query, new Dictionary<string, object?> { { "name", name }, { "userId", userId } });
        }

        public async Task<IEnumerable<Deployment>> GetDeploymentsAsync(Guid appId, string? userId = null)
        {
            var query = "SELECT Deployment { id, status, created_at, image_tag, port, owner_id } FILTER .app.id = <uuid>$appId";
            var results = await _client.QueryAsync<Deployment>(query, new Dictionary<string, object?> { { "appId", appId } });
            return results.Where(d => d != null)!;
        }

        public async Task CreateDeploymentAsync(Deployment deployment)
        {
            var query = @"
                INSERT Deployment {
                    app := (SELECT App FILTER .id = <uuid>$appId),
                    status := <str>$status,
                    image_tag := <str>$imageTag,
                    port := <int32>$port,
                    owner_id := <str>$owner
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "appId", deployment.AppId },
                { "status", deployment.Status.ToString() },
                { "imageTag", deployment.ImageTag },
                { "port", deployment.Port },
                { "owner", deployment.OwnerId }
            });
        }

        public async Task UpdateDeploymentStatusAsync(Guid deploymentId, DeploymentStatus status, string? imageTag = null, int? port = null)
        {
            var query = @"
                UPDATE Deployment
                FILTER .id = <uuid>$id
                SET {
                    status := <str>$status,
                    image_tag := <str>$imageTag ?? .image_tag,
                    port := <int32>$port ?? .port
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "id", deploymentId },
                { "status", status.ToString() },
                { "imageTag", imageTag },
                { "port", port }
            });
        }

        public async Task<Deployment?> GetDeploymentByIdAsync(Guid deploymentId, string? userId = null)
        {
            var query = "SELECT Deployment { id, status, created_at, image_tag, port, owner_id } FILTER .id = <uuid>$id";
            return await _client.QuerySingleAsync<Deployment>(query, new Dictionary<string, object?> { { "id", deploymentId } });
        }

        public async Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync(string? userId = null)
        {
            var query = "SELECT Deployment { id, status, created_at, image_tag, port, owner_id } FILTER .status = 'Running'";
            var results = await _client.QueryAsync<Deployment>(query);
            return results.Where(d => d != null)!;
        }

        public async Task<Dictionary<string, string>> GetSecretsAsync(Guid appId, string? userId = null)
        {
            var query = "SELECT Secret { key, encrypted_value } FILTER .app.id = <uuid>$appId";
            var results = await _client.QueryAsync<SecretProxy>(query, new Dictionary<string, object?> { { "appId", appId } });
            return results.Where(s => s != null).ToDictionary(s => s!.Key, s => s!.EncryptedValue);
        }

        private class SecretProxy { public string Key { get; set; } = ""; public string EncryptedValue { get; set; } = ""; }

        public async Task SaveSecretAsync(Guid appId, string key, string encryptedValue)
        {
            var query = @"
                INSERT Secret {
                    app := (SELECT App FILTER .id = <uuid>$appId),
                    key := <str>$key,
                    encrypted_value := <str>$val
                } UNLESS CONFLICT ON (.app, .key)
                ELSE (
                    UPDATE Secret
                    SET { encrypted_value := <str>$val }
                )";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "appId", appId },
                { "key", key },
                { "val", encryptedValue }
            });
        }

        public async Task DeleteSecretAsync(Guid appId, string key)
        {
            var query = "DELETE Secret FILTER .app.id = <uuid>$appId AND .key = <str>$key";
            await _client.ExecuteAsync(query, new Dictionary<string, object?> { { "appId", appId }, { "key", key } });
        }

        public async Task CreateInstanceAsync(Instance instance)
        {
            var query = @"
                INSERT Instance {
                    app := (SELECT App FILTER .id = <uuid>$appId),
                    deployment := (SELECT Deployment FILTER .id = <uuid>$depId),
                    container_name := <str>$name,
                    port := <int32>$port,
                    process_id := <int32>$pid,
                    assigned_cpu_cores := <int32>$cpu,
                    assigned_memory_mb := <int32>$mem,
                    status := <str>$status,
                    owner_id := <str>$owner
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "appId", instance.AppId },
                { "depId", instance.DeploymentId },
                { "name", instance.ContainerName },
                { "port", instance.Port },
                { "pid", instance.ProcessId },
                { "cpu", instance.AssignedCpuCores },
                { "mem", instance.AssignedMemoryMb },
                { "status", instance.Status },
                { "owner", instance.OwnerId }
            });
        }

        public async Task DeleteInstanceAsync(Guid instanceId)
        {
            var query = "DELETE Instance FILTER .id = <uuid>$id";
            await _client.ExecuteAsync(query, new Dictionary<string, object?> { { "id", instanceId } });
        }

        public async Task<IEnumerable<Instance>> GetActiveInstancesAsync(string? userId = null)
        {
            var query = "SELECT Instance { id, app := { id }, deployment := { id }, container_name, port, process_id, assigned_cpu_cores, assigned_memory_mb, status, created_at, owner_id } FILTER .status = 'Running'";
            var results = await _client.QueryAsync<InstanceProxy>(query);
            return results.Where(r => r != null).Select(r => new Instance 
            {
                Id = r!.Id,
                AppId = r.App.Id,
                DeploymentId = r.Deployment.Id,
                ContainerName = r.ContainerName,
                Port = r.Port,
                ProcessId = r.ProcessId,
                AssignedCpuCores = r.AssignedCpuCores,
                AssignedMemoryMb = r.AssignedMemoryMb,
                Status = r.Status,
                CreatedAt = r.CreatedAt.DateTime,
                OwnerId = r.OwnerId
            });
        }

        private class InstanceProxy 
        {
            public Guid Id { get; set; }
            public IdOnly App { get; set; } = new();
            public IdOnly Deployment { get; set; } = new();
            public string ContainerName { get; set; } = "";
            public int Port { get; set; }
            public int? ProcessId { get; set; }
            public int? AssignedCpuCores { get; set; }
            public int? AssignedMemoryMb { get; set; }
            public string Status { get; set; } = "";
            public DateTimeOffset CreatedAt { get; set; }
            public string OwnerId { get; set; } = "";
        }
        private class IdOnly { public Guid Id { get; set; } }

        public async Task<IEnumerable<Instance>> GetInstancesByDeploymentIdAsync(Guid deploymentId, string? userId = null)
        {
            var query = "SELECT Instance { id, app := { id }, deployment := { id }, container_name, port, process_id, assigned_cpu_cores, assigned_memory_mb, status, created_at, owner_id } FILTER .deployment.id = <uuid>$depId";
            var results = await _client.QueryAsync<InstanceProxy>(query, new Dictionary<string, object?> { { "depId", deploymentId } });
            return results.Where(r => r != null).Select(r => new Instance 
            {
                Id = r!.Id,
                AppId = r.App.Id,
                DeploymentId = r.Deployment.Id,
                ContainerName = r.ContainerName,
                Port = r.Port,
                ProcessId = r.ProcessId,
                AssignedCpuCores = r.AssignedCpuCores,
                AssignedMemoryMb = r.AssignedMemoryMb,
                Status = r.Status,
                CreatedAt = r.CreatedAt.DateTime,
                OwnerId = r.OwnerId
            });
        }

        public async Task<IEnumerable<Peer>> GetPeersAsync()
        {
            var query = "SELECT Peer { id, name, ip_address, status, tags, last_seen }";
            var results = await _client.QueryAsync<PeerProxy>(query);
            return results.Where(p => p != null).Select(p => new Peer
            {
                Id = p!.Id,
                Name = p.Name,
                IpAddress = p.IpAddress,
                Status = p.Status,
                Tags = p.Tags,
                LastSeen = p.LastSeen.DateTime
            });
        }

        private class PeerProxy 
        {
            public Guid Id { get; set; }
            public string Name { get; set; } = "";
            public string IpAddress { get; set; } = "";
            public string Status { get; set; } = "";
            public string Tags { get; set; } = "";
            public DateTimeOffset LastSeen { get; set; }
        }

        public async Task CreatePeerAsync(Peer peer)
        {
            var query = @"
                INSERT Peer {
                    name := <str>$name,
                    ip_address := <str>$ip,
                    status := <str>$status,
                    tags := <str>$tags
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "name", peer.Name },
                { "ip", peer.IpAddress },
                { "status", peer.Status },
                { "tags", peer.Tags }
            });
        }

        public async Task UpdatePeerStatusAsync(Guid peerId, string status, string? ip = null)
        {
            var query = @"
                UPDATE Peer
                FILTER .id = <uuid>$id
                SET {
                    status := <str>$status,
                    ip_address := <str>$ip ?? .ip_address,
                    last_seen := datetime_current()
                }";

            await _client.ExecuteAsync(query, new Dictionary<string, object?>
            {
                { "id", peerId },
                { "status", status },
                { "ip", ip }
            });
        }
    }
}
