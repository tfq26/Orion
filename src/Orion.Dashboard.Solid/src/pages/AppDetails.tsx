import { createResource, createSignal, For, Show, Component, onCleanup, createEffect } from 'solid-js';
import { useParams, A } from '@solidjs/router';
import { OrionBadge, OrionButton, OrionCard } from '../components/UI';
import { api, App, AppLogEntry, Deployment } from '../services/api';
import { useAuth } from '../contexts/AuthContext';
import { MetricsGraph } from '../components/MetricsGraph';
import { DirectoryPickerDialog } from '../components/DirectoryPicker';

const AppDetails: Component = () => {
    const params = useParams();
    const [currentTab, setCurrentTab] = createSignal('Overview');
    const [activeMetric, setActiveMetric] = createSignal<'cpu' | 'memory'>('cpu');
    
    // Fetch app data & metrics
    const [app] = createResource(() => api.getApp(params.id || ''));
    const [metrics, { refetch }] = createResource(() => api.getAppMetrics(params.id || ''));
    const [deployments, { refetch: refetchDeployments }] = createResource(() => params.id || '', api.getDeployments);
    
    // Signals for new features
    const [logs, setLogs] = createSignal<string[]>([]);
    const [isBuilding, setIsBuilding] = createSignal(false);
    const [secretsJson, setSecretsJson] = createSignal('{\n  "API_KEY": "orion_sk_...",\n  "DB_URL": "postgresql://..." \n}');
    
    // Edit mode signals
    const auth = useAuth();
    const [isEditing, setIsEditing] = createSignal(false);
    const [editCommand, setEditCommand] = createSignal('');
    const [editRun, setEditRun] = createSignal('');
    const [editFolder, setEditFolder] = createSignal('');
    const [showPicker, setShowPicker] = createSignal(false);

    createEffect(() => {
        const a = app();
        if (a) {
            setEditCommand(a.buildCommand || 'npm run build');
            setEditRun(a.runCommand || 'npm start');
            setEditFolder(a.buildFolder || 'dist');
        }
    });

    const getOwnerName = () => {
        const ownerId = app()?.ownerId;
        const currentUser = auth.user();
        if (currentUser && ownerId === currentUser.userId) {
            return currentUser.name || 'You';
        }
        return ownerId || '—';
    };
    
    const timer = setInterval(refetch, 5000);
    const deploymentTimer = setInterval(() => {
        if (params.id) refetchDeployments();
    }, 3000);
    
    // Connect to real logs stream
    createEffect(() => {
        if (!params.id) return;
        const cleanup = api.streamAppLogs(params.id, (log: AppLogEntry) => {
            const formatted = `[${new Date(log.timestamp).toLocaleTimeString()}] ${log.level.toUpperCase()}: ${log.message}`;
            setLogs(prev => [...prev.slice(-100), formatted]);
            
            // Auto-detect build completion or progress
            if (log.message.toLowerCase().includes('successful') || log.message.toLowerCase().includes('started instance')) {
                setIsBuilding(false);
            }
        });
        onCleanup(cleanup);
    });

    onCleanup(() => clearInterval(timer));
    onCleanup(() => clearInterval(deploymentTimer));

    const getLatestDeployment = (): Deployment | undefined => {
        const list = deployments();
        if (!list?.length) return undefined;
        return [...list].sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime())[0];
    };

    createEffect(() => {
        const latest = getLatestDeployment();
        if (!latest) return;

        if (latest.status === 'Pending' || latest.status === 'Building' || latest.status === 'Deploying') {
            setIsBuilding(true);
            return;
        }

        setIsBuilding(false);

        if (latest.status === 'Failed') {
            setCurrentTab('Logs');
        }
    });

    const handleRedeploy = async () => {
        setIsBuilding(true);
        setCurrentTab('Logs');
        setLogs([]);
        
        try {
            const deployment = await api.triggerBuild(params.id || '');
            setLogs(prev => [...prev, `[${new Date().toLocaleTimeString()}] INFO: Build queued (${deployment.id.slice(0, 8)})`]);
            await refetchDeployments();
        } catch (e) {
            setLogs(prev => [...prev, `[${new Date().toLocaleTimeString()}] ERROR: ${e instanceof Error ? e.message : 'Failed to trigger build'}`]);
            setIsBuilding(false);
        }
    };

    const getDeploymentTone = () => {
        const latest = getLatestDeployment();
        if (!latest) return 'idle';
        if (latest.status === 'Failed') return 'failed';
        if (latest.status === 'Pending' || latest.status === 'Building' || latest.status === 'Deploying') return 'building';
        return 'running';
    };

    const getDeploymentLabel = () => {
        const latest = getLatestDeployment();
        if (!latest) return 'Awaiting Deployment';
        if (latest.status === 'Failed') return 'Build Failed';
        if (latest.status === 'Pending') return 'Queued';
        if (latest.status === 'Building') return 'Build In Progress';
        if (latest.status === 'Deploying') return 'Deploying';
        return 'Instance Logs Stream';
    };

    const handleUpdateSecrets = async () => {
        try {
            const parsed = JSON.parse(secretsJson());
            await api.updateAppSecrets(params.id || '', parsed);
            alert('Secrets updated and queued for next build.');
        } catch (e) {
            alert('Invalid JSON format for secrets.');
        }
    };

    return (
        <div class="animate-in fade-in slide-in-from-bottom duration-400 text-gray-900 dark:text-gray-100 transition-colors duration-300">
            <header class="mb-10">
                <div class="flex items-center gap-2 text-xs text-gray-400 dark:text-gray-500 mb-4 font-bold uppercase tracking-widest">
                    <A href="/apps" class="hover:text-blue-600 dark:hover:text-blue-400 transition-colors">Applications</A>
                    <span>/</span>
                    <span class="text-gray-500">Deployment Details</span>
                </div>

                <div class="flex justify-between items-start">
                    <div>
                        <div class="flex items-center gap-3 mb-1">
                           <h1 class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white">{app()?.name || `App_${(params.id || '').split('-')[0].toUpperCase()}`}</h1>
                           <OrionBadge status={app()?.status === 'Running' ? 'success' : 'default'}>{app()?.status || 'Active'}</OrionBadge>
                        </div>
                        <div class="flex items-center gap-4 text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">
                            <span class="font-mono">ID: {(params.id || '').substring(0, 12)}</span>
                            <span class="w-1 h-1 bg-gray-300 dark:bg-gray-700 rounded-full"></span>
                            <a href={app()?.url} target="_blank" class="text-blue-600 dark:text-blue-400 hover:underline transition-all lowercase italic tracking-normal">Live: {app()?.url}</a>
                        </div>
                    </div>
                    <div class="flex gap-3">
                        <OrionButton variant="ghost" onclick={handleRedeploy} disabled={isBuilding() || isEditing()}>
                            {isBuilding() ? 'Building...' : 'Redeploy'}
                        </OrionButton>
                        <OrionButton variant="primary" onclick={() => setCurrentTab('Settings')}>
                            Manage Instance
                        </OrionButton>
                    </div>
                </div>
            </header>

            <div class="flex gap-8 border-b border-gray-100 dark:border-gray-800 mb-10 overflow-x-auto no-scrollbar">
                <For each={['Overview', 'Logs', 'Telemetry', 'Secrets', 'Settings']}>
                    {tab => (
                        <button
                            class={`
                    pb-3 text-sm font-bold transition-all uppercase tracking-widest
                    ${currentTab() === tab ? 'border-b-2 border-blue-600 text-blue-600 dark:text-blue-400 dark:border-blue-400' : 'text-gray-400 dark:text-gray-500 hover:text-gray-600 dark:hover:text-gray-300'}
                  `}
                            onClick={() => setCurrentTab(tab)}
                        >
                            {tab}
                        </button>
                    )}
                </For>
            </div>

            <div class="grid grid-cols-1 md:grid-cols-2 gap-6">
                <Show when={currentTab() === 'Overview'}>
                    <div class="space-y-6">
                        <OrionCard title="Application Metadata">
                            <div class="space-y-4">
                                <SpecItem label="Status" value={app()?.status || 'Active'} color="cyan" />
                                <SpecItem label="Source Repository" value={app()?.repoUrl?.startsWith('orion-upload://') ? 'Local Upload' : app()?.repoUrl?.split('/').pop()?.replace('.git', '') || '—'} />
                                <SpecItem label="Public URL" value={app()?.url || '—'} color="cyan" />
                                <SpecItem label="Owner" value={getOwnerName()} />
                            </div>
                        </OrionCard>

                        <div class="grid grid-cols-2 gap-4">
                            <div class="bg-blue-50/30 dark:bg-blue-900/10 border border-blue-100 dark:border-blue-900/30 p-6 rounded-sm shadow-sm">
                                <div class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-2">CPU Utilization</div>
                                <div class="text-2xl font-bold text-blue-600 dark:text-blue-400">
                                    {metrics() && metrics()!.length > 0 ? Math.round(metrics()![metrics()!.length - 1].cpu) : 0}%
                                </div>
                            </div>
                            <div class="bg-gray-50/50 dark:bg-gray-900/30 border border-gray-100 dark:border-gray-800/50 p-6 rounded-sm shadow-sm">
                                <div class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-2">Memory Reserved</div>
                                <div class="text-2xl font-bold text-gray-700 dark:text-gray-200">
                                    {metrics() && metrics()!.length > 0 ? Math.round(metrics()![metrics()!.length - 1].memory) : 0}MB
                                </div>
                            </div>
                        </div>
                    </div>

                    <OrionCard title="Deployment History">
                        <div class="space-y-3">
                            <Show when={app()} fallback={<div class="p-8 text-center text-xs text-gray-400 uppercase tracking-widest font-bold">No history available</div>}>
                                <div class="p-4 bg-gray-50 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 rounded-sm flex justify-between items-center text-sm transition-all hover:bg-white dark:hover:bg-gray-900 hover:shadow-sm hover:border-blue-100 dark:hover:border-blue-900 group">
                                    <span class="text-gray-600 dark:text-gray-300 font-bold group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">Initial Deployment</span>
                                    <div class="flex items-center gap-4">
                                        <span class="text-[11px] font-bold text-gray-400 dark:text-gray-500">Just now</span>
                                        <OrionBadge status="success">Successful</OrionBadge>
                                    </div>
                                </div>
                            </Show>
                        </div>
                    </OrionCard>
                </Show>

                <Show when={currentTab() === 'Telemetry'}>
                    <div class="col-span-2 grid grid-cols-1 md:grid-cols-3 gap-6">
                        <div class="md:col-span-2 space-y-6">
                            <div class="flex justify-between items-end">
                                <h4 class="text-xs font-semibold text-gray-400 dark:text-gray-500 uppercase tracking-wider">Historical Performance</h4>
                                <div class="flex bg-gray-100 dark:bg-gray-900 border border-gray-200 dark:border-gray-800 p-1 rounded-sm">
                                    <button 
                                        onClick={() => setActiveMetric('cpu')}
                                        class={`px-3 py-1 text-xs font-semibold rounded-sm transition-all ${activeMetric() === 'cpu' ? 'bg-blue-600 text-white shadow-sm' : 'text-gray-500 hover:text-gray-900 dark:hover:text-gray-100'}`}
                                    >
                                        CPU
                                    </button>
                                    <button 
                                        onClick={() => setActiveMetric('memory')}
                                        class={`px-3 py-1 text-xs font-semibold rounded-sm transition-all ${activeMetric() === 'memory' ? 'bg-blue-600 text-white shadow-sm' : 'text-gray-500 hover:text-gray-900 dark:hover:text-gray-100'}`}
                                    >
                                        Memory
                                    </button>
                                </div>
                            </div>
                            <div class="h-[300px] bg-white dark:bg-[#050505] rounded-sm border border-gray-100 dark:border-gray-800 p-4 shadow-sm">
                                <Show when={metrics() && metrics()!.length > 0} fallback={<div class="h-full flex items-center justify-center text-xs text-gray-400 font-bold uppercase tracking-widest">Awaiting telemetry data...</div>}>
                                    <MetricsGraph data={metrics() || []} type={activeMetric()} height={260} />
                                </Show>
                            </div>
                        </div>
                        <OrionCard title="Telemetry Stream">
                            <div class="space-y-3 font-mono text-[11px] text-gray-600 dark:text-gray-400">
                                <Show when={metrics() && metrics()!.length > 0} fallback={<div class="text-center py-4 text-[10px] font-bold uppercase tracking-widest text-gray-500">No active stream</div>}>
                                    <For each={metrics()?.slice().reverse().slice(0, 15)}>
                                        {(m) => (
                                            <div class="flex justify-between border-b border-gray-50 dark:border-gray-800/50 pb-2 last:border-0">
                                                <span class="text-gray-400 dark:text-gray-500 font-medium">{new Date(m.timestamp).toLocaleTimeString()}</span>
                                                <span class={`${activeMetric() === 'cpu' ? 'text-blue-600 dark:text-blue-400 font-bold' : 'text-gray-500'}`}>{m.cpu.toFixed(1)}%</span>
                                                <span class={`${activeMetric() === 'memory' ? 'text-gray-900 dark:text-gray-100 font-bold' : 'text-gray-500'}`}>{m.memory.toFixed(0)}MB</span>
                                            </div>
                                        )}
                                    </For>
                                </Show>
                            </div>
                        </OrionCard>
                    </div>
                </Show>

                <Show when={currentTab() === 'Logs'}>
                    <div class="col-span-2 bg-[#020617] border border-gray-800 p-8 rounded-sm font-mono text-[13px] leading-relaxed shadow-2xl min-h-[500px] overflow-y-auto">
                        <div class="flex items-center gap-3 mb-6 pb-4 border-b border-gray-800/50">
                            <div class={`w-2 h-2 rounded-full shadow-sm ${getDeploymentTone() === 'building' ? 'animate-pulse bg-amber-500 shadow-amber-500/50' : ''} ${getDeploymentTone() === 'failed' ? 'bg-red-500 shadow-red-500/50' : ''} ${getDeploymentTone() === 'running' ? 'bg-emerald-500 shadow-emerald-500/50' : ''} ${getDeploymentTone() === 'idle' ? 'bg-slate-500 shadow-slate-500/50' : ''}`}></div>
                            <span class="text-xs font-bold uppercase tracking-widest text-slate-400">
                                {getDeploymentLabel()}
                            </span>
                        </div>

                        <Show when={getLatestDeployment()?.status === 'Failed'}>
                            <div class="mb-6 rounded-sm border border-red-900/70 bg-red-950/40 px-4 py-3 text-red-200">
                                <div class="text-[10px] font-bold uppercase tracking-[0.2em] text-red-300">Last deployment failed</div>
                                <div class="mt-1 text-[12px] text-red-200/90">
                                    Review the build logs below to find the failing command or missing dependency.
                                </div>
                            </div>
                        </Show>
                        
                        <div class="space-y-1.5">
                            <Show when={logs().length === 0}>
                                <div class="text-slate-600 italic py-20 text-center">
                                    <p class="mb-2 uppercase text-[10px] font-bold tracking-[0.2em] opacity-40">Zero state reached</p>
                                    <p class="text-[11px]">Click "Redeploy" to initiate a build and stream terminal output here.</p>
                                </div>
                            </Show>
                            <For each={logs()}>
                                {(log) => (
                                    <div class={`
                                        ${log.includes('ERROR') ? 'text-red-400' : ''}
                                        ${log.includes('SUCCESS') ? 'text-emerald-400 font-bold' : ''}
                                        ${log.includes('INFO') ? 'text-slate-400' : ''}
                                        ${log.includes('DEBUG') ? 'text-slate-600 text-[11px]' : ''}
                                        ${!log.includes(':') ? 'text-slate-300' : ''}
                                    `}>
                                        {log}
                                    </div>
                                )}
                            </For>
                        </div>
                    </div>
                </Show>

                <Show when={currentTab() === 'Secrets'}>
                    <div class="col-span-2 space-y-6">
                        <div class="professional-card">
                            <div class="flex justify-between items-center mb-6">
                                <div>
                                    <h3 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1">Environment Secrets</h3>
                                    <p class="text-[11px] text-gray-500">Paste your raw JSON environment map below. Secrets are encrypted at rest.</p>
                                </div>
                                <OrionButton variant="primary" onclick={handleUpdateSecrets}>Save & Inject</OrionButton>
                            </div>

                            <textarea 
                                class="w-full h-64 bg-gray-50 dark:bg-[#0a0a0a] border border-gray-100 dark:border-gray-800 rounded-sm p-6 font-mono text-sm text-gray-800 dark:text-gray-200 outline-none focus:border-blue-500 transition-all transition-colors duration-300 shadow-inner"
                                value={secretsJson()}
                                onInput={(e) => setSecretsJson(e.currentTarget.value)}
                            ></textarea>

                            <div class="mt-8">
                                <h4 class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-[0.2em] mb-4">Extracted Values</h4>
                                <div class="grid grid-cols-1 md:grid-cols-2 gap-3">
                                    <For each={(() => { try { return Object.entries(JSON.parse(secretsJson())) } catch { return [] } })()}>
                                        {([key, value]) => (
                                            <div class="flex justify-between items-center p-3 px-4 bg-white dark:bg-zinc-900 border border-gray-100 dark:border-gray-800 rounded-sm">
                                                <span class="text-xs font-bold text-blue-600 dark:text-blue-400 font-mono tracking-tight">{key}</span>
                                                <span class="text-xs font-bold text-gray-400 font-mono">••••••••</span>
                                            </div>
                                        )}
                                    </For>
                                </div>
                            </div>
                        </div>
                    </div>
                </Show>

                <Show when={currentTab() === 'Settings'}>
                    <div class="col-span-2 grid grid-cols-1 md:grid-cols-2 gap-6 items-start">
                        <div class="space-y-6">
                            <OrionCard title="Build Configuration">
                                <div class="space-y-4 pt-2">
                                    <div>
                                        <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1.5 block">Build Command</label>
                                        <input 
                                            type="text" 
                                            class="w-full bg-gray-50 dark:bg-zinc-900 border border-gray-100 dark:border-gray-800 rounded-sm px-3 py-2 text-sm font-semibold text-gray-900 dark:text-white outline-none focus:border-blue-500 transition-all placeholder:text-gray-400 dark:placeholder:text-gray-600"
                                            value={editCommand()}
                                            placeholder="e.g. npm run build"
                                            onInput={(e) => setEditCommand(e.currentTarget.value)}
                                        />
                                    </div>
                                    <div>
                                        <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1.5 block">Run Command</label>
                                        <input 
                                            type="text" 
                                            class="w-full bg-gray-50 dark:bg-zinc-900 border border-gray-100 dark:border-gray-800 rounded-sm px-3 py-2 text-sm font-semibold text-gray-900 dark:text-white outline-none focus:border-blue-500 transition-all placeholder:text-gray-400 dark:placeholder:text-gray-600"
                                            value={editRun()}
                                            placeholder="e.g. npm start"
                                            onInput={(e) => setEditRun(e.currentTarget.value)}
                                        />
                                    </div>
                                    <div>
                                        <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1.5 block">Build Folder</label>
                                        <div class="flex gap-2">
                                            <input 
                                                type="text" 
                                                class="flex-1 bg-gray-50 dark:bg-zinc-900 border border-gray-100 dark:border-gray-800 rounded-sm px-3 py-2 text-sm font-semibold text-gray-900 dark:text-white outline-none focus:border-blue-500 transition-all placeholder:text-gray-400 dark:placeholder:text-gray-600"
                                                value={editFolder()}
                                                placeholder="e.g. dist"
                                                onInput={(e) => setEditFolder(e.currentTarget.value)}
                                            />
                                            <OrionButton variant="ghost" class="shrink-0" onclick={() => setShowPicker(true)}>
                                                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                                                    <path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                                                </svg>
                                            </OrionButton>
                                        </div>
                                    </div>
                                    <OrionButton variant="primary" class="w-full mt-2" onclick={async () => {
                                        try {
                                            const currentApp = app();
                                            if (!currentApp) return;
                                            await api.updateApp(currentApp.id, {
                                                ...currentApp,
                                                buildCommand: editCommand(),
                                                runCommand: editRun(),
                                                buildFolder: editFolder()
                                            });
                                            alert('Build configuration saved.');
                                        } catch (e) {
                                            alert('Failed to update configuration.');
                                        }
                                    }}>Save Configuration</OrionButton>
                                </div>
                            </OrionCard>
                        </div>

                        <div class="space-y-6">
                            <OrionCard title="General Settings">
                                <div class="space-y-4 pt-2">
                                    <div>
                                        <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1.5 block">Application Name</label>
                                        <input 
                                            type="text" 
                                            class="w-full bg-gray-50 dark:bg-zinc-900 border border-gray-100 dark:border-gray-800 rounded-sm px-3 py-2 text-sm font-semibold text-gray-900 dark:text-white outline-none focus:border-blue-500 transition-all"
                                            value={app()?.name || ''}
                                            disabled={true}
                                        />
                                        <p class="mt-1.5 text-[10px] text-gray-500 italic">Immutable unique identifier for the Orion subnet.</p>
                                    </div>
                                    <div>
                                        <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1.5 block">Repository Source</label>
                                        <input 
                                            type="text" 
                                            class="w-full bg-gray-50 dark:bg-zinc-900 border border-gray-100 dark:border-gray-800 rounded-sm px-3 py-2 text-sm font-semibold text-gray-400 dark:text-gray-500 outline-none cursor-not-allowed"
                                            value={app()?.repoUrl?.startsWith('orion-upload://') ? 'Local Upload Source' : app()?.repoUrl || ''}
                                            disabled={true}
                                        />
                                    </div>
                                    <div class="pt-4 border-t border-gray-50 dark:border-gray-800/50 mt-4">
                                        <OrionButton variant="ghost" class="w-full text-red-500 border-red-100 dark:border-red-900/30 hover:bg-red-50 dark:hover:bg-red-950/20" onclick={async () => {
                                            if (confirm('Are you absolutely sure you want to delete this application? This action cannot be undone.')) {
                                                try {
                                                    await api.deleteApp(params.id || '');
                                                    window.location.href = '/apps';
                                                } catch (e) {
                                                    alert(e instanceof Error ? e.message : 'Failed to delete application. Please try again.');
                                                }
                                            }
                                        }}>Delete Application</OrionButton>
                                    </div>
                                </div>
                            </OrionCard>
                        </div>
                    </div>
                </Show>
            </div>

            <Show when={showPicker()}>
                <DirectoryPickerDialog 
                    repoUrl={app()?.repoUrl || ''} 
                    onSelect={(path) => setEditFolder(path)}
                    onClose={() => setShowPicker(false)}
                />
            </Show>
        </div>
    );
};

const SpecItem: Component<{ label: string; value: string; color?: string }> = (props) => (
    <div class="flex justify-between items-center py-2.5 border-b border-gray-50 dark:border-gray-800/50 font-medium last:border-0">
        <span class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">{props.label}</span>
        <span class={`text-sm font-semibold ${props.color === 'cyan' ? 'text-blue-600 dark:text-blue-400' : 'text-gray-700 dark:text-gray-200'}`}>{props.value}</span>
    </div>
);

export default AppDetails;
