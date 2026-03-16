import { createMemo, createResource, createSignal, createEffect, For, Show, type Component } from 'solid-js';
import { api } from '../services/api';

type TopologyNodeType = 'gateway' | 'control' | 'service' | 'storage' | 'telemetry' | 'mesh' | 'build';

interface TopologyNode {
  id: string;
  label: string;
  type: TopologyNodeType;
  x: number;
  y: number;
  width: number;
  height: number;
  status: string;
  meta: string;
  details: string[];
  accent: string;
}

interface TopologyEdge {
  id: string;
  from: string;
  to: string;
  label: string;
  tone: 'neutral' | 'active' | 'warn';
}

const Infrastructure: Component = () => {
  const [summary] = createResource(api.getSummary);
  const [nodeTelemetry] = createResource(api.getNodeTelemetry);
  const [selectedNodeId, setSelectedNodeId] = createSignal<string>('gateway');
  const [selectedAppId, setSelectedAppId] = createSignal<string | null>(null);

  // Initialize selectedAppId when data is loaded
  createEffect(() => {
     const apps = summary()?.apps ?? [];
     if (apps.length > 0 && !selectedAppId()) {
         setSelectedAppId(apps[0].id);
     }
  });

  const topology = createMemo(() => {
    const data = summary();
    const telemetry = nodeTelemetry();

    const allApps = data?.apps ?? [];
    const selectedApp = allApps.find(a => a.id === selectedAppId());
    
    const colCenter = 550;
    const baseY = 300;

    const nodes: TopologyNode[] = [];
    const edges: TopologyEdge[] = [];

    if (selectedApp) {
      const app = selectedApp;
      const serviceId = `service-${app.id}`;
      const buildId = `build-${app.id}`;
      
      nodes.push({
        id: serviceId,
        label: app.name,
        type: 'service',
        x: colCenter,
        y: baseY,
        width: 220,
        height: 72,
        status: app.status,
        meta: 'Cluster Service',
        details: [
          `Latest build: ${app.latestBuildStatus}`,
          `Stability: ${app.stability}`,
          `CPU ${Math.round(app.cpuUsage)}% / Memory ${(app.memoryUsageMb / 1024).toFixed(1)} GB`
        ],
        accent: app.status === 'Running' ? 'emerald' : app.status === 'Failed' ? 'red' : 'blue'
      });

      nodes.push({
        id: buildId,
        label: `${app.name} Deployment`,
        type: 'build',
        x: colCenter,
        y: baseY + 180,
        width: 220,
        height: 72,
        status: app.latestBuildStatus,
        meta: 'Build Lane',
        details: [
          'Automatic CI/CD deployment pipeline active.',
          app.latestBuildAt ? `Synced at ${formatTimestamp(app.latestBuildAt)}.` : 'No sync history.',
          `Health: ${app.latestBuildStatus}`
        ],
        accent: app.latestBuildStatus === 'Failed' ? 'red' : 'blue'
      });

      edges.push({ 
        id: `${buildId}-service`, 
        from: buildId, 
        to: serviceId, 
        label: 'Automatic Release', 
        tone: app.latestBuildStatus === 'Failed' ? 'warn' : 'active' 
      });
    }

    return { nodes, edges };
  });

  const selectedNode = createMemo(() => topology().nodes.find((node) => node.id === selectedNodeId()) ?? topology().nodes[0]);

  return (
    <div class="animate-in fade-in duration-500 text-gray-900 dark:text-gray-100 transition-colors duration-300">
      <header class="mb-8">
        <div class="flex items-end justify-between gap-6">
          <div>
            <h1 class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white">Infrastructure</h1>
            <p class="mt-2 max-w-3xl text-sm text-gray-500 dark:text-gray-400">
              Visualize how ingress, services, builds, storage, telemetry, and the secure mesh connect across the current Orion project.
            </p>
          </div>
          <div class="flex items-center gap-3">
            <div class="relative">
              <select 
                class="appearance-none rounded-sm border border-gray-200 bg-white px-4 py-2 pr-10 text-xs font-bold uppercase tracking-wider text-gray-700 focus:outline-none focus:ring-2 focus:ring-blue-500/20 dark:border-gray-800 dark:bg-zinc-950 dark:text-gray-300"
                value={selectedAppId() ?? ''}
                onChange={(e) => setSelectedAppId(e.currentTarget.value)}
              >
                <For each={summary()?.apps ?? []}>
                  {(app) => <option value={app.id}>{app.name}</option>}
                </For>
              </select>
              <div class="pointer-events-none absolute inset-y-0 right-0 flex items-center px-2 text-gray-400">
                <svg class="h-4 w-4 fill-current" viewBox="0 0 20 20">
                  <path d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z" />
                </svg>
              </div>
            </div>
            <span class="rounded-full border border-blue-200 bg-blue-50 px-3 py-2 text-[10px] font-black uppercase tracking-[0.18em] text-blue-600 dark:border-blue-900/60 dark:bg-blue-950/30 dark:text-blue-400">
              Isolated View
            </span>
          </div>
        </div>
      </header>

      <section class="w-full">
        <div class="professional-card !p-0 overflow-hidden w-full">
          <div class="flex items-center justify-between border-b border-gray-100 px-6 py-4 dark:border-gray-800">
            <div>
              <div class="text-[10px] font-bold uppercase tracking-[0.22em] text-gray-400 dark:text-gray-500">Topology Canvas</div>
              <div class="mt-2 text-sm text-gray-500 dark:text-gray-400">Railway-style dependency view of the running project.</div>
            </div>
            <div class="flex items-center gap-2 text-[11px] font-semibold text-gray-500 dark:text-gray-400">
              <LegendDot tone="active" /> Live path
              <LegendDot tone="neutral" /> Dependency
              <LegendDot tone="warn" /> Attention
            </div>
          </div>

          <Show when={!summary.loading} fallback={
            <div class="h-[720px] animate-pulse bg-gray-50/50 dark:bg-zinc-900/40"></div>
          }>
            <div class="relative h-[820px] overflow-hidden bg-[radial-gradient(circle_at_top_left,_rgba(56,189,248,0.09),_transparent_26%),radial-gradient(circle_at_bottom_right,_rgba(59,130,246,0.08),_transparent_30%)] dark:bg-[radial-gradient(circle_at_top_left,_rgba(34,211,238,0.1),_transparent_24%),radial-gradient(circle_at_bottom_right,_rgba(37,99,235,0.12),_transparent_28%),#050505]">
              <div class="absolute inset-0 opacity-60 dark:opacity-20" style="background-image: linear-gradient(rgba(148,163,184,0.12) 1px, transparent 1px), linear-gradient(90deg, rgba(148,163,184,0.12) 1px, transparent 1px); background-size: 36px 36px;"></div>

              <svg class="absolute inset-0 h-full w-full px-20">
                <defs>
                  <filter id="edgeGlow">
                    <feGaussianBlur stdDeviation="2" result="coloredBlur" />
                    <feMerge>
                      <feMergeNode in="coloredBlur" />
                      <feMergeNode in="SourceGraphic" />
                    </feMerge>
                  </filter>
                  <marker
                    id="arrowhead"
                    markerWidth="10"
                    markerHeight="7"
                    refX="9"
                    refY="3.5"
                    orient="auto"
                  >
                    <polygon points="0 0, 10 3.5, 0 7" fill="currentColor" class="text-cyan-400 dark:text-cyan-300" />
                  </marker>
                </defs>
                <For each={topology().edges}>
                  {(edge) => {
                    const from = topology().nodes.find((node) => node.id === edge.from);
                    const to = topology().nodes.find((node) => node.id === edge.to);
                    if (!from || !to) return null;

                    const startX = from.x + from.width / 2;
                    const endX = to.x + to.width / 2;
                    
                    // Adjust vertical positions to sit at node borders
                    const isFromAbove = from.y < to.y;
                    const startY = isFromAbove ? from.y + from.height : from.y;
                    const endY = isFromAbove ? to.y - 10 : to.y + to.height + 10;
                    
                    const midX = (startX + endX) / 2;
                    const midY = (startY + endY) / 2;
                    const path = `M ${startX} ${startY} C ${startX} ${midY}, ${endX} ${midY}, ${endX} ${endY}`;
                    
                    const tone = edge.tone === 'active'
                      ? 'stroke-cyan-400/70 dark:stroke-cyan-300/60 transition-colors duration-500'
                      : edge.tone === 'warn'
                        ? 'stroke-amber-400/70 dark:stroke-amber-300/60'
                        : 'stroke-slate-300/70 dark:stroke-slate-700/70';

                    return (
                      <g>
                        <path
                          d={path}
                          fill="none"
                          class={tone}
                          stroke-width="2.2"
                          stroke-dasharray={edge.tone === 'active' ? '0' : '7 7'}
                          filter="url(#edgeGlow)"
                          marker-end="url(#arrowhead)"
                        />
                        {/* Edge labels removed for cleaner aesthetic */}
                      </g>
                    );
                  }}
                </For>
              </svg>

              <For each={topology().nodes}>
                {(node) => (
                  <button
                    class={`absolute group rounded-sm border px-4 py-3 text-left transition-all duration-300 hover:shadow-xl dark:hover:border-blue-500/40 ${
                      selectedNodeId() === node.id
                        ? 'border-blue-400/50 bg-white shadow-lg dark:border-blue-500/40 dark:bg-zinc-950 shadow-blue-500/5'
                        : 'border-gray-200/80 bg-white/95 dark:border-gray-800/80 dark:bg-zinc-950/95'
                    }`}
                    style={{ left: `${node.x}px`, top: `${node.y}px`, width: `${node.width}px`, height: `${node.height}px` }}
                    onClick={() => setSelectedNodeId(node.id)}
                  >
                    <div class="absolute left-0 top-0 bottom-0 w-1 opacity-20 transition-opacity group-hover:opacity-100" classList={{
                      'bg-blue-500': node.accent === 'blue',
                      'bg-amber-500': node.accent === 'amber',
                      'bg-emerald-500': node.accent === 'emerald',
                      'bg-violet-500': node.accent === 'violet',
                      'bg-red-500': node.accent === 'red'
                    }}></div>

                    <div class="flex h-full items-center justify-between gap-3">
                      <div class="overflow-hidden">
                        <div class="text-[9px] font-black uppercase tracking-[0.25em] text-gray-400 dark:text-gray-500 truncate transition-colors group-hover:text-gray-500 dark:group-hover:text-gray-400">{node.meta}</div>
                        <div class="mt-1.5 text-[14px] font-bold text-gray-900 dark:text-gray-100 truncate tracking-tight">{node.label}</div>
                      </div>
                      
                      {/* Arrow button removed */}
                    </div>
                  </button>
                )}
              </For>
            </div>
          </Show>
        </div>
      </section>
    </div>
  );
};

const SnapshotRow: Component<{ label: string; value: string }> = (props) => (
  <div class="flex items-center justify-between rounded-sm border border-gray-100 bg-gray-50 px-4 py-3 dark:border-gray-800 dark:bg-zinc-900">
    <span class="text-sm text-gray-500 dark:text-gray-400">{props.label}</span>
    <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">{props.value}</span>
  </div>
);

const LegendDot: Component<{ tone: 'neutral' | 'active' | 'warn' }> = (props) => (
  <span class={`inline-block h-2.5 w-2.5 rounded-full ${
    props.tone === 'active'
      ? 'bg-cyan-400'
      : props.tone === 'warn'
        ? 'bg-amber-400'
        : 'bg-slate-400'
  }`}></span>
);

const NodeTypeBadge: Component<{ type: TopologyNodeType }> = (props) => (
  <span class="rounded-full border border-gray-200 px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.18em] text-gray-500 dark:border-gray-800 dark:text-gray-400">
    {props.type}
  </span>
);

const StatusPill: Component<{ status: string }> = (props) => {
  const tone = () => {
    switch (props.status) {
      case 'Running':
      case 'Routing Live':
      case 'Available':
      case 'Collecting':
      case 'mTLS Active':
        return 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300';
      case 'Failed':
        return 'bg-red-100 text-red-700 dark:bg-red-950/40 dark:text-red-300';
      case 'Building':
      case 'Deploying':
      case 'Pending':
      case 'Observing':
      case 'Syncing':
      case 'Provisioning':
        return 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300';
      default:
        return 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300';
    }
  };

  return (
    <span class={`inline-flex rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.18em] ${tone()}`}>
      {props.status}
    </span>
  );
};

function formatTimestamp(value: string) {
  return new Date(value).toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}

function formatStorageValue(valueGb: number) {
  if (valueGb >= 1) {
    return `${valueGb.toFixed(2)} GB`;
  }

  return `${Math.round(valueGb * 1024)} MB`;
}

export default Infrastructure;
