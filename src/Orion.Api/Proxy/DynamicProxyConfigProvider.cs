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

        public void Update(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        {
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
