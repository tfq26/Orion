using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Orion.Core.Services
{
    public interface IPilotService
    {
        Task<PilotReport> AnalyzeHealthAsync();
        Task<bool> TriggerRecoveryAsync(string reason, Func<Task<bool>> recoveryAction);
    }

    public class PilotReport
    {
        public bool IsHealthy { get; set; }
        public List<string> Issues { get; set; } = new();
        public List<string> ActionsTaken { get; set; } = new();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string PilotStatus { get; set; } = "Observing";
    }
}
