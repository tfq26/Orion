import { createResource, For, Show, Component, createSignal } from 'solid-js';
import { A } from '@solidjs/router';
import { api } from '../services/api';
import { OrionBadge, OrionButton } from '../components/UI';
import DeploymentWizard from '../components/DeploymentWizard';

const Apps: Component = () => {
  const [summary, { refetch }] = createResource(api.getSummary);
  const [showWizard, setShowWizard] = createSignal(false);

  return (
    <div class="animate-in fade-in slide-in-from-right duration-400 text-gray-900 dark:text-gray-100 transition-colors duration-300">
      <Show when={showWizard()}>
        <DeploymentWizard
          onClose={() => setShowWizard(false)}
          onSuccess={() => refetch()}
        />
      </Show>

      <header class="flex justify-between items-center mb-10">
        <div>
          <h1 class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white mb-2">Applications</h1>
          <p class="text-sm font-medium text-gray-500 dark:text-gray-400">Manage and monitor orchestration across your infrastructure.</p>
        </div>
        <OrionButton variant="primary" onclick={() => setShowWizard(true)}>+ New Application</OrionButton>
      </header>

      <div class="grid grid-cols-1 gap-4">
        <Show when={!summary.loading} fallback={
          <div class="p-20 text-center text-gray-400 dark:text-gray-600 font-medium italic">Fetching active applications...</div>
        }>
          <Show when={summary()?.apps.length === 0} fallback={
            <For each={summary()?.apps}>
              {(app) => (
                <div class="professional-card flex items-center justify-between group">
                  <div class="flex items-center gap-6">
                    <div class="w-12 h-12 bg-gray-50 dark:bg-gray-900/50 border border-gray-100 dark:border-gray-800 flex items-center justify-center text-blue-600 dark:text-blue-400 group-hover:border-blue-200 dark:group-hover:border-blue-800 group-hover:bg-blue-50 dark:group-hover:bg-blue-900/20 transition-all rounded-xl shadow-sm">
                      <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5"><rect x="3" y="3" width="18" height="18" rx="2" ry="2" /><path d="M12 8v8m-4-4h8" /></svg>
                    </div>
                    <div>
                      <h3 class="font-bold text-lg text-gray-900 dark:text-gray-100 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">{app.name}</h3>
                      <div class="text-xs font-medium text-gray-400 dark:text-gray-500 font-mono tracking-tight">{app.id}</div>
                    </div>
                  </div>

                  <div class="flex items-center gap-12">
                    <div class="text-right">
                      <div class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-1">Replicas</div>
                      <div class="text-xl font-bold text-blue-600 dark:text-blue-400 tracking-tighter">{app.activeReplicas}</div>
                    </div>
                    <div class="text-right">
                      <div class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-2">Status</div>
                      <OrionBadge status={app.status === 'Running' ? 'success' : 'default'}>{app.status}</OrionBadge>
                    </div>
                    <div class="flex items-center gap-6">
                      <a 
                        href={`https://${app.name.toLowerCase().replace(/\s+/g, '-')}.orion.run`} 
                        target="_blank" 
                        class="text-xs font-bold text-blue-600 dark:text-blue-400 hover:underline flex items-center gap-1.5 transition-all"
                        onClick={(e) => e.stopPropagation()}
                      >
                        <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                          <path d="M18 13v6a2 2 0 01-2 2H5a2 2 0 01-2-2V8a2 2 0 012-2h6M15 3h6v6M10 14L21 3"/>
                        </svg>
                        Launch
                      </a>
                      <A href={`/apps/${app.id}`}>
                        <OrionButton variant="ghost">View Details</OrionButton>
                      </A>
                    </div>
                  </div>
                </div>
              )}
              </For>
          }>
            <div class="flex flex-col items-center justify-center py-32 px-6 text-center bg-gray-50/30 dark:bg-zinc-900/10 rounded-sm border border-dashed border-gray-100 dark:border-gray-800">
              <h3 class="text-2xl font-bold text-gray-900 dark:text-white mb-3">No Applications</h3>
                <p class="max-w-md text-sm text-gray-500 dark:text-gray-400 mb-10 lowercase leading-relaxed italic">
                  Registry is currently empty. Start by deploying an application from GitHub to activate your compute subnet.
                </p>
                <OrionButton variant="primary" class="!px-12 !py-6 !text-sm !font-bold" onclick={() => setShowWizard(true)}>
                  Create Your First Application
                </OrionButton>
            </div>
          </Show>
        </Show>
      </div>
      </div>
    );
  };

export default Apps;
