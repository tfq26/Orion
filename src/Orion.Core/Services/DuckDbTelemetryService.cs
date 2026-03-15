using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DuckDB.NET.Data;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public class DuckDbTelemetryService : ITelemetryService
    {
        private readonly string _connectionString;
        private readonly string _dbPath;

        public DuckDbTelemetryService(string dbPath = "orion_telemetry.db")
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
                CREATE TABLE IF NOT EXISTS logs (
                    id UUID PRIMARY KEY,
                    app_id UUID,
                    deployment_id UUID,
                    owner_id TEXT,
                    message TEXT,
                    level TEXT,
                    timestamp TIMESTAMP
                );

                CREATE TABLE IF NOT EXISTS metrics (
                    id UUID PRIMARY KEY,
                    app_id UUID,
                    owner_id TEXT,
                    cpu_usage DOUBLE,
                    memory_usage_mb INTEGER,
                    timestamp TIMESTAMP
                );
            ";
            await cmd.ExecuteNonQueryAsync();

            // Migration for owner_id
            try {
                cmd.CommandText = "ALTER TABLE logs ADD COLUMN owner_id TEXT DEFAULT ''";
                await cmd.ExecuteNonQueryAsync();
                cmd.CommandText = "ALTER TABLE metrics ADD COLUMN owner_id TEXT DEFAULT ''";
                await cmd.ExecuteNonQueryAsync();
            } catch { /* Column might already exist */ }
        }

        public async Task CreateLogAsync(LogEntry log)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO logs (id, app_id, deployment_id, owner_id, message, level, timestamp) VALUES (?, ?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(log.Id));
            cmd.Parameters.Add(new DuckDBParameter(log.AppId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(log.DeploymentId ?? (object)DBNull.Value));
            cmd.Parameters.Add(new DuckDBParameter(log.OwnerId));
            cmd.Parameters.Add(new DuckDBParameter(log.Message));
            cmd.Parameters.Add(new DuckDBParameter(log.Level));
            cmd.Parameters.Add(new DuckDBParameter(log.Timestamp));
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<LogEntry>> GetLogsAsync(Guid appId, Guid? deploymentId = null, string? userId = null)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            var sql = "SELECT id, app_id, deployment_id, owner_id, message, level, timestamp FROM logs WHERE ";
            
            if (deploymentId.HasValue)
            {
                sql += "deployment_id = ?";
                cmd.Parameters.Add(new DuckDBParameter(deploymentId.Value));
            }
            else
            {
                sql += "app_id = ?";
                cmd.Parameters.Add(new DuckDBParameter(appId));
            }

            if (!string.IsNullOrEmpty(userId))
            {
                sql += " AND owner_id = ?";
                cmd.Parameters.Add(new DuckDBParameter(userId));
            }

            cmd.CommandText = sql + " ORDER BY timestamp ASC";

            using var reader = await cmd.ExecuteReaderAsync();
            var logs = new List<LogEntry>();
            while (await reader.ReadAsync())
            {
                logs.Add(new LogEntry
                {
                    Id = reader.GetGuid(0),
                    AppId = reader.IsDBNull(1) ? null : reader.GetGuid(1),
                    DeploymentId = reader.IsDBNull(2) ? null : reader.GetGuid(2),
                    OwnerId = reader.IsDBNull(3) ? "" : reader.GetString(3),
                    Message = reader.GetString(4),
                    Level = reader.GetString(5),
                    Timestamp = reader.GetDateTime(6)
                });
            }
            return logs;
        }
        public async Task CreateMetricAsync(Guid appId, string? userId, double cpu, int memory)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO metrics (id, app_id, owner_id, cpu_usage, memory_usage_mb, timestamp) VALUES (?, ?, ?, ?, ?, ?)";
            cmd.Parameters.Add(new DuckDBParameter(Guid.NewGuid()));
            cmd.Parameters.Add(new DuckDBParameter(appId));
            cmd.Parameters.Add(new DuckDBParameter(userId ?? ""));
            cmd.Parameters.Add(new DuckDBParameter(cpu));
            cmd.Parameters.Add(new DuckDBParameter(memory));
            cmd.Parameters.Add(new DuckDBParameter(DateTime.UtcNow));
            
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<IEnumerable<dynamic>> GetMetricsAsync(Guid appId, string? userId = null, int limit = 100)
        {
            using var connection = new DuckDBConnection(_connectionString);
            await connection.OpenAsync();

            using var cmd = connection.CreateCommand();
            var sql = "SELECT timestamp, cpu_usage, memory_usage_mb FROM metrics WHERE app_id = ?";
            cmd.Parameters.Add(new DuckDBParameter(appId));

            if (!string.IsNullOrEmpty(userId))
            {
                sql += " AND owner_id = ?";
                cmd.Parameters.Add(new DuckDBParameter(userId));
            }

            cmd.CommandText = sql + $" ORDER BY timestamp DESC LIMIT {limit}";

            using var reader = await cmd.ExecuteReaderAsync();
            var metrics = new List<dynamic>();
            while (await reader.ReadAsync())
            {
                metrics.Add(new
                {
                    timestamp = reader.GetDateTime(0),
                    cpu = reader.GetDouble(1),
                    memory = reader.GetInt32(2)
                });
            }
            // Reverse so it's chronological for the graph
            metrics.Reverse();
            return metrics;
        }
    }
}
