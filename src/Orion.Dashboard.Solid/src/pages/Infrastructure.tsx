import { createResource, createSignal, For, Show, onMount, Component } from 'solid-js';
import { api } from '../services/api';

interface Node {
  id: string;
  name: string;
  isControlPlane: boolean;
  x: number;
  y: number;
  cpuUsage: number;
  memoryUsage: number;
}

const Infrastructure: Component = () => {
  const [summary] = createResource(api.getSummary);
  const [nodes, setNodes] = createSignal<Node[]>([]);
  const [selectedNode, setSelectedNode] = createSignal<string | null>(null);

  onMount(() => {
    // Initialize nodes when summary is loaded
    const unsubscribe = summary.loading ? () => { } : () => {
      if (summary()) {
        const data = summary()!;
        const newNodes: Node[] = [];

        // CP Node
        newNodes.push({
          id: 'control-plane',
          name: 'Control Plane',
          isControlPlane: true,
          x: 400,
          y: 300,
          cpuUsage: 5,
          memoryUsage: 2.4
        });

        // App Instance Nodes
        data.apps.forEach((app, appIdx) => {
          for (let i = 0; i < app.activeReplicas; i++) {
            const angle = (2 * Math.PI / (app.activeReplicas * data.apps.length)) * (appIdx * app.activeReplicas + i);
            const radius = 250;
            newNodes.push({
              id: `${app.id}-instance-${i}`,
              name: `${app.name} Instance ${i + 1}`,
              isControlPlane: false,
              x: 400 + radius * Math.cos(angle),
              y: 300 + radius * Math.sin(angle),
              cpuUsage: app.cpuUsage,
              memoryUsage: app.memoryUsageMb / 1024
            });
          }
        });
        setNodes(newNodes);
      }
    };
    unsubscribe();
  });

  return (
    <div class="h-[calc(100vh-2rem)] w-full flex flex-col animate-in fade-in duration-1000 text-gray-900 dark:text-gray-100 transition-colors duration-300">
      <header class="mb-8">
        <h1 class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white mb-2">Network Topology</h1>
        <p class="text-xs font-medium text-gray-400 dark:text-gray-500">Visualizing Wormhole Peering and Instance Distribution mapping across global regions.</p>
      </header>

      <div class="flex-1 bg-white w-full dark:bg-[#050505] border border-gray-200 dark:border-gray-800 relative overflow-hidden group rounded-sm shadow-sm transition-colors duration-300">
        {/* Background Grid Pattern */}
        <div class="absolute inset-0 opacity-[0.4] dark:opacity-[0.1] pointer-events-none" 
             style="background-image: radial-gradient(circle, #cbd5e1 1px, transparent 1px); background-size: 32px 32px;"></div>

        <svg class="absolute inset-0 w-full h-full pointer-events-none">
          <For each={nodes().filter(n => !n.isControlPlane)}>
            {node => (
              <line
                x1="400" y1="300" x2={node.x} y2={node.y}
                stroke="currentColor" 
                class="text-blue-500/20 dark:text-blue-400/10"
                stroke-width="1.5" 
                stroke-dasharray="4 4"
              />
            )}
          </For>
        </svg>

        <For each={nodes()}>
          {node => (
            <div
              class={`
                absolute cursor-pointer transition-all duration-500 p-3 px-4 rounded-xl border-2
                ${node.isControlPlane 
                  ? 'bg-blue-600 border-blue-400 text-white shadow-lg shadow-blue-500/20 z-10' 
                  : 'bg-white dark:bg-zinc-900 border-gray-100 dark:border-gray-800 hover:border-blue-400 dark:hover:border-blue-500 text-gray-900 dark:text-gray-100 shadow-sm'}
                ${selectedNode() === node.id ? 'ring-2 ring-blue-500 dark:ring-blue-400 ring-offset-4 dark:ring-offset-black' : ''}
              `}
              style={{ left: `${node.x}px`, top: `${node.y}px`, transform: 'translate(-50%, -50%)' }}
              onClick={() => setSelectedNode(node.id)}
            >
              <div class="flex items-center gap-3">
                <div class={`w-2 h-2 rounded-full ${node.isControlPlane ? 'bg-white' : 'bg-blue-500'} animate-pulse`}></div>
                <div class="text-[11px] font-bold tracking-tight truncate max-w-[120px]">{node.name}</div>
              </div>
            </div>
          )}
        </For>
      </div>
      <style>{`
        @keyframes dash {
          to { stroke-dashoffset: -20; }
        }
      `}</style>
    </div>
  );
};

export default Infrastructure;
