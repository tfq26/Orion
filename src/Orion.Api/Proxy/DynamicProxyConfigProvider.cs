using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Primitives;
using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.LoadBalancing;
using Orion.Core.Services;

namespace Orion.Api.Proxy
{
    public class DynamicProxyConfigProvider : IProxyConfigProvider
    {
        private volatile CustomMemoryConfig _config;

        public DynamicProxyConfigProvider()
        {
            _config = new CustomMemoryConfig(new List<RouteConfig>(), new List<ClusterConfig>());
        }

        public IProxyConfig GetConfig() => _config;

        public void Update(IEnumerable<Orion.Core.Models.App> apps, IEnumerable<Orion.Core.Models.Instance> instances)
        {
            var routes = new List<RouteConfig>();
            var clusters = new List<ClusterConfig>();

            foreach (var app in apps)
            {
                var appInstances = instances.Where(i => i.AppId == app.Id).ToList();

                if (appInstances.Any())
                {
                    var clusterId = $"cluster-{app.Name.ToLower()}";
                    var routeId = $"route-{app.Name.ToLower()}";
                    Console.WriteLine($"[PROXY] Mapping {app.Name} to {appInstances.Count} instances");

                    routes.Add(new RouteConfig
                    {
                        RouteId = routeId,
                        ClusterId = clusterId,
                        Match = new RouteMatch
                        {
                            Path = $"/proxy/{app.Name.ToLower()}/{{**remainder}}"
                        },
                        Transforms = new[]
                        {
                            new Dictionary<string, string> {{ "PathRemovePrefix", $"/proxy/{app.Name.ToLower()}" }}
                        }
                    });

                    routes.Add(new RouteConfig
                    {
                        RouteId = routeId + "-base",
                        ClusterId = clusterId,
                        Match = new RouteMatch
                        {
                            Path = $"/proxy/{app.Name.ToLower()}"
                        },
                        Transforms = new[]
                        {
                            new Dictionary<string, string> {{ "PathRemovePrefix", $"/proxy/{app.Name.ToLower()}" }}
                        }
                    });

                    // Host-based routing (e.g., myapp.lvh.me:5000)
                    routes.Add(new RouteConfig
                    {
                        RouteId = routeId + "-host",
                        ClusterId = clusterId,
                        Match = new RouteMatch
                        {
                            Hosts = new[] { $"{app.Name.ToLower()}.lvh.me", $"{app.Name.ToLower()}.orion.local" },
                            Path = "{**remainder}"
                        }
                    });

                    var destinations = new Dictionary<string, DestinationConfig>();
                    for (int i = 0; i < appInstances.Count; i++)
                    {
                        destinations[$"dest-{i}"] = new DestinationConfig 
                        { 
                            Address = $"http://localhost:{appInstances[i].Port}" 
                        };
                    }

                    clusters.Add(new ClusterConfig
                    {
                        ClusterId = clusterId,
                        LoadBalancingPolicy = LoadBalancingPolicies.RoundRobin,
                        Destinations = destinations
                    });
                }
            }

            var oldConfig = _config;
            _config = new CustomMemoryConfig(routes, clusters);
            oldConfig.SignalChange();
        }

        private class CustomMemoryConfig : IProxyConfig
        {
            private readonly CancellationTokenSource _cts = new CancellationTokenSource();

            public CustomMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
            {
                Routes = routes;
                Clusters = clusters;
                ChangeToken = new CancellationChangeToken(_cts.Token);
            }

            public IReadOnlyList<RouteConfig> Routes { get; }
            public IReadOnlyList<ClusterConfig> Clusters { get; }
            public IChangeToken ChangeToken { get; }

            public void SignalChange() => _cts.Cancel();
        }
    }
}
