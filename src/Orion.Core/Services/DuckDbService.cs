using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface IDatabaseService
    {
        Task InitializeAsync();
        Task<IEnumerable<App>> GetAppsAsync();
        Task CreateAppAsync(App app);
        Task<IEnumerable<Deployment>> GetDeploymentsAsync(Guid appId);
        Task CreateDeploymentAsync(Deployment deployment);
        Task UpdateDeploymentStatusAsync(Guid deploymentId, DeploymentStatus status, string? imageTag = null, int? port = null);
        Task<Deployment?> GetDeploymentByIdAsync(Guid deploymentId);
        Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync();
        Task<App?> GetAppByNameAsync(string name);
        Task CreateLogAsync(LogEntry log);
        Task<IEnumerable<LogEntry>> GetLogsAsync(Guid appId, Guid? deploymentId = null);
        Task<Dictionary<string, string>> GetSecretsAsync(Guid appId);
        Task SaveSecretAsync(Guid appId, string key, string encryptedValue);
        Task DeleteSecretAsync(Guid appId, string key);
        Task CreateInstanceAsync(Instance instance);
        Task DeleteInstanceAsync(Guid instanceId);
        Task<IEnumerable<Instance>> GetActiveInstancesAsync();
        Task<IEnumerable<Instance>> GetInstancesByDeploymentIdAsync(Guid deploymentId);
        Task<IEnumerable<Peer>> GetPeersAsync();
        Task CreatePeerAsync(Peer peer);
        Task UpdatePeerStatusAsync(Guid peerId, string status, string? ip = null);
    }

    public class DuckDbService : IDatabaseService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DuckDbService(string dbPath = "orion.db")
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath}";
        }

        public async Task InitializeAsync()
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS apps (
                    id UUID PRIMARY KEY,
                    name TEXT UNIQUE,
                    repo_url TEXT,
                    created_at TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS deployments (
                    id UUID PRIMARY KEY,
                    app_id UUID,
                    status TEXT,
                    image_tag TEXT,
                    port INTEGER,
                    created_at TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS logs (
                    id UUID PRIMARY KEY,
                    app_id UUID,
                    deployment_id UUID,
                    message TEXT,
                    level TEXT,
                    timestamp TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS secrets (
                    app_id UUID,
                    key TEXT,
                    encrypted_value TEXT,
                    PRIMARY KEY (app_id, key)
                );

                CREATE TABLE IF NOT EXISTS instances (
                    id UUID PRIMARY KEY,
                    deployment_id UUID,
                    app_id UUID,
                    container_name TEXT,
                    port INTEGER,
                    process_id INTEGER,
                    status TEXT,
                    created_at TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS peers (
                    id UUID PRIMARY KEY,
                    name TEXT UNIQUE,
                    ip_address TEXT,
                    status TEXT,
                    tags TEXT,
                    last_seen TIMESTAMP
                );
            ";
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<App>> GetAppsAsync()
        {
            var apps = new List<App>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, repo_url, created_at FROM apps";
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                apps.Add(new App
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    RepoUrl = reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                });
            }
            return apps;
        }

        public async Task CreateAppAsync(App app)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO apps (id, name, repo_url, created_at) VALUES (?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(app.Id));
            cmd.Parameters.Add(new DuckDBParameter(app.Name));
            cmd.Parameters.Add(new DuckDBParameter(app.RepoUrl));
            cmd.Parameters.Add(new DuckDBParameter(app.CreatedAt));
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<Deployment>> GetDeploymentsAsync(Guid appId)
        {
            var deployments = new List<Deployment>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, status, image_tag, port, created_at FROM deployments WHERE app_id = ?";
            cmd.Parameters.Add(new DuckDBParameter(appId));
            
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                deployments.Add(new Deployment
                {
                    Id = reader.GetGuid(0),
                    AppId = reader.GetGuid(1),
                    Status = Enum.Parse<DeploymentStatus>(reader.GetString(2)),
                    ImageTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Port = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
            return deployments;
        }

        public async Task<Dictionary<string, string>> GetSecretsAsync(Guid appId)
        {
            var secrets = new Dictionary<string, string>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT key, encrypted_value FROM secrets WHERE app_id = ?";
            cmd.Parameters.Add(new DuckDBParameter(appId));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                secrets[reader.GetString(0)] = reader.GetString(1);
            }
            return secrets;
        }

        public async Task SaveSecretAsync(Guid appId, string key, string encryptedValue)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO secrets (app_id, key, encrypted_value) VALUES (?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(appId));
            cmd.Parameters.Add(new DuckDBParameter(key));
            cmd.Parameters.Add(new DuckDBParameter(encryptedValue));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteSecretAsync(Guid appId, string key)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM secrets WHERE app_id = ? AND key = ?";
            cmd.Parameters.Add(new DuckDBParameter(appId));
            cmd.Parameters.Add(new DuckDBParameter(key));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CreateInstanceAsync(Instance instance)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO instances (id, deployment_id, app_id, container_name, port, process_id, status, created_at) VALUES (?, ?, ?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(instance.Id));
            cmd.Parameters.Add(new DuckDBParameter(instance.DeploymentId));
            cmd.Parameters.Add(new DuckDBParameter(instance.AppId));
            cmd.Parameters.Add(new DuckDBParameter(instance.ContainerName));
            cmd.Parameters.Add(new DuckDBParameter(instance.Port));
            cmd.Parameters.Add(new DuckDBParameter(instance.ProcessId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(instance.Status));
            cmd.Parameters.Add(new DuckDBParameter(instance.CreatedAt));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteInstanceAsync(Guid instanceId)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM instances WHERE id = ?";
            cmd.Parameters.Add(new DuckDBParameter(instanceId));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<Instance>> GetActiveInstancesAsync()
        {
            var instances = new List<Instance>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, deployment_id, app_id, container_name, port, process_id, status, created_at FROM instances WHERE status = 'Running'";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                instances.Add(new Instance
                {
                    Id = reader.GetGuid(0),
                    DeploymentId = reader.GetGuid(1),
                    AppId = reader.GetGuid(2),
                    ContainerName = reader.GetString(3),
                    Port = reader.GetInt32(4),
                    ProcessId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Status = reader.GetString(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
            return instances;
        }

        public async Task<IEnumerable<Instance>> GetInstancesByDeploymentIdAsync(Guid deploymentId)
        {
            var instances = new List<Instance>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, deployment_id, app_id, container_name, port, process_id, status, created_at FROM instances WHERE deployment_id = ?";
            cmd.Parameters.Add(new DuckDBParameter(deploymentId));

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                instances.Add(new Instance
                {
                    Id = reader.GetGuid(0),
                    DeploymentId = reader.GetGuid(1),
                    AppId = reader.GetGuid(2),
                    ContainerName = reader.GetString(3),
                    Port = reader.GetInt32(4),
                    ProcessId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Status = reader.GetString(6),
                    CreatedAt = reader.GetDateTime(7)
                });
            }
            return instances;
        }

        public async Task CreateDeploymentAsync(Deployment deployment)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO deployments (id, app_id, status, image_tag, port, created_at) VALUES (?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(deployment.Id));
            cmd.Parameters.Add(new DuckDBParameter(deployment.AppId));
            cmd.Parameters.Add(new DuckDBParameter(deployment.Status.ToString()));
            cmd.Parameters.Add(new DuckDBParameter(deployment.ImageTag ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(deployment.Port ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(deployment.CreatedAt));
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDeploymentStatusAsync(Guid deploymentId, DeploymentStatus status, string? imageTag = null, int? port = null)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE deployments SET status = ?, image_tag = ?, port = ? WHERE id = ?";
            cmd.Parameters.Add(new DuckDBParameter(status.ToString()));
            cmd.Parameters.Add(new DuckDBParameter(imageTag ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(port ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(deploymentId));
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Deployment?> GetDeploymentByIdAsync(Guid deploymentId)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, status, image_tag, port, created_at FROM deployments WHERE id = ?";
            cmd.Parameters.Add(new DuckDBParameter(deploymentId));

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Deployment
                {
                    Id = reader.GetGuid(0),
                    AppId = reader.GetGuid(1),
                    Status = Enum.Parse<DeploymentStatus>(reader.GetString(2)),
                    ImageTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Port = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt = reader.GetDateTime(5)
                };
            }
            return null;
        }

        public async Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync()
        {
            var deployments = new List<Deployment>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, status, image_tag, port, created_at FROM deployments WHERE status = 'Running' AND port IS NOT NULL";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                deployments.Add(new Deployment
                {
                    Id = reader.GetGuid(0),
                    AppId = reader.GetGuid(1),
                    Status = Enum.Parse<DeploymentStatus>(reader.GetString(2)),
                    ImageTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Port = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt = reader.GetDateTime(5)
                });
            }
            return deployments;
        }

        public async Task<App?> GetAppByNameAsync(string name)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, repo_url, created_at FROM apps WHERE name = ?";
            cmd.Parameters.Add(new DuckDBParameter(name));
            
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new App
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    RepoUrl = reader.GetString(2),
                    CreatedAt = reader.GetDateTime(3)
                };
            }
            return null;
        }

        public async Task CreateLogAsync(LogEntry log)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO logs (id, app_id, deployment_id, message, level, timestamp) VALUES (?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(log.Id));
            cmd.Parameters.Add(new DuckDBParameter(log.AppId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(log.DeploymentId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(log.Message));
            cmd.Parameters.Add(new DuckDBParameter(log.Level));
            cmd.Parameters.Add(new DuckDBParameter(log.Timestamp));
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<LogEntry>> GetLogsAsync(Guid appId, Guid? deploymentId = null)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            if (deploymentId.HasValue)
            {
                cmd.CommandText = "SELECT id, app_id, deployment_id, message, level, timestamp FROM logs WHERE deployment_id = ? ORDER BY timestamp ASC";
                cmd.Parameters.Add(new DuckDBParameter(deploymentId.Value));
            }
            else
            {
                cmd.CommandText = "SELECT id, app_id, deployment_id, message, level, timestamp FROM logs WHERE app_id = ? ORDER BY timestamp ASC";
                cmd.Parameters.Add(new DuckDBParameter(appId));
            }

            using var reader = await cmd.ExecuteReaderAsync();
            var logs = new List<LogEntry>();
            while (await reader.ReadAsync())
            {
                logs.Add(new LogEntry
                {
                    Id = reader.GetGuid(0),
                    AppId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    DeploymentId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    Message = reader.GetString(3),
                    Level = reader.GetString(4),
                    Timestamp = reader.GetDateTime(5)
                });
            }
            return logs;
        }
        public async Task<IEnumerable<Peer>> GetPeersAsync()
        {
            var peers = new List<Peer>();
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, ip_address, status, tags, last_seen FROM peers";

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                peers.Add(new Peer
                {
                    Id = reader.GetGuid(0),
                    Name = reader.GetString(1),
                    IpAddress = reader.GetString(2),
                    Status = reader.GetString(3),
                    Tags = reader.GetString(4),
                    LastSeen = reader.GetDateTime(5)
                });
            }
            return peers;
        }

        public async Task CreatePeerAsync(Peer peer)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO peers (id, name, ip_address, status, tags, last_seen) VALUES (?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(peer.Id));
            cmd.Parameters.Add(new DuckDBParameter(peer.Name));
            cmd.Parameters.Add(new DuckDBParameter(peer.IpAddress));
            cmd.Parameters.Add(new DuckDBParameter(peer.Status));
            cmd.Parameters.Add(new DuckDBParameter(peer.Tags));
            cmd.Parameters.Add(new DuckDBParameter(peer.LastSeen));

            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdatePeerStatusAsync(Guid peerId, string status, string? ip = null)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            if (ip != null)
            {
                cmd.CommandText = "UPDATE peers SET status = ?, ip_address = ?, last_seen = ? WHERE id = ?";
                cmd.Parameters.Add(new DuckDBParameter(status));
                cmd.Parameters.Add(new DuckDBParameter(ip));
                cmd.Parameters.Add(new DuckDBParameter(DateTime.UtcNow));
                cmd.Parameters.Add(new DuckDBParameter(peerId));
            }
            else
            {
                cmd.CommandText = "UPDATE peers SET status = ?, last_seen = ? WHERE id = ?";
                cmd.Parameters.Add(new DuckDBParameter(status));
                cmd.Parameters.Add(new DuckDBParameter(DateTime.UtcNow));
                cmd.Parameters.Add(new DuckDBParameter(peerId));
            }

            await cmd.ExecuteNonQueryAsync();
        }
    }
}
