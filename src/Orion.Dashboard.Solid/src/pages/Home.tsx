import { createResource, For, Show, Component } from 'solid-js';
import { A, useNavigate } from '@solidjs/router';
import { api } from '../services/api';
import { OrionBadge } from '../components/UI';

const Home: Component = () => {
    const [summary] = createResource(api.getSummary);
    const navigate = useNavigate();

    return (
        <div class="animate-in fade-in slide-in-from-bottom duration-400 text-gray-900 dark:text-gray-100 transition-colors duration-300">
            <header class="mb-10">
                <h1 class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white mb-2">Control Plane</h1>
                <div class="flex items-center gap-4 text-xs font-medium">
                    <span class="text-gray-500 dark:text-gray-400">Infrastructure Health:</span>
                    <span class={`${summary()?.pilotStatus === 'Online' ? 'text-emerald-600 dark:text-emerald-400' : 'text-amber-600 dark:text-amber-400'} flex items-center gap-1.5 leading-none font-semibold transition-colors`}>
                        <div class={`w-2 h-2 ${summary()?.pilotStatus === 'Online' ? 'bg-emerald-500' : 'bg-amber-500'} rounded-full shadow-sm shadow-emerald-200 dark:shadow-emerald-900/40`}></div>
                        {summary()?.pilotStatus === 'Online' ? 'Optimal' : summary()?.pilotStatus || 'Syncing...'}
                    </span>
                    <span class="w-px h-3 bg-gray-200 dark:bg-gray-800"></span>
                    <span class="text-gray-500 dark:text-gray-400">Network Matrix:</span>
                    <span class="text-blue-600 dark:text-blue-400 font-semibold">{summary() ? 'Provisioned' : 'Initializing...'}</span>
                </div>
            </header>

            <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-12">
                <StatCard label="Total Applications" value={summary()?.totalApps ?? 0} />
                <StatCard label="Active Nodes" value={summary()?.connectedPeers ?? 0} />
                <StatCard label="Pilot Gateway" value={summary()?.pilotStatus ?? '...'} />
                <StatCard label="Secure Mesh" value={summary() ? 'mTLS Active' : '...'} />
            </section>

            <section>
                <div class="flex justify-between items-end mb-6">
                    <h2 class="text-xl font-bold text-gray-900 dark:text-white">Active Deployments</h2>
                    <A href="/apps" class="text-xs font-bold text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 transition-colors uppercase tracking-wider">View All Applications</A>
                </div>

                <div class="professional-card !p-0 overflow-hidden">
                    <table class="w-full text-left text-sm border-collapse">
                        <thead class="bg-gray-50 dark:bg-zinc-900/50 border-b border-gray-100 dark:border-gray-800">
                            <tr>
                                <th class="px-6 py-4 font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest text-[10px]">Application</th>
                                <th class="px-6 py-4 font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest text-[10px]">Status</th>
                                <th class="px-6 py-4 font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest text-[10px]">Resource Allocation</th>
                                <th class="px-6 py-4 font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest text-[10px]">Instance Scale</th>
                            </tr>
                        </thead>
                        <tbody class="divide-y divide-gray-50 dark:divide-gray-800/50">
                            <Show when={!summary.loading} fallback={
                                <tr><td colspan="4" class="p-12 text-center text-gray-400 dark:text-gray-600 font-medium italic">Synchronizing telemetry data...</td></tr>
                            }>
                                <For each={summary()?.apps}>
                                    {(app) => (
                                        <tr 
                                            class="hover:bg-gray-50 dark:hover:bg-zinc-900/30 transition-colors group cursor-pointer"
                                            onClick={() => navigate(`/apps/${app.id}`)}
                                        >
                                            <td class="px-6 py-4">
                                                <div class="font-bold text-gray-900 dark:text-gray-100 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">{app.name}</div>
                                                <div class="text-[11px] font-mono text-gray-400 dark:text-gray-500 mt-0.5 tracking-tight">{app.id.substring(0, 12)}</div>
                                            </td>
                                            <td class="px-6 py-4">
                                                <OrionBadge status={app.status === 'Running' ? 'success' : 'default'}>
                                                    {app.status}
                                                </OrionBadge>
                                            </td>
                                            <td class="px-6 py-4">
                                                <div class="flex items-center gap-6">
                                                    <div class="flex flex-col">
                                                        <span class="text-[10px] text-gray-400 dark:text-gray-500 uppercase font-bold tracking-tight">CPU</span>
                                                        <span class="text-sm text-blue-600 dark:text-blue-400 font-bold">{app.cpuUsage.toFixed(1)}%</span>
                                                    </div>
                                                    <div class="flex flex-col">
                                                        <span class="text-[10px] text-gray-400 dark:text-gray-500 uppercase font-bold tracking-tight">Memory</span>
                                                        <span class="text-sm text-gray-600 dark:text-gray-300 font-bold">{app.memoryUsageMb}MB</span>
                                                    </div>
                                                </div>
                                            </td>
                                            <td class="px-6 py-4">
                                                <div class="flex items-center gap-2">
                                                    <div class="text-lg font-bold text-blue-600 dark:text-blue-400">{app.activeReplicas}</div>
                                                    <span class="text-[10px] text-gray-400 dark:text-gray-500 uppercase font-bold mt-1 tracking-tight">Nodes</span>
                                                </div>
                                            </td>
                                        </tr>
                                    )}
                                </For>
                            </Show>
                        </tbody>
                    </table>
                </div>
            </section>
        </div>
    );
};

const StatCard: Component<{ label: string; value: string | number }> = (props) => {
    return (
        <div class="professional-card !p-6 flex flex-col justify-between min-h-[140px] hover:scale-[1.02] transition-transform">
            <div class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest leading-none">{props.label}</div>
            <div class="text-4xl font-bold text-gray-900 dark:text-white tracking-tighter">
                {props.value}
            </div>
        </div>
    );
};

export default Home;
