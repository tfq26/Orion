import { createMemo, createResource, createSignal, For, onCleanup, Show, type Component, onMount, createEffect } from 'solid-js';
import { A, useNavigate } from '@solidjs/router';
import { api, type DeploymentAssessmentReport, type NodeTelemetrySample } from '../services/api';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '../components/ui/table';
import { OrionButton } from '../components/UI';

declare global {
    var Chart: any;
}

const Home: Component = () => {
    const [summary, { refetch }] = createResource(api.getSummary);
    const [nodeTelemetry, { refetch: refetchNodeTelemetry }] = createResource(api.getNodeTelemetry);
    const [menuAppId, setMenuAppId] = createSignal<string | null>(null);
    const [busyAppId, setBusyAppId] = createSignal<string | null>(null);
    const [assessment, setAssessment] = createSignal<DeploymentAssessmentReport | null>(null);
    const [notice, setNotice] = createSignal<{ tone: 'info' | 'success' | 'error'; message: string } | null>(null);
    const navigate = useNavigate();

    const refreshTimer = setInterval(() => {
        void refetch();
        void refetchNodeTelemetry();
    }, 6000);
    onCleanup(() => clearInterval(refreshTimer));

    const closeMenu = () => setMenuAppId(null);

    const withAction = async (appId: string, action: () => Promise<void>) => {
        setBusyAppId(appId);
        try {
            await action();
            await refetch();
        } catch (error) {
            setNotice({
                tone: 'error',
                message: error instanceof Error ? error.message : 'The requested action could not be completed.'
            });
        } finally {
            setBusyAppId(null);
            closeMenu();
        }
    };

    const handlePause = async (appId: string) => withAction(appId, async () => {
        const result = await api.pauseDeployment(appId);
        setNotice({ tone: 'success', message: result.message });
    });

    const handleRefresh = async (appId: string) => withAction(appId, async () => {
        const result = await api.refreshDeployment(appId);
        setNotice({ tone: result.startedBuild ? 'success' : 'info', message: result.message });
    });

    const handleAssess = async (appId: string) => withAction(appId, async () => {
        const report = await api.assessDeployment(appId);
        setAssessment(report);
        setNotice(null);
    });

    const telemetryCards = createMemo(() => {
        const samples = nodeTelemetry()?.samples ?? [];
        return [
            {
                key: 'cpu',
                label: 'CPU Usage',
                unit: '%',
                accent: 'cyan' as const,
                value: `${Math.round(samples.at(-1)?.cpuUsage ?? 0)}%`,
                subvalue: 'Aggregate processor load across all logical cores.',
                chartMax: 100,
                data: samples.map((sample) => sample.cpuUsage)
            },
            {
                key: 'memory',
                label: 'Memory',
                unit: '%',
                accent: 'blue' as const,
                value: `${(samples.at(-1)?.memoryUsageGb ?? 0).toFixed(1)} GB`,
                subvalue: `Utilizing ${Math.round(samples.at(-1)?.memoryUsagePercent ?? 0)}% of resident system memory.`,
                chartMax: 100,
                data: samples.map((sample) => sample.memoryUsagePercent)
            },
            {
                key: 'storage',
                label: 'Storage',
                unit: ' GB',
                accent: 'amber' as const,
                value: formatStorageValue(samples.at(-1)?.storageUsageGb ?? 0),
                subvalue: 'Tracked Orion databases, keys, build cache, and local storage.',
                chartMax: Math.max(0.25, ...samples.map((sample) => sample.storageUsageGb), 0.25),
                data: samples.map((sample) => sample.storageUsageGb)
            },
            {
                key: 'network',
                label: 'Network',
                unit: 'Mbps',
                accent: 'emerald' as const,
                value: `${(samples.at(-1)?.networkTrafficMbps ?? 0).toFixed(2)} Mbps`,
                subvalue: 'Sum of ingress and egress throughput at the interface.',
                chartMax: Math.max(1, ...samples.map((sample) => sample.networkTrafficMbps), 5),
                data: samples.map((sample) => sample.networkTrafficMbps)
            }
        ];
    });

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

            <section class="mb-8">
                <Show when={nodeTelemetry()} fallback={
                    <div class="flex gap-4 overflow-hidden min-h-[160px]">
                        <For each={[1, 2]}>
                            {() => <div class="professional-card h-64 w-[calc(50%-8px)] animate-pulse rounded-sm bg-gray-50/50 dark:bg-zinc-900/40"></div>}
                        </For>
                    </div>
                }>
                    <div class="flex gap-6 overflow-x-auto snap-x snap-mandatory pb-6 scrollbar-hide no-scrollbar">
                        <For each={telemetryCards()}>
                            {(card) => (
                                <div class="professional-card !p-0 flex flex-col h-72 min-w-[320px] md:min-w-[480px] flex-1 snap-start group relative overflow-hidden border-gray-100/50 dark:border-gray-800/30">
                                     <div class="absolute inset-x-0 bottom-0 h-1/2 bg-gradient-to-t from-blue-500/[0.03] dark:from-blue-400/[0.02] to-transparent pointer-events-none"></div>
                                     
                                     <div class="px-6 pt-5 flex items-start justify-between relative z-10">
                                        <div>
                                            <div class="flex items-center gap-1.5 mb-1">
                                                <div class={`w-1.5 h-1.5 rounded-full ${
                                                    card.accent === 'cyan' ? 'bg-cyan-400 shadow-[0_0_8px_#22d3ee]' :
                                                    card.accent === 'blue' ? 'bg-blue-500 shadow-[0_0_8px_#3b82f6]' :
                                                    card.accent === 'amber' ? 'bg-amber-400 shadow-[0_0_8px_#f59e0b]' :
                                                    'bg-emerald-400 shadow-[0_0_8px_#10b981]'
                                                }`}></div>
                                                <span class="text-[10px] font-black uppercase tracking-[0.25em] text-gray-400 dark:text-gray-500">{card.label}</span>
                                            </div>
                                            <div class="text-3xl font-black text-gray-900 dark:text-gray-100 tracking-tighter">{card.value}</div>
                                        </div>
                                        <div class="max-w-[180px] text-right">
                                            <p class="text-[10px] font-medium leading-relaxed text-gray-400 dark:text-gray-600 uppercase tracking-tight">
                                                {card.subvalue}
                                            </p>
                                        </div>
                                     </div>

                                     <div class="flex-1 relative mt-2 px-2 overflow-hidden">
                                        <TelemetryChart 
                                            label={card.label}
                                            data={card.data}
                                            unit={card.unit}
                                            color={
                                                card.accent === 'cyan' ? '#22d3ee' :
                                                card.accent === 'blue' ? '#3b82f6' :
                                                card.accent === 'amber' ? '#f59e0b' :
                                                '#10b981'
                                            }
                                            max={card.chartMax}
                                        />
                                     </div>
                                </div>
                            )}
                        </For>
                    </div>
                </Show>
            </section>

            <section class="grid grid-cols-1 md:grid-cols-4 gap-4 mb-12">
                <StatCard label="Total Applications" value={summary()?.totalApps ?? 0} />
                <StatCard label="Active Nodes" value={summary()?.connectedPeers ?? 0} />
                <StatCard label="Pilot Gateway" value={summary()?.pilotStatus ?? '...'} />
                <StatCard label="Secure Mesh" value={summary() ? 'mTLS Active' : '...'} />
            </section>

            <section>
                <div class="flex justify-between items-end mb-6">
                    <div>
                        <h2 class="text-xl font-bold text-gray-900 dark:text-white">Active Deployments</h2>
                        <p class="mt-1 text-sm text-gray-500 dark:text-gray-400">Track build health, resource stability, live scale, and deployment actions from one view.</p>
                    </div>
                    <A href="/apps" class="text-xs font-bold text-blue-600 dark:text-blue-400 hover:text-blue-700 dark:hover:text-blue-300 transition-colors uppercase tracking-wider">View All Applications</A>
                </div>

                <Show when={notice()}>
                    {(currentNotice) => (
                        <div class={`mb-4 rounded-sm border px-4 py-3 text-sm ${currentNotice().tone === 'success'
                                ? 'border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/30 dark:text-emerald-300'
                                : currentNotice().tone === 'error'
                                    ? 'border-red-200 bg-red-50 text-red-700 dark:border-red-900/60 dark:bg-red-950/30 dark:text-red-300'
                                    : 'border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/60 dark:bg-blue-950/30 dark:text-blue-300'
                            }`}>
                            {currentNotice().message}
                        </div>
                    )}
                </Show>

                <div class="overflow-visible rounded-sm">
                    <Table>
                        <TableHeader class="bg-gray-50 dark:bg-zinc-900/50 border-b border-gray-100 dark:border-gray-800">
                            <TableRow class="border-b-0 hover:bg-transparent">
                                <TableHead>Application</TableHead>
                                <TableHead>Latest Build</TableHead>
                                <TableHead>Stability</TableHead>
                                <TableHead>Scale</TableHead>
                                <TableHead class="text-right">Actions</TableHead>
                            </TableRow>
                        </TableHeader>
                        <TableBody class="divide-y divide-gray-50 dark:divide-gray-800/50">
                            <Show when={summary()} fallback={
                                <TableRow class="hover:bg-transparent">
                                    <TableCell colSpan={5} class="p-12 text-center text-gray-400 dark:text-gray-600 font-medium italic">
                                        Synchronizing telemetry data...
                                    </TableCell>
                                </TableRow>
                            }>
                                <Show when={summary()?.apps.length === 0}>
                                    <TableRow class="hover:bg-transparent">
                                        <TableCell colSpan={5} class="p-0 border-0">
                                            <div class="flex flex-col items-center justify-center py-24 px-6 text-center bg-gray-50/30 dark:bg-zinc-900/10 rounded-sm">
                                                <h3 class="text-xl font-bold text-gray-900 dark:text-white mb-2">Zero Instances Detected</h3>
                                                <p class="max-w-md text-sm text-gray-500 dark:text-gray-400 mb-8 lowercase leading-relaxed italic">
                                                    Your Orion subnet is currently idle. No applications have been registered to this control plane yet. Launch a new service to begin telemetry tracking.
                                                </p>
                                                <A href="/deploy/github">
                                                    <OrionButton variant="primary" class="!px-10 !py-6 !text-sm !font-bold">
                                                        Deploy Your First App
                                                    </OrionButton>
                                                </A>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                </Show>

                                <For each={summary()?.apps}>
                                    {(app) => (
                                        <TableRow
                                            class="hover:bg-blue-50/50 dark:hover:bg-blue-900/10 group cursor-pointer border-l-2 border-transparent hover:border-blue-500 transition-all"
                                            onClick={() => navigate(`/apps/${app.id}`)}
                                        >
                                            <TableCell>
                                                <div class="font-bold text-gray-900 dark:text-gray-100 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">{app.name}</div>
                                                <div class="mt-1 flex items-center gap-2">
                                                    <span class="text-[11px] font-mono text-gray-400 dark:text-gray-500 tracking-tight">{app.id.substring(0, 12)}</span>
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                <div class="flex flex-col gap-2">
                                                    <ToneBadge tone={getBuildTone(app.latestBuildStatus)}>{app.latestBuildStatus}</ToneBadge>
                                                    <span class="text-[11px] text-gray-500 dark:text-gray-400">
                                                        {app.latestBuildAt ? formatTimestamp(app.latestBuildAt) : 'No builds yet'}
                                                    </span>
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                <div class="flex flex-col gap-2">
                                                    <ToneBadge tone={getStabilityTone(app.stability)}>{app.stability}</ToneBadge>
                                                    <span class="text-[11px] text-gray-500 dark:text-gray-400">
                                                        {getStabilityHint(app.stability)}
                                                    </span>
                                                </div>
                                            </TableCell>
                                            <TableCell>
                                                <div class="flex flex-col">
                                                    <span class="text-lg font-bold text-blue-600 dark:text-blue-400">{app.activeReplicas}</span>
                                                    <span class="text-[10px] text-gray-400 dark:text-gray-500 uppercase font-bold tracking-tight">Replica{app.activeReplicas === 1 ? '' : 's'}</span>
                                                </div>
                                            </TableCell>
                                            <TableCell class="text-right">
                                                <div class="relative inline-flex" onClick={(event) => event.stopPropagation()}>
                                                    <button
                                                        class="inline-flex h-10 w-10 items-center justify-center rounded-sm border border-gray-200 bg-white text-gray-500 transition-colors hover:border-blue-200 hover:text-blue-600 dark:border-gray-800 dark:bg-zinc-950 dark:text-gray-400 dark:hover:border-blue-900 dark:hover:text-blue-400"
                                                        onClick={() => setMenuAppId(menuAppId() === app.id ? null : app.id)}
                                                        aria-label={`Open actions for ${app.name}`}
                                                    >
                                                        <svg width="16" height="16" viewBox="0 0 24 24" fill="currentColor">
                                                            <circle cx="5" cy="12" r="2"></circle>
                                                            <circle cx="12" cy="12" r="2"></circle>
                                                            <circle cx="19" cy="12" r="2"></circle>
                                                        </svg>
                                                    </button>

                                                    <Show when={menuAppId() === app.id}>
                                                        <div class="absolute right-0 top-12 z-20 min-w-[220px] rounded-sm border border-gray-200 bg-white p-2 shadow-xl dark:border-gray-800 dark:bg-zinc-950">
                                                            <ActionButton
                                                                label="Pause Deployment"
                                                                busy={busyAppId() === app.id}
                                                                onClick={() => handlePause(app.id)}
                                                            />
                                                            <ActionButton
                                                                label="Refresh From Repo"
                                                                busy={busyAppId() === app.id}
                                                                onClick={() => handleRefresh(app.id)}
                                                            />
                                                            <ActionButton
                                                                label="Assess Resources"
                                                                busy={busyAppId() === app.id}
                                                                onClick={() => handleAssess(app.id)}
                                                            />
                                                        </div>
                                                    </Show>
                                                </div>
                                            </TableCell>
                                        </TableRow>
                                    )}
                                </For>
                            </Show>
                        </TableBody>
                    </Table>
                </div>
            </section>

            <Show when={assessment()}>
                {(report) => (
                    <div class="fixed inset-0 z-50 flex items-center justify-center bg-black/55 px-4">
                        <div class="w-full max-w-2xl rounded-sm border border-gray-200 bg-white p-8 shadow-2xl dark:border-gray-800 dark:bg-zinc-950">
                            <div class="flex items-start justify-between gap-6">
                                <div>
                                    <div class="text-[10px] font-bold uppercase tracking-[0.2em] text-gray-400 dark:text-gray-500">Resource Assessment</div>
                                    <h3 class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{report().appName}</h3>
                                    <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">{report().review}</p>
                                </div>
                                <button
                                    class="rounded-sm border border-gray-200 px-3 py-2 text-sm text-gray-500 hover:border-blue-200 hover:text-blue-600 dark:border-gray-800 dark:text-gray-400 dark:hover:border-blue-900 dark:hover:text-blue-400"
                                    onClick={() => setAssessment(null)}
                                >
                                    Close
                                </button>
                            </div>

                            <div class="mt-6 grid grid-cols-1 gap-4 md:grid-cols-4">
                                <MetricCard label="Stability" value={report().stability} />
                                <MetricCard label="Current Scale" value={report().currentReplicas} />
                                <MetricCard label="Recommended Scale" value={report().recommendedReplicas} />
                                <MetricCard label="Action" value={report().recommendedAction} />
                            </div>

                            <div class="mt-6 grid grid-cols-1 gap-4 md:grid-cols-2">
                                <div class="rounded-sm border border-gray-100 bg-gray-50 p-4 dark:border-gray-800 dark:bg-zinc-900">
                                    <div class="text-[10px] font-bold uppercase tracking-[0.18em] text-gray-400 dark:text-gray-500">Live Usage</div>
                                    <div class="mt-3 space-y-2 text-sm text-gray-700 dark:text-gray-200">
                                        <div class="flex justify-between"><span>CPU</span><span class="font-bold">{report().cpuUsage.toFixed(1)}%</span></div>
                                        <div class="flex justify-between"><span>Memory</span><span class="font-bold">{report().memoryUsageMb}MB</span></div>
                                        <div class="flex justify-between"><span>Allocated CPU</span><span class="font-bold">{report().allocatedCpuCores} core(s)</span></div>
                                        <div class="flex justify-between"><span>Allocated Memory</span><span class="font-bold">{report().allocatedMemoryMb}MB</span></div>
                                    </div>
                                </div>
                                <div class="rounded-sm border border-gray-100 bg-gray-50 p-4 dark:border-gray-800 dark:bg-zinc-900">
                                    <div class="text-[10px] font-bold uppercase tracking-[0.18em] text-gray-400 dark:text-gray-500">Review</div>
                                    <ul class="mt-3 space-y-2 text-sm text-gray-700 dark:text-gray-200">
                                        <For each={report().findings}>
                                            {(finding) => <li>{finding}</li>}
                                        </For>
                                    </ul>
                                </div>
                            </div>
                        </div>
                    </div>
                )}
            </Show>
        </div>
    );
};

const TelemetryChart: Component<{
    label: string,
    data: number[],
    color: string,
    max: number,
    unit: string
}> = (props) => {
    let canvas: HTMLCanvasElement | undefined;
    let chart: any;

    onMount(() => {
        if (!canvas) return;
        const ctx = canvas.getContext('2d');
        if (!ctx) return;

        chart = new Chart(ctx, {
            type: 'line',
            data: {
                labels: props.data.map((_, i) => i),
                datasets: [{
                    data: props.data,
                    borderColor: props.color,
                    borderWidth: 2,
                    fill: {
                        target: 'origin',
                        above: props.color + '1a',
                    },
                    tension: 0.4,
                    pointRadius: 0,
                    pointHoverRadius: 4,
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false },
                    tooltip: {
                        enabled: true,
                        backgroundColor: '#18181b',
                        titleFont: { size: 10, weight: 'bold' },
                        bodyFont: { size: 10 },
                        padding: 8,
                        displayColors: false,
                        callbacks: {
                            label: (context: any) => `${props.label}: ${context.parsed.y.toFixed(1)}${props.unit}`
                        }
                    }
                },
                scales: {
                    x: { display: false },
                    y: {
                        display: false,
                        min: 0,
                        max: props.max
                    }
                },
                interaction: {
                    intersect: false,
                    mode: 'index',
                },
                animation: false
            }
        });
    });

    createEffect(() => {
        if (chart) {
            chart.data.datasets[0].data = props.data;
            chart.data.labels = props.data.map((_, i) => i);
            chart.update('none');
        }
    });

    onCleanup(() => chart?.destroy());

    return (
        <div class="h-full w-full">
            <canvas ref={canvas} />
        </div>
    );
};

const StatCard: Component<{ label: string; value: string | number }> = (props) => (
    <div class="professional-card !p-6 flex flex-col justify-between min-h-[140px] hover:scale-[1.02] transition-all group overflow-hidden relative">
        <div class="absolute -right-4 -top-4 w-16 h-16 bg-blue-500/5 rounded-full blur-2xl group-hover:bg-blue-500/10 transition-all"></div>
        <div class="text-[10px] font-black text-gray-400 dark:text-gray-500 uppercase tracking-[0.2em] leading-none mb-4">{props.label}</div>
        <div class="text-4xl font-black text-gray-900 dark:text-white tracking-tighter">
            {props.value}
        </div>
        <div class="mt-4 h-1 w-8 bg-gray-100 dark:bg-zinc-800 rounded-full group-hover:w-16 group-hover:bg-blue-500 transition-all duration-500"></div>
    </div>
);

const MetricCard: Component<{ label: string; value: string | number }> = (props) => (
    <div class="rounded-sm border border-gray-100 bg-gray-50 p-4 dark:border-gray-800 dark:bg-zinc-900">
        <div class="text-[10px] font-bold uppercase tracking-[0.18em] text-gray-400 dark:text-gray-500">{props.label}</div>
        <div class="mt-2 text-2xl font-bold text-gray-900 dark:text-white">{props.value}</div>
    </div>
);

const ToneBadge: Component<{ tone: 'green' | 'blue' | 'amber' | 'red' | 'slate'; children: string }> = (props) => (
    <span class={`inline-flex items-center rounded-sm px-2.5 py-1 text-[9px] font-black uppercase tracking-[0.2em] shadow-sm ${props.tone === 'green'
            ? 'bg-emerald-500/10 text-emerald-600 border border-emerald-500/20 dark:text-emerald-400 dark:bg-emerald-400/10 dark:shadow-[0_0_8px_rgba(52,211,153,0.1)]'
            : props.tone === 'blue'
                ? 'bg-blue-500/10 text-blue-600 border border-blue-500/20 dark:text-blue-400 dark:bg-blue-400/10 dark:shadow-[0_0_8px_rgba(96,165,250,0.1)]'
                : props.tone === 'amber'
                    ? 'bg-amber-500/10 text-amber-600 border border-amber-500/20 dark:text-amber-400 dark:bg-amber-400/10 dark:shadow-[0_0_8px_rgba(245,158,11,0.1)]'
                    : props.tone === 'red'
                        ? 'bg-red-500/10 text-red-600 border border-red-500/20 dark:text-red-400 dark:bg-red-400/10 dark:shadow-[0_0_8px_rgba(239,68,68,0.1)]'
                        : 'bg-slate-500/10 text-slate-600 border border-slate-500/20 dark:text-slate-400 dark:bg-slate-800 dark:shadow-none'
        }`}>
        {props.children}
    </span>
);

const ActionButton: Component<{ label: string; busy: boolean; onClick: () => void }> = (props) => (
    <button
        class="flex w-full rounded-sm px-3 py-2 text-left transition-colors hover:bg-gray-50 dark:hover:bg-zinc-900 disabled:cursor-not-allowed disabled:opacity-60"
        disabled={props.busy}
        onClick={props.onClick}
    >
        <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">{props.label}</span>
    </button>
);

function formatTimestamp(value: string) {
    return new Date(value).toLocaleString([], { month: 'short', day: 'numeric', hour: 'numeric', minute: '2-digit' });
}

function formatStorageValue(valueGb: number) {
    if (valueGb >= 1) {
        return `${valueGb.toFixed(2)} GB`;
    }

    return `${Math.round(valueGb * 1024)} MB`;
}

function formatTelemetryWindow(samples: NodeTelemetrySample[]) {
    if (samples.length < 2) {
        return 'Live';
    }

    const first = new Date(samples[0].timestamp).getTime();
    const last = new Date(samples[samples.length - 1].timestamp).getTime();
    const minutes = Math.max(1, Math.round((last - first) / 60000));
    return `${minutes} min`;
}

function getBuildTone(status: string) {
    switch (status) {
        case 'Running':
            return 'green' as const;
        case 'Building':
        case 'Deploying':
        case 'Pending':
            return 'amber' as const;
        case 'Paused':
            return 'slate' as const;
        case 'Failed':
            return 'red' as const;
        default:
            return 'blue' as const;
    }
}

function getOperationalTone(status: string) {
    switch (status) {
        case 'Running':
            return 'green' as const;
        case 'Paused':
            return 'slate' as const;
        case 'Building':
        case 'Deploying':
        case 'Pending':
            return 'amber' as const;
        case 'Failed':
            return 'red' as const;
        default:
            return 'blue' as const;
    }
}

function getStabilityTone(stability: string) {
    switch (stability) {
        case 'Right-Sized':
            return 'green' as const;
        case 'Constrained':
            return 'red' as const;
        case 'Overprovisioned':
            return 'amber' as const;
        default:
            return 'slate' as const;
    }
}

function getStabilityHint(stability: string) {
    switch (stability) {
        case 'Right-Sized':
            return 'Current resource allocation looks healthy.';
        case 'Constrained':
            return 'The workload is pushing its current budget.';
        case 'Overprovisioned':
            return 'There is likely room to scale down safely.';
        case 'Idle':
            return 'No live replicas are serving traffic.';
        default:
            return 'No assessment available.';
    }
}

export default Home;
