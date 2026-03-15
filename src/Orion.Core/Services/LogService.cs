using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface ILogService
    {
        Task LogAsync(Guid appId, Guid deploymentId, string ownerId, string message, string level = "Info");
        IEnumerable<LogEntry> GetRecentLogs(Guid resourceId, string? userId = null);
        event EventHandler<LogEntry>? OnLogReceived;
    }

    public class LogService : ILogService
    {
        private readonly ITelemetryService _db;
        private readonly ConcurrentDictionary<Guid, List<LogEntry>> _liveLogs = new();

        public event EventHandler<LogEntry>? OnLogReceived;

        public LogService(ITelemetryService db)
        {
            _db = db;
        }

        public async Task LogAsync(Guid appId, Guid deploymentId, string ownerId, string message, string level = "Info")
        {
            var log = new LogEntry
            {
                AppId = appId,
                DeploymentId = deploymentId,
                OwnerId = ownerId,
                Message = message,
                Level = level,
                Timestamp = DateTime.UtcNow
            };

            // 1. Persist to DuckDB
            await _db.CreateLogAsync(log);

            // 2. Keep in memory for live streams (limited to last 100 per deployment)
            var logs = _liveLogs.GetOrAdd(deploymentId, _ => new List<LogEntry>());
            lock (logs)
            {
                logs.Add(log);
                if (logs.Count > 100) logs.RemoveAt(0);
            }

            // 3. Notify subscribers (SSE)
            OnLogReceived?.Invoke(this, log);
        }

        public IEnumerable<LogEntry> GetRecentLogs(Guid resourceId, string? userId = null)
        {
            if (_liveLogs.TryGetValue(resourceId, out var logs))
            {
                lock (logs)
                {
                    if (string.IsNullOrEmpty(userId)) return new List<LogEntry>(logs);
                    return logs.Where(l => l.OwnerId == userId).ToList();
                }
            }
            return Array.Empty<LogEntry>();
        }
    }
}
