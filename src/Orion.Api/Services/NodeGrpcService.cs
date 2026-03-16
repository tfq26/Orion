using Grpc.Core;
using Orion.Core.Grpc;
using Orion.Core.Services;
using Orion.Core.Models;
using Microsoft.Extensions.Logging;

namespace Orion.Api.Services
{
    public class NodeGrpcService : Orion.Core.Grpc.NodeService.NodeServiceBase
    {
        private readonly ILogger<NodeGrpcService> _logger;
        private readonly ITelemetryService _telemetry;
        private readonly IMetadataService _db;
        private readonly IContainerService _containerService;

        public NodeGrpcService(
            ILogger<NodeGrpcService> logger, 
            ITelemetryService telemetry,
            IMetadataService db,
            IContainerService containerService)
        {
            _logger = logger;
            _telemetry = telemetry;
            _db = db;
            _containerService = containerService;
        }

        public override async Task<JoinResponse> Join(JoinRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"[GRPC] Node {request.NodeName} ({request.NodeId}) is requesting to join. Resources: {request.CpuCores} cores, {request.MemoryMb} MB");
            
            try
            {
                var peer = new Peer
                {
                    Id = Guid.Parse(request.NodeId),
                    Name = request.NodeName,
                    IpAddress = request.IpAddress,
                    Status = "Online",
                    Tags = "worker", // Default to worker
                    LastSeen = DateTime.UtcNow
                };

                await _db.CreatePeerAsync(peer);
                _logger.LogInformation($"[GRPC] Successfully registered peer: {request.NodeName}");

                return new JoinResponse { Success = true, Message = "Welcome to the Orion cluster." };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to register peer");
                return new JoinResponse { Success = false, Message = $"Registration failed: {ex.Message}" };
            }
        }

        public override async Task<WorkloadResponse> StartWorkload(WorkloadRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"[GRPC] Starting workload {request.ContainerName} (Image: {request.ImageTag})");
            
            var result = await _containerService.StartContainerAsync(
                request.ImageTag, 
                request.ContainerName, 
                request.EnvVars, 
                request.CpuCores, 
                request.MemoryMb);

            if (result.Success)
            {
                return new WorkloadResponse { Success = true, Message = "Started", Port = result.Port, ProcessId = result.ProcessId ?? 0 };
            }
            return new WorkloadResponse { Success = false, Message = "Failed to start container" };
        }

        public override async Task<WorkloadResponse> StopWorkload(StopWorkloadRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"[GRPC] Stopping workload {request.ContainerName}");
            var success = await _containerService.StopContainerAsync(request.ContainerName);
            return new WorkloadResponse { Success = success, Message = success ? "Stopped" : "Failed" };
        }

        public override Task<HeartbeatResponse> Heartbeat(HeartbeatRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"[GRPC] Heartbeat received from {request.NodeId}");
            return Task.FromResult(new HeartbeatResponse { Success = true });
        }

        public override async Task<TelemetrySummary> StreamTelemetry(IAsyncStreamReader<TelemetryData> requestStream, ServerCallContext context)
        {
            int count = 0;
            while (await requestStream.MoveNext())
            {
                var data = requestStream.Current;
                // In a real scenario, we'd batch these into the telemetry service
                _logger.LogDebug($"[GRPC] Received telemetry for {data.AppId}: CPU {data.CpuUsage}%");
                count++;
            }

            return new TelemetrySummary { ProcessedCount = count };
        }

        public override async Task<StateResponse> TransferState(StateRequest request, ServerCallContext context)
        {
            _logger.LogInformation($"[GRPC] State transfer requested for {request.Key}");
            
            // This would interact with the storage layer to move state between nodes
            return new StateResponse 
            { 
                Data = Google.Protobuf.ByteString.Empty,
                ContentType = "application/octet-stream"
            };
        }
    }
}
