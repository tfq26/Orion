using System;
using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Orion.Core.Models
{
    public class OrionManifest
    {
        [YamlMember(Alias = "version")]
        public string Version { get; set; } = "1.0";

        [YamlMember(Alias = "workloads")]
        public List<WorkloadDefinition> Workloads { get; set; } = new();
    }

    public class WorkloadDefinition
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "wasm")]
        public string Wasm { get; set; } = string.Empty;

        [YamlMember(Alias = "replicas")]
        public int Replicas { get; set; } = 1;

        [YamlMember(Alias = "env")]
        public Dictionary<string, string> Env { get; set; } = new();

        [YamlMember(Alias = "resources")]
        public ResourceRequirement Resources { get; set; } = new();

        [YamlMember(Alias = "routes")]
        public List<RouteDefinition> Routes { get; set; } = new();
    }

    public class ResourceRequirement
    {
        [YamlMember(Alias = "cpu")]
        public int CpuCores { get; set; } = 1;

        [YamlMember(Alias = "memory")]
        public int MemoryMb { get; set; } = 512;
    }

    public class RouteDefinition
    {
        [YamlMember(Alias = "host")]
        public string Host { get; set; } = string.Empty;

        [YamlMember(Alias = "path")]
        public string Path { get; set; } = "/";
    }
}
