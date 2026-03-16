using DuckDB.NET.Data;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface INodeTelemetryHistoryStore
    {
        Task InitializeAsync();
        Task AppendRawSampleAsync(string nodeName, string architecture, NodeTelemetrySample sample);
        Task RollupAsync(DateTime utcNow);
        Task<List<NodeTelemetrySample>> GetRecentRawSamplesAsync(int limit);
        Task<List<NodeTelemetrySample>> GetHourlySamplesAsync(int limit);
        Task<List<NodeTelemetrySample>> GetDailySamplesAsync(int limit);
    }

    public class NodeTelemetryHistoryStore : INodeTelemetryHistoryStore
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _gate = new(1, 1);

        public NodeTelemetryHistoryStore(string dbPath = "orion_telemetry.db")
        {
            _connectionString = $"Data Source={dbPath}";
        }

        public async Task InitializeAsync()
        {
            await _gate.WaitAsync();
            try
            {
                using var connection = new DuckDBConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    CREATE TABLE IF NOT EXISTS node_metrics_raw (
                        id UUID PRIMARY KEY,
                        node_name TEXT,
                        architecture TEXT,
                        timestamp TIMESTAMP,
                        cpu_usage DOUBLE,
                        memory_usage_percent DOUBLE,
                        memory_usage_gb DOUBLE,
                        storage_usage_percent DOUBLE,
                        storage_usage_gb DOUBLE,
                        network_traffic_mbps DOUBLE
                    );

                    CREATE TABLE IF NOT EXISTS node_metrics_hourly (
                        bucket_start TIMESTAMP PRIMARY KEY,
                        node_name TEXT,
                        architecture TEXT,
                        cpu_usage DOUBLE,
                        memory_usage_percent DOUBLE,
                        memory_usage_gb DOUBLE,
                        storage_usage_percent DOUBLE,
                        storage_usage_gb DOUBLE,
                        network_traffic_mbps DOUBLE
                    );

                    CREATE TABLE IF NOT EXISTS node_metrics_daily (
                        bucket_start TIMESTAMP PRIMARY KEY,
                        node_name TEXT,
                        architecture TEXT,
                        cpu_usage DOUBLE,
                        memory_usage_percent DOUBLE,
                        memory_usage_gb DOUBLE,
                        storage_usage_percent DOUBLE,
                        storage_usage_gb DOUBLE,
                        network_traffic_mbps DOUBLE
                    );
                ";

                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task AppendRawSampleAsync(string nodeName, string architecture, NodeTelemetrySample sample)
        {
            await _gate.WaitAsync();
            try
            {
                using var connection = new DuckDBConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = @"
                    INSERT INTO node_metrics_raw (
                        id, node_name, architecture, timestamp, cpu_usage, memory_usage_percent,
                        memory_usage_gb, storage_usage_percent, storage_usage_gb, network_traffic_mbps
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
                ";
                cmd.Parameters.Add(new DuckDBParameter(Guid.NewGuid()));
                cmd.Parameters.Add(new DuckDBParameter(nodeName));
                cmd.Parameters.Add(new DuckDBParameter(architecture));
                cmd.Parameters.Add(new DuckDBParameter(sample.Timestamp));
                cmd.Parameters.Add(new DuckDBParameter(sample.CpuUsage));
                cmd.Parameters.Add(new DuckDBParameter(sample.MemoryUsagePercent));
                cmd.Parameters.Add(new DuckDBParameter(sample.MemoryUsageGb));
                cmd.Parameters.Add(new DuckDBParameter(sample.StorageUsagePercent));
                cmd.Parameters.Add(new DuckDBParameter(sample.StorageUsageGb));
                cmd.Parameters.Add(new DuckDBParameter(sample.NetworkTrafficMbps));
                await cmd.ExecuteNonQueryAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        public async Task RollupAsync(DateTime utcNow)
        {
            await _gate.WaitAsync();
            try
            {
                var startOfToday = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 0, 0, 0, DateTimeKind.Utc);
                var startOfCurrentHour = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, utcNow.Hour, 0, 0, DateTimeKind.Utc);
                var startOfWeek = StartOfWeekUtc(utcNow);

                using var connection = new DuckDBConnection(_connectionString);
                await connection.OpenAsync();

                using var rollupHourly = connection.CreateCommand();
                rollupHourly.CommandText = @"
                    INSERT INTO node_metrics_hourly
                    SELECT
                        date_trunc('hour', timestamp) AS bucket_start,
                        any_value(node_name),
                        any_value(architecture),
                        avg(cpu_usage),
                        avg(memory_usage_percent),
                        avg(memory_usage_gb),
                        avg(storage_usage_percent),
                        avg(storage_usage_gb),
                        avg(network_traffic_mbps)
                    FROM node_metrics_raw
                    WHERE timestamp < ?
                    GROUP BY 1
                    ON CONFLICT DO NOTHING
                ";
                rollupHourly.Parameters.Add(new DuckDBParameter(startOfToday));
                await rollupHourly.ExecuteNonQueryAsync();

                using var deleteRolledRaw = connection.CreateCommand();
                deleteRolledRaw.CommandText = "DELETE FROM node_metrics_raw WHERE timestamp < ?";
                deleteRolledRaw.Parameters.Add(new DuckDBParameter(startOfToday));
                await deleteRolledRaw.ExecuteNonQueryAsync();

                using var currentDayHourly = connection.CreateCommand();
                currentDayHourly.CommandText = @"
                    INSERT INTO node_metrics_hourly
                    SELECT
                        date_trunc('hour', timestamp) AS bucket_start,
                        any_value(node_name),
                        any_value(architecture),
                        avg(cpu_usage),
                        avg(memory_usage_percent),
                        avg(memory_usage_gb),
                        avg(storage_usage_percent),
                        avg(storage_usage_gb),
                        avg(network_traffic_mbps)
                    FROM node_metrics_raw
                    WHERE timestamp >= ? AND timestamp < ?
                    GROUP BY 1
                    ON CONFLICT (bucket_start) DO UPDATE SET
                        node_name = excluded.node_name,
                        architecture = excluded.architecture,
                        cpu_usage = excluded.cpu_usage,
                        memory_usage_percent = excluded.memory_usage_percent,
                        memory_usage_gb = excluded.memory_usage_gb,
                        storage_usage_percent = excluded.storage_usage_percent,
                        storage_usage_gb = excluded.storage_usage_gb,
                        network_traffic_mbps = excluded.network_traffic_mbps
                ";
                currentDayHourly.Parameters.Add(new DuckDBParameter(startOfToday));
                currentDayHourly.Parameters.Add(new DuckDBParameter(startOfCurrentHour));
                await currentDayHourly.ExecuteNonQueryAsync();

                using var rollupDaily = connection.CreateCommand();
                rollupDaily.CommandText = @"
                    INSERT INTO node_metrics_daily
                    SELECT
                        date_trunc('day', bucket_start) AS bucket_start,
                        any_value(node_name),
                        any_value(architecture),
                        avg(cpu_usage),
                        avg(memory_usage_percent),
                        avg(memory_usage_gb),
                        avg(storage_usage_percent),
                        avg(storage_usage_gb),
                        avg(network_traffic_mbps)
                    FROM node_metrics_hourly
                    WHERE bucket_start < ?
                    GROUP BY 1
                    ON CONFLICT DO NOTHING
                ";
                rollupDaily.Parameters.Add(new DuckDBParameter(startOfWeek));
                await rollupDaily.ExecuteNonQueryAsync();

                using var deleteRolledHourly = connection.CreateCommand();
                deleteRolledHourly.CommandText = "DELETE FROM node_metrics_hourly WHERE bucket_start < ?";
                deleteRolledHourly.Parameters.Add(new DuckDBParameter(startOfWeek));
                await deleteRolledHourly.ExecuteNonQueryAsync();
            }
            finally
            {
                _gate.Release();
            }
        }

        public Task<List<NodeTelemetrySample>> GetRecentRawSamplesAsync(int limit)
            => QuerySamplesAsync("SELECT timestamp, cpu_usage, memory_usage_percent, memory_usage_gb, storage_usage_percent, storage_usage_gb, network_traffic_mbps FROM node_metrics_raw ORDER BY timestamp DESC LIMIT ?", limit);

        public Task<List<NodeTelemetrySample>> GetHourlySamplesAsync(int limit)
            => QuerySamplesAsync("SELECT bucket_start, cpu_usage, memory_usage_percent, memory_usage_gb, storage_usage_percent, storage_usage_gb, network_traffic_mbps FROM node_metrics_hourly ORDER BY bucket_start DESC LIMIT ?", limit);

        public Task<List<NodeTelemetrySample>> GetDailySamplesAsync(int limit)
            => QuerySamplesAsync("SELECT bucket_start, cpu_usage, memory_usage_percent, memory_usage_gb, storage_usage_percent, storage_usage_gb, network_traffic_mbps FROM node_metrics_daily ORDER BY bucket_start DESC LIMIT ?", limit);

        private async Task<List<NodeTelemetrySample>> QuerySamplesAsync(string sql, int limit)
        {
            await _gate.WaitAsync();
            try
            {
                using var connection = new DuckDBConnection(_connectionString);
                await connection.OpenAsync();

                using var cmd = connection.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.Add(new DuckDBParameter(limit));

                using var reader = await cmd.ExecuteReaderAsync();
                var samples = new List<NodeTelemetrySample>();
                while (await reader.ReadAsync())
                {
                    samples.Add(new NodeTelemetrySample
                    {
                        Timestamp = reader.GetDateTime(0),
                        CpuUsage = reader.GetDouble(1),
                        MemoryUsagePercent = reader.GetDouble(2),
                        MemoryUsageGb = reader.GetDouble(3),
                        StorageUsagePercent = reader.GetDouble(4),
                        StorageUsageGb = reader.GetDouble(5),
                        NetworkTrafficMbps = reader.GetDouble(6)
                    });
                }

                samples.Reverse();
                return samples;
            }
            finally
            {
                _gate.Release();
            }
        }

        private static DateTime StartOfWeekUtc(DateTime utcNow)
        {
            var date = utcNow.Date;
            var diff = (7 + (date.DayOfWeek - DayOfWeek.Monday)) % 7;
            return date.AddDays(-diff);
        }
    }
}
