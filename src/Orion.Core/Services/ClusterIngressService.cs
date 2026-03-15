using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Orion.Core.Models;
using Yarp.ReverseProxy.Configuration;

namespace Orion.Core.Services
{
    public interface IClusterIngressService
    {
        Task<IReadOnlyList<RouteConfig>> GetRoutesAsync();
        Task<IReadOnlyList<ClusterConfig>> GetClustersAsync();
    }

    public class ClusterIngressService : IClusterIngressService
    {
        private readonly IMetadataService _db;
        private readonly ILogger<ClusterIngressService> _logger;

        public ClusterIngressService(IMetadataService db, ILogger<ClusterIngressService> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task<IReadOnlyList<RouteConfig>> GetRoutesAsync()
        {
            var apps = await _db.GetAppsAsync();
            var routes = new List<RouteConfig>();

            foreach (var app in apps)
            {
                var routeId = $"route-{app.Name.ToLower()}";
                var clusterId = $"cluster-{app.Name.ToLower()}";

                // Path-based: /proxy/appname/
                routes.Add(new RouteConfig
                {
                    RouteId = routeId,
                    ClusterId = clusterId,
                    Match = new RouteMatch { Path = $"/proxy/{app.Name.ToLower()}/{{**remainder}}" },
                    Transforms = new[] { new Dictionary<string, string> {{ "PathRemovePrefix", $"/proxy/{app.Name.ToLower()}" }} }
                });

                // Host-based: appname.orion.local
                routes.Add(new RouteConfig
                {
                    RouteId = routeId + "-host",
                    ClusterId = clusterId,
                    Match = new RouteMatch { Hosts = new[] { $"{app.Name.ToLower()}.orion.local" }, Path = "{**remainder}" }
                });
            }

            return routes;
        }

        public async Task<IReadOnlyList<ClusterConfig>> GetClustersAsync()
        {
            var apps = await _db.GetAppsAsync();
            var allInstances = await _db.GetActiveInstancesAsync();
            var peers = await _db.GetPeersAsync();
            var clusters = new List<ClusterConfig>();

            foreach (var app in apps)
            {
                var appInstances = allInstances.Where(i => i.AppId == app.Id);
                var destinations = new Dictionary<string, DestinationConfig>();

                foreach (var inst in appInstances)
                {
                    // Find which node this instance is running on
                    var instanceNode = peers.FirstOrDefault(p => inst.ContainerName.Contains(p.Name.ToLower()));
                    var targetIp = instanceNode?.IpAddress ?? "localhost";
                    
                    destinations.Add($"dest-{inst.Id}", new DestinationConfig
                    {
                        Address = $"http://{targetIp}:{inst.Port}"
                    });
                }

                clusters.Add(new ClusterConfig
                {
                    ClusterId = $"cluster-{app.Name.ToLower()}",
                    Destinations = destinations,
                    LoadBalancingPolicy = "RoundRobin"
                });
            }

            return clusters;
        }
    }
}
