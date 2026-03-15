using Grpc.Net.Client;
using Orion.Core.Grpc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Orion.Core.Services
{
    public interface INodeServiceClient
    {
        Task<JoinResponse> JoinAsync(string masterUrl, JoinRequest request);
        Task SendHeartbeatAsync(string masterUrl, string nodeId);
        Task<WorkloadResponse> StartWorkloadAsync(string nodeUrl, WorkloadRequest request);
        Task<WorkloadResponse> StopWorkloadAsync(string nodeUrl, StopWorkloadRequest request);
    }

    public class NodeServiceClient : INodeServiceClient
    {
        private readonly ILogger<NodeServiceClient> _logger;

        public NodeServiceClient(ILogger<NodeServiceClient> logger)
        {
            _logger = logger;
        }

        public async Task<JoinResponse> JoinAsync(string masterUrl, JoinRequest request)
        {
            using var channel = GrpcChannel.ForAddress(masterUrl);
            var client = new NodeService.NodeServiceClient(channel);
            
            _logger.LogInformation($"[GRPC-CLIENT] Attempting to join cluster at {masterUrl}");
            return await client.JoinAsync(request);
        }

        public async Task SendHeartbeatAsync(string masterUrl, string nodeId)
        {
            using var channel = GrpcChannel.ForAddress(masterUrl);
            var client = new NodeService.NodeServiceClient(channel);
            
            await client.HeartbeatAsync(new HeartbeatRequest { NodeId = nodeId });
        }

        public async Task<WorkloadResponse> StartWorkloadAsync(string nodeUrl, WorkloadRequest request)
        {
            using var channel = GrpcChannel.ForAddress(nodeUrl);
            var client = new NodeService.NodeServiceClient(channel);
            
            _logger.LogInformation($"[GRPC-CLIENT] Starting remote workload on {nodeUrl} for app {request.AppId}");
            return await client.StartWorkloadAsync(request);
        }

        public async Task<WorkloadResponse> StopWorkloadAsync(string nodeUrl, StopWorkloadRequest request)
        {
            using var channel = GrpcChannel.ForAddress(nodeUrl);
            var client = new NodeService.NodeServiceClient(channel);
            
            _logger.LogInformation($"[GRPC-CLIENT] Stopping remote workload on {nodeUrl} (Container: {request.ContainerName})");
            return await client.StopWorkloadAsync(request);
        }
    }
}
