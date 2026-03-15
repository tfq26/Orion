using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;

namespace Orion.Core.Services
{
    public interface IMeshService
    {
        Task InitializeAsync();
        Task<IEnumerable<Peer>> GetPeersAsync();
        Task<string> GetAuthKeyAsync();
    }

    public class MeshService : IMeshService
    {
        private readonly ILogger<MeshService> _logger;
        private readonly IMetadataService _db;
        private readonly string _headscaleUrl;
        private readonly string _apiKey;

        public MeshService(ILogger<MeshService> logger, IMetadataService db)
        {
            _logger = logger;
            _db = db;
            // In a real prod setup, these would come from env or secrets
            _headscaleUrl = Environment.GetEnvironmentVariable("HEADSCALE_URL") ?? "http://localhost:8080";
            _apiKey = Environment.GetEnvironmentVariable("HEADSCALE_API_KEY") ?? "mock_key";
        }

        public async Task InitializeAsync()
        {
            _logger.LogInformation("MeshService initializing...");
            // In simulation mode, we just check connectivity
            _logger.LogInformation($"[MESH] Connecting to Headscale at {_headscaleUrl}");
            
            // For first boot, register the local node as a seed peer
            var peers = await _db.GetPeersAsync();
            if (!System.Linq.Enumerable.Any(peers))
            {
                var localNode = new Peer
                {
                    Name = "orion-master-01",
                    IpAddress = "100.64.0.1",
                    Status = "Online",
                    Tags = "master, control-plane",
                    LastSeen = DateTime.UtcNow
                };
                await _db.CreatePeerAsync(localNode);
                _logger.LogInformation($"[MESH] Registered local node: {localNode.Name}");
            }
        }

        public async Task<IEnumerable<Peer>> GetPeersAsync()
        {
            return await _db.GetPeersAsync();
        }

        public Task<string> GetAuthKeyAsync()
        {
            // This would call Headscale API to generate a pre-auth key
            return Task.FromResult("mkey:bootstrap-auth-token-simulated");
        }
    }
}
