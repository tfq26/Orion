using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public class SqliteMetadataService : IMetadataService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public SqliteMetadataService(string dbPath = "orion_metadata.db")
        {
            _dbPath = dbPath;
            _connectionString = $"Data Source={_dbPath}";
        }

        public async Task InitializeAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS apps (
                    id TEXT PRIMARY KEY,
                    name TEXT UNIQUE,
                    repo_url TEXT,
                    owner_id TEXT,
                    build_command TEXT,
                    run_command TEXT,
                    build_folder TEXT,
                    created_at TEXT
                );

                CREATE TABLE IF NOT EXISTS deployments (
                    id TEXT PRIMARY KEY,
                    app_id TEXT,
                    owner_id TEXT,
                    status TEXT,
                    image_tag TEXT,
                    port INTEGER,
                    created_at TEXT
                );

                CREATE TABLE IF NOT EXISTS secrets (
                    app_id TEXT,
                    key TEXT,
                    encrypted_value TEXT,
                    PRIMARY KEY (app_id, key)
                );

                CREATE TABLE IF NOT EXISTS instances (
                    id TEXT PRIMARY KEY,
                    deployment_id TEXT,
                    app_id TEXT,
                    owner_id TEXT,
                    container_name TEXT,
                    port INTEGER,
                    process_id INTEGER,
                    status TEXT,
                    created_at TEXT
                );

                CREATE TABLE IF NOT EXISTS peers (
                    id TEXT PRIMARY KEY,
                    name TEXT UNIQUE,
                    ip_address TEXT,
                    status TEXT,
                    tags TEXT,
                    last_seen TEXT
                );
            ";
            await cmd.ExecuteNonQueryAsync();

            // Migration: Add owner_id if missing
            var tables = new[] { "apps", "deployments", "instances" };
            foreach (var table in tables)
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = $"PRAGMA table_info({table});";
                using var reader = await checkCmd.ExecuteReaderAsync();
                bool hasOwnerId = false;
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1) == "owner_id") hasOwnerId = true;
                }
                reader.Close();

                if (!hasOwnerId)
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE {table} ADD COLUMN owner_id TEXT DEFAULT '';";
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }

            // Migration: Add build_command, run_command, build_folder if missing in apps
            var appCols = new[] { "build_command", "run_command", "build_folder" };
            foreach (var col in appCols)
            {
                using var checkCmd = connection.CreateCommand();
                checkCmd.CommandText = "PRAGMA table_info(apps);";
                using var reader = await checkCmd.ExecuteReaderAsync();
                bool hasCol = false;
                while (await reader.ReadAsync())
                {
                    if (reader.GetString(1) == col) hasCol = true;
                }
                reader.Close();

                if (!hasCol)
                {
                    using var alterCmd = connection.CreateCommand();
                    alterCmd.CommandText = $"ALTER TABLE apps ADD COLUMN {col} TEXT;";
                    await alterCmd.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<IEnumerable<App>> GetAppsAsync(string? userId = null)
        {
            var apps = new List<App>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, repo_url, owner_id, build_command, run_command, build_folder, created_at FROM apps";
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " WHERE owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                apps.Add(new App
                {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    RepoUrl = reader.GetString(2),
                    OwnerId = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    BuildCommand = reader.IsDBNull(4) ? null : reader.GetString(4),
                    RunCommand = reader.IsDBNull(5) ? null : reader.GetString(5),
                    BuildFolder = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7))
                });
            }
            return apps;
        }

        public async Task CreateAppAsync(App app)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO apps (id, name, repo_url, owner_id, build_command, run_command, build_folder, created_at) VALUES (@id, @name, @repo, @owner, @build, @run, @folder, @created)";
            cmd.Parameters.AddWithValue("@id", app.Id.ToString());
            cmd.Parameters.AddWithValue("@name", app.Name);
            cmd.Parameters.AddWithValue("@repo", app.RepoUrl);
            cmd.Parameters.AddWithValue("@owner", app.OwnerId);
            cmd.Parameters.AddWithValue("@build", (object?)app.BuildCommand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@run", (object?)app.RunCommand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@folder", (object?)app.BuildFolder ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", app.CreatedAt.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateAppAsync(App app)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE apps SET name = @name, repo_url = @repo, build_command = @build, run_command = @run, build_folder = @folder WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", app.Id.ToString());
            cmd.Parameters.AddWithValue("@name", app.Name);
            cmd.Parameters.AddWithValue("@repo", app.RepoUrl);
            cmd.Parameters.AddWithValue("@build", (object?)app.BuildCommand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@run", (object?)app.RunCommand ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@folder", (object?)app.BuildFolder ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<App?> GetAppByNameAsync(string name, string? userId = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, repo_url, owner_id, build_command, run_command, build_folder, created_at FROM apps WHERE name = @name";
            cmd.Parameters.AddWithValue("@name", name);
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " AND owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new App {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    RepoUrl = reader.GetString(2),
                    OwnerId = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    BuildCommand = reader.IsDBNull(4) ? null : reader.GetString(4),
                    RunCommand = reader.IsDBNull(5) ? null : reader.GetString(5),
                    BuildFolder = reader.IsDBNull(6) ? null : reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7))
                };
            }
            return null;
        }

        public async Task<IEnumerable<Deployment>> GetDeploymentsAsync(Guid appId, string? userId = null)
        {
            var deployments = new List<Deployment>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, status, image_tag, port, created_at, owner_id FROM deployments WHERE app_id = @appId";
            cmd.Parameters.AddWithValue("@appId", appId.ToString());
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " AND owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                deployments.Add(new Deployment {
                    Id = Guid.Parse(reader.GetString(0)),
                    AppId = Guid.Parse(reader.GetString(1)),
                    Status = Enum.Parse<DeploymentStatus>(reader.GetString(2)),
                    ImageTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Port = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    OwnerId = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }
            return deployments;
        }

        public async Task CreateDeploymentAsync(Deployment deployment)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO deployments (id, app_id, owner_id, status, image_tag, port, created_at) VALUES (@id, @appId, @owner, @status, @tag, @port, @created)";
            cmd.Parameters.AddWithValue("@id", deployment.Id.ToString());
            cmd.Parameters.AddWithValue("@appId", deployment.AppId.ToString());
            cmd.Parameters.AddWithValue("@owner", deployment.OwnerId);
            cmd.Parameters.AddWithValue("@status", deployment.Status.ToString());
            cmd.Parameters.AddWithValue("@tag", (object?)deployment.ImageTag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@port", (object?)deployment.Port ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@created", deployment.CreatedAt.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdateDeploymentStatusAsync(Guid deploymentId, DeploymentStatus status, string? imageTag = null, int? port = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "UPDATE deployments SET status = @status, image_tag = @tag, port = @port WHERE id = @id";
            cmd.Parameters.AddWithValue("@status", status.ToString());
            cmd.Parameters.AddWithValue("@tag", (object?)imageTag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@port", (object?)port ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", deploymentId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<Deployment?> GetDeploymentByIdAsync(Guid deploymentId, string? userId = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, status, image_tag, port, created_at, owner_id FROM deployments WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", deploymentId.ToString());
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " AND owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new Deployment {
                    Id = Guid.Parse(reader.GetString(0)),
                    AppId = Guid.Parse(reader.GetString(1)),
                    Status = Enum.Parse<DeploymentStatus>(reader.GetString(2)),
                    ImageTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Port = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    OwnerId = reader.IsDBNull(6) ? "" : reader.GetString(6)
                };
            }
            return null;
        }

        public async Task<IEnumerable<Deployment>> GetActiveDeploymentsAsync(string? userId = null)
        {
            var deployments = new List<Deployment>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, app_id, status, image_tag, port, created_at, owner_id FROM deployments WHERE status = 'Running' AND port IS NOT NULL";
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " AND owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                deployments.Add(new Deployment {
                    Id = Guid.Parse(reader.GetString(0)),
                    AppId = Guid.Parse(reader.GetString(1)),
                    Status = Enum.Parse<DeploymentStatus>(reader.GetString(2)),
                    ImageTag = reader.IsDBNull(3) ? null : reader.GetString(3),
                    Port = reader.IsDBNull(4) ? null : reader.GetInt32(4),
                    CreatedAt = DateTime.Parse(reader.GetString(5)),
                    OwnerId = reader.IsDBNull(6) ? "" : reader.GetString(6)
                });
            }
            return deployments;
        }

        public async Task<Dictionary<string, string>> GetSecretsAsync(Guid appId, string? userId = null)
        {
            var secrets = new Dictionary<string, string>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Verify ownership first via a join or check
            using var cmd = connection.CreateCommand();
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText = @"
                    SELECT s.key, s.encrypted_value 
                    FROM secrets s
                    JOIN apps a ON s.app_id = a.id
                    WHERE s.app_id = @appId AND a.owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }
            else
            {
                cmd.CommandText = "SELECT key, encrypted_value FROM secrets WHERE app_id = @appId";
            }
            cmd.Parameters.AddWithValue("@appId", appId.ToString());

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                secrets[reader.GetString(0)] = reader.GetString(1);
            }
            return secrets;
        }

        public async Task SaveSecretAsync(Guid appId, string key, string encryptedValue)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT OR REPLACE INTO secrets (app_id, key, encrypted_value) VALUES (@appId, @key, @val)";
            cmd.Parameters.AddWithValue("@appId", appId.ToString());
            cmd.Parameters.AddWithValue("@key", key);
            cmd.Parameters.AddWithValue("@val", encryptedValue);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteSecretAsync(Guid appId, string key)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM secrets WHERE app_id = @appId AND key = @key";
            cmd.Parameters.AddWithValue("@appId", appId.ToString());
            cmd.Parameters.AddWithValue("@key", key);
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task CreateInstanceAsync(Instance instance)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO instances (id, deployment_id, app_id, owner_id, container_name, port, process_id, status, created_at) VALUES (@id, @depId, @appId, @owner, @name, @port, @pid, @status, @created)";
            cmd.Parameters.AddWithValue("@id", instance.Id.ToString());
            cmd.Parameters.AddWithValue("@depId", instance.DeploymentId.ToString());
            cmd.Parameters.AddWithValue("@appId", instance.AppId.ToString());
            cmd.Parameters.AddWithValue("@owner", instance.OwnerId);
            cmd.Parameters.AddWithValue("@name", instance.ContainerName);
            cmd.Parameters.AddWithValue("@port", instance.Port);
            cmd.Parameters.AddWithValue("@pid", (object?)instance.ProcessId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@status", instance.Status);
            cmd.Parameters.AddWithValue("@created", instance.CreatedAt.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task DeleteInstanceAsync(Guid instanceId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "DELETE FROM instances WHERE id = @id";
            cmd.Parameters.AddWithValue("@id", instanceId.ToString());
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<Instance>> GetActiveInstancesAsync(string? userId = null)
        {
            var instances = new List<Instance>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, deployment_id, app_id, container_name, port, process_id, status, created_at, owner_id FROM instances WHERE status = 'Running'";
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " AND owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                instances.Add(new Instance {
                    Id = Guid.Parse(reader.GetString(0)),
                    DeploymentId = Guid.Parse(reader.GetString(1)),
                    AppId = Guid.Parse(reader.GetString(2)),
                    ContainerName = reader.GetString(3),
                    Port = reader.GetInt32(4),
                    ProcessId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Status = reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    OwnerId = reader.IsDBNull(8) ? "" : reader.GetString(8)
                });
            }
            return instances;
        }

        public async Task<IEnumerable<Instance>> GetInstancesByDeploymentIdAsync(Guid deploymentId, string? userId = null)
        {
            var instances = new List<Instance>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, deployment_id, app_id, container_name, port, process_id, status, created_at, owner_id FROM instances WHERE deployment_id = @id";
            cmd.Parameters.AddWithValue("@id", deploymentId.ToString());
            if (!string.IsNullOrEmpty(userId))
            {
                cmd.CommandText += " AND owner_id = @userId";
                cmd.Parameters.AddWithValue("@userId", userId);
            }

            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                instances.Add(new Instance {
                    Id = Guid.Parse(reader.GetString(0)),
                    DeploymentId = Guid.Parse(reader.GetString(1)),
                    AppId = Guid.Parse(reader.GetString(2)),
                    ContainerName = reader.GetString(3),
                    Port = reader.GetInt32(4),
                    ProcessId = reader.IsDBNull(5) ? null : reader.GetInt32(5),
                    Status = reader.GetString(6),
                    CreatedAt = DateTime.Parse(reader.GetString(7)),
                    OwnerId = reader.IsDBNull(8) ? "" : reader.GetString(8)
                });
            }
            return instances;
        }

        public async Task<IEnumerable<Peer>> GetPeersAsync()
        {
            var peers = new List<Peer>();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT id, name, ip_address, status, tags, last_seen FROM peers";
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                peers.Add(new Peer {
                    Id = Guid.Parse(reader.GetString(0)),
                    Name = reader.GetString(1),
                    IpAddress = reader.GetString(2),
                    Status = reader.GetString(3),
                    Tags = reader.GetString(4),
                    LastSeen = DateTime.Parse(reader.GetString(5))
                });
            }
            return peers;
        }

        public async Task CreatePeerAsync(Peer peer)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO peers (id, name, ip_address, status, tags, last_seen) VALUES (@id, @name, @ip, @status, @tags, @seen)";
            cmd.Parameters.AddWithValue("@id", peer.Id.ToString());
            cmd.Parameters.AddWithValue("@name", peer.Name);
            cmd.Parameters.AddWithValue("@ip", peer.IpAddress);
            cmd.Parameters.AddWithValue("@status", peer.Status);
            cmd.Parameters.AddWithValue("@tags", peer.Tags);
            cmd.Parameters.AddWithValue("@seen", peer.LastSeen.ToString("o"));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task UpdatePeerStatusAsync(Guid peerId, string status, string? ip = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            if (ip != null)
            {
                cmd.CommandText = "UPDATE peers SET status = @status, ip_address = @ip, last_seen = @seen WHERE id = @id";
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@ip", ip);
                cmd.Parameters.AddWithValue("@seen", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", peerId.ToString());
            }
            else
            {
                cmd.CommandText = "UPDATE peers SET status = @status, last_seen = @seen WHERE id = @id";
                cmd.Parameters.AddWithValue("@status", status);
                cmd.Parameters.AddWithValue("@seen", DateTime.UtcNow.ToString("o"));
                cmd.Parameters.AddWithValue("@id", peerId.ToString());
            }
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
