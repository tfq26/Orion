using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface IResourceAdvisorService
    {
        Task<DeploymentAssessmentReport> AssessAsync(App app, string? userId = null);
    }

    public class ResourceAdvisorService : IResourceAdvisorService
    {
        private readonly IMetadataService _metadataService;
        private readonly IMetricsService _metricsService;

        public ResourceAdvisorService(IMetadataService metadataService, IMetricsService metricsService)
        {
            _metadataService = metadataService;
            _metricsService = metricsService;
        }

        public async Task<DeploymentAssessmentReport> AssessAsync(App app, string? userId = null)
        {
            var metrics = await _metricsService.GetMetricsAsync(app.Id);
            var activeInstances = (await _metadataService.GetActiveInstancesAsync(userId))
                .Where(instance => instance.AppId == app.Id)
                .ToList();

            var allocatedCpu = Math.Max(app.RequiredCpuCores ?? 1, 1);
            var allocatedMemory = Math.Max(app.RequiredMemoryMb ?? 256, 256);
            var currentReplicas = activeInstances.Count;

            var report = new DeploymentAssessmentReport
            {
                AppId = app.Id,
                AppName = app.Name,
                CurrentReplicas = currentReplicas,
                RecommendedReplicas = currentReplicas,
                CpuUsage = metrics.CpuUsage,
                MemoryUsageMb = metrics.MemoryUsageMb,
                AllocatedCpuCores = allocatedCpu,
                AllocatedMemoryMb = allocatedMemory
            };

            if (currentReplicas == 0)
            {
                report.Stability = "Idle";
                report.RecommendedAction = "Hold";
                report.Review = "No live replicas are running right now, so there is nothing to scale until traffic or a redeploy resumes the app.";
                report.Findings.Add("The deployment is currently idle.");
                report.Findings.Add("Scale recommendations will become more accurate once replicas are serving traffic.");
                return report;
            }

            var cpuPressure = metrics.CpuUsage >= 80;
            var memoryPressure = metrics.MemoryUsageMb >= (int)Math.Round(allocatedMemory * 0.85);
            var cpuSlack = metrics.CpuUsage <= 25;
            var memorySlack = metrics.MemoryUsageMb <= (int)Math.Round(allocatedMemory * 0.45);

            report.Findings.Add($"Average CPU usage is {metrics.CpuUsage:F1}% per active replica.");
            report.Findings.Add($"Average memory usage is {metrics.MemoryUsageMb} MB against an allocation target of {allocatedMemory} MB.");
            report.Findings.Add($"There are {currentReplicas} active replica(s) handling the current workload.");

            if (cpuPressure || memoryPressure)
            {
                report.Stability = "Constrained";
                report.RecommendedAction = "Scale Up";
                report.RecommendedReplicas = currentReplicas + 1;
                report.Review = "The current deployment is running hot. Add capacity before the workload becomes unstable.";
                if (cpuPressure)
                {
                    report.Findings.Add("CPU pressure is above the comfort threshold.");
                }
                if (memoryPressure)
                {
                    report.Findings.Add("Memory consumption is close to the configured ceiling.");
                }
            }
            else if (cpuSlack && memorySlack && currentReplicas > 1)
            {
                report.Stability = "Overprovisioned";
                report.RecommendedAction = "Scale Down";
                report.RecommendedReplicas = currentReplicas - 1;
                report.Review = "The deployment is comfortably under budget. You can likely remove a replica and keep the same user experience.";
                report.Findings.Add("CPU and memory are both well below their provisioned targets.");
            }
            else
            {
                report.Stability = "Right-Sized";
                report.RecommendedAction = "Hold";
                report.RecommendedReplicas = currentReplicas;
                report.Review = "The current resource footprint looks healthy. Keep the deployment at its current scale unless traffic changes.";
                report.Findings.Add("The deployment is within its expected operating range.");
            }

            return report;
        }
    }
}
