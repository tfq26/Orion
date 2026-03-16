import { createMemo, createResource, createSignal, For, Show, type Component } from 'solid-js';
import { api } from '../services/api';

type AutomationTone = 'blue' | 'emerald' | 'amber' | 'violet';

interface AutomationPolicy {
  id: string;
  name: string;
  category: 'Deployments' | 'Scaling' | 'Resources' | 'Hygiene';
  description: string;
  cadence: string;
  target: string;
  status: 'Active' | 'Draft' | 'Recommended';
  tone: AutomationTone;
  lastRun?: string;
  nextRun?: string;
}

const Automations: Component = () => {
  const [summary] = createResource(api.getSummary);
  const [selectedPolicyId, setSelectedPolicyId] = createSignal<string>('refresh-watch');

  const policies = createMemo<AutomationPolicy[]>(() => {
    const apps = summary()?.apps ?? [];
    const primaryApp = apps[0];

    return [
      {
        id: 'refresh-watch',
        name: 'Refresh Watch',
        category: 'Deployments',
        description: 'Poll connected repositories, compare revisions, and trigger a build only when new commits are detected.',
        cadence: 'Every 15 minutes',
        target: primaryApp ? `${primaryApp.name} and other connected apps` : 'All connected apps',
        status: 'Recommended',
        tone: 'blue',
        nextRun: 'Today at :15, :30, :45',
      },
      {
        id: 'nightly-assess',
        name: 'Nightly Resource Assess',
        category: 'Resources',
        description: 'Run a nightly audit against active deployments and generate scale guidance based on CPU, memory, storage, and traffic.',
        cadence: 'Daily at 1:00 AM',
        target: apps.length ? `${apps.length} active application${apps.length === 1 ? '' : 's'}` : 'Control plane',
        status: 'Active',
        tone: 'emerald',
        lastRun: 'Last night at 1:00 AM',
        nextRun: 'Tomorrow at 1:00 AM'
      },
      {
        id: 'stability-guard',
        name: 'Stability Guard',
        category: 'Scaling',
        description: 'Watch for sustained constrained deployments and recommend or trigger scale-up actions when pressure remains high.',
        cadence: 'Every 10 minutes',
        target: apps.filter((app) => app.stability === 'Constrained').length
          ? `${apps.filter((app) => app.stability === 'Constrained').length} constrained service${apps.filter((app) => app.stability === 'Constrained').length === 1 ? '' : 's'}`
          : 'Services with instability',
        status: apps.some((app) => app.stability === 'Constrained') ? 'Recommended' : 'Draft',
        tone: 'amber',
        nextRun: 'Next cycle in 10 minutes'
      },
      {
        id: 'artifact-cleanup',
        name: 'Artifact Cleanup',
        category: 'Hygiene',
        description: 'Prune stale build artifacts, old logs, and inactive deployment residue to keep Orion storage footprint under control.',
        cadence: 'Weekly on Sunday',
        target: 'Build cache, telemetry logs, local storage',
        status: 'Draft',
        tone: 'violet',
        nextRun: 'Sunday at 3:00 AM'
      }
    ];
  });

  const selectedPolicy = createMemo(() => policies().find((policy) => policy.id === selectedPolicyId()) ?? policies()[0]);

  const recentRuns = createMemo(() => {
    const apps = summary()?.apps ?? [];
    return [
      {
        id: 'run-refresh',
        name: 'Refresh Watch',
        result: apps.some((app) => app.latestBuildStatus === 'Running') ? 'No new commits detected' : 'Waiting for connected repositories',
        time: '6 minutes ago',
        tone: 'blue' as const
      },
      {
        id: 'run-assess',
        name: 'Nightly Resource Assess',
        result: apps.some((app) => app.stability === 'Overprovisioned')
          ? 'Suggested scale down opportunities'
          : 'No scale changes recommended',
        time: '10 hours ago',
        tone: 'emerald' as const
      },
      {
        id: 'run-guard',
        name: 'Stability Guard',
        result: apps.some((app) => app.stability === 'Constrained')
          ? 'High pressure detected on one or more services'
          : 'Cluster within safe operating range',
        time: '14 minutes ago',
        tone: 'amber' as const
      }
    ];
  });

  const recommendedTargets = createMemo(() => {
    const apps = summary()?.apps ?? [];
    return apps.length
      ? apps.map((app) => ({
          name: app.name,
          summary: `${app.activeReplicas} replicas · ${app.latestBuildStatus} build · ${app.stability}`,
          recommendation: app.latestBuildStatus === 'Failed'
            ? 'Enable refresh watch + retry policy'
            : app.stability === 'Constrained'
              ? 'Attach stability guard'
              : app.stability === 'Overprovisioned'
                ? 'Schedule nightly assess'
                : 'Healthy baseline'
        }))
      : [{
          name: 'No applications yet',
          summary: 'Deploy an app to start attaching automations.',
          recommendation: 'Suggested policies will appear here'
        }];
  });

  return (
    <div class="animate-in fade-in slide-in-from-right duration-400 text-gray-900 dark:text-gray-100 transition-colors duration-300">
      <header class="mb-10 flex items-end justify-between gap-6">
        <div>
          <h1 class="text-3xl font-bold tracking-tight text-gray-900 dark:text-white">Automations</h1>
          <p class="mt-2 max-w-3xl text-sm text-gray-500 dark:text-gray-400">
            Define hands-off platform behaviors for refreshes, assessments, scaling decisions, and cleanup so Orion keeps your services healthy without constant manual intervention.
          </p>
        </div>
        <button class="rounded-sm border border-blue-200 bg-blue-50 px-4 py-3 text-xs font-bold uppercase tracking-[0.18em] text-blue-600 transition-colors hover:border-blue-300 hover:bg-blue-100 dark:border-blue-900/60 dark:bg-blue-950/30 dark:text-blue-400 dark:hover:border-blue-800 dark:hover:bg-blue-950/50">
          Create Policy
        </button>
      </header>

      <section class="grid gap-4 md:grid-cols-4 mb-10">
        <AutomationStat label="Policies" value={policies().length} detail="Draft, active, and recommended rules" />
        <AutomationStat label="Active" value={policies().filter((policy) => policy.status === 'Active').length} detail="Running unattended in the control plane" />
        <AutomationStat label="Recommendations" value={policies().filter((policy) => policy.status === 'Recommended').length} detail="Suggested next automations to enable" />
        <AutomationStat label="Recent Runs" value={recentRuns().length} detail="Latest system-triggered policy evaluations" />
      </section>

      <section class="grid gap-6 xl:grid-cols-[1.2fr_0.8fr]">
        <div class="space-y-6">
          <div class="professional-card">
            <div class="flex items-end justify-between gap-4">
              <div>
                <div class="text-[10px] font-bold uppercase tracking-[0.22em] text-gray-400 dark:text-gray-500">Policy Library</div>
                <h2 class="mt-2 text-xl font-bold text-gray-900 dark:text-white">Suggested Orion Automations</h2>
              </div>
              <div class="text-xs font-semibold text-gray-500 dark:text-gray-400">Designed around the workflows already present in your control plane.</div>
            </div>

            <div class="mt-6 grid gap-4">
              <For each={policies()}>
                {(policy) => (
                  <button
                    class={`rounded-sm border p-5 text-left transition-all hover:-translate-y-0.5 hover:shadow-md ${
                      selectedPolicyId() === policy.id
                        ? 'border-blue-300 bg-blue-50/70 dark:border-blue-800 dark:bg-blue-950/20'
                        : 'border-gray-100 bg-white dark:border-gray-800 dark:bg-zinc-950'
                    }`}
                    onClick={() => setSelectedPolicyId(policy.id)}
                  >
                    <div class="flex items-start justify-between gap-4">
                      <div>
                        <div class="text-[10px] font-bold uppercase tracking-[0.18em] text-gray-400 dark:text-gray-500">{policy.category}</div>
                        <div class="mt-2 text-lg font-bold text-gray-900 dark:text-white">{policy.name}</div>
                        <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">{policy.description}</p>
                      </div>
                      <AutomationPill tone={policy.tone}>{policy.status}</AutomationPill>
                    </div>
                    <div class="mt-5 grid gap-3 md:grid-cols-3">
                      <PolicyField label="Cadence" value={policy.cadence} />
                      <PolicyField label="Target" value={policy.target} />
                      <PolicyField label="Next Run" value={policy.nextRun ?? 'Awaiting schedule'} />
                    </div>
                  </button>
                )}
              </For>
            </div>
          </div>

          <div class="professional-card">
            <div class="text-[10px] font-bold uppercase tracking-[0.22em] text-gray-400 dark:text-gray-500">Recommended Targets</div>
            <div class="mt-5 grid gap-3">
              <For each={recommendedTargets()}>
                {(target) => (
                  <div class="rounded-sm border border-gray-100 bg-gray-50 px-4 py-4 dark:border-gray-800 dark:bg-zinc-900">
                    <div class="flex items-center justify-between gap-4">
                      <div>
                        <div class="font-semibold text-gray-900 dark:text-gray-100">{target.name}</div>
                        <div class="mt-1 text-sm text-gray-500 dark:text-gray-400">{target.summary}</div>
                      </div>
                      <div class="text-right text-xs font-semibold uppercase tracking-[0.16em] text-blue-600 dark:text-blue-400">
                        {target.recommendation}
                      </div>
                    </div>
                  </div>
                )}
              </For>
            </div>
          </div>
        </div>

        <div class="space-y-6">
          <div class="professional-card">
            <div class="text-[10px] font-bold uppercase tracking-[0.22em] text-gray-400 dark:text-gray-500">Policy Inspector</div>
            <Show when={selectedPolicy()}>
              {(policy) => (
                <>
                  <div class="mt-4 flex items-start justify-between gap-4">
                    <div>
                      <h2 class="text-2xl font-bold text-gray-900 dark:text-white">{policy().name}</h2>
                      <p class="mt-2 text-sm text-gray-500 dark:text-gray-400">{policy().description}</p>
                    </div>
                    <AutomationPill tone={policy().tone}>{policy().status}</AutomationPill>
                  </div>

                  <div class="mt-6 space-y-3">
                    <InspectorRow label="Category" value={policy().category} />
                    <InspectorRow label="Cadence" value={policy().cadence} />
                    <InspectorRow label="Target" value={policy().target} />
                    <InspectorRow label="Last Run" value={policy().lastRun ?? 'Not yet executed'} />
                    <InspectorRow label="Next Run" value={policy().nextRun ?? 'Pending activation'} />
                  </div>

                  <div class="mt-6 rounded-sm border border-gray-100 bg-gray-50 p-4 text-sm text-gray-600 dark:border-gray-800 dark:bg-zinc-900 dark:text-gray-300">
                    This first implementation is a control surface for Orion-native automation ideas. The next backend step would be persisting these policies and attaching a scheduler so they can execute refresh, assess, cleanup, and scale workflows automatically.
                  </div>

                  <div class="mt-6 flex gap-3">
                    <button class="flex-1 rounded-sm border border-blue-200 bg-blue-50 px-4 py-3 text-sm font-semibold text-blue-600 transition-colors hover:border-blue-300 hover:bg-blue-100 dark:border-blue-900/60 dark:bg-blue-950/30 dark:text-blue-400 dark:hover:border-blue-800 dark:hover:bg-blue-950/50">
                      Activate Policy
                    </button>
                    <button class="flex-1 rounded-sm border border-gray-200 px-4 py-3 text-sm font-semibold text-gray-600 transition-colors hover:border-gray-300 hover:bg-gray-50 dark:border-gray-800 dark:text-gray-300 dark:hover:border-gray-700 dark:hover:bg-zinc-900">
                      Customize
                    </button>
                  </div>
                </>
              )}
            </Show>
          </div>

          <div class="professional-card">
            <div class="text-[10px] font-bold uppercase tracking-[0.22em] text-gray-400 dark:text-gray-500">Recent Automation Activity</div>
            <div class="mt-5 space-y-3">
              <For each={recentRuns()}>
                {(run) => (
                  <div class="rounded-sm border border-gray-100 bg-gray-50 px-4 py-4 dark:border-gray-800 dark:bg-zinc-900">
                    <div class="flex items-center justify-between gap-4">
                      <div>
                        <div class="font-semibold text-gray-900 dark:text-gray-100">{run.name}</div>
                        <div class="mt-1 text-sm text-gray-500 dark:text-gray-400">{run.result}</div>
                      </div>
                      <AutomationPill tone={run.tone}>{run.time}</AutomationPill>
                    </div>
                  </div>
                )}
              </For>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
};

const AutomationStat: Component<{ label: string; value: string | number; detail: string }> = (props) => (
  <div class="professional-card !p-6">
    <div class="text-[10px] font-bold uppercase tracking-[0.18em] text-gray-400 dark:text-gray-500">{props.label}</div>
    <div class="mt-4 text-4xl font-bold tracking-tight text-gray-900 dark:text-white">{props.value}</div>
    <div class="mt-3 text-sm text-gray-500 dark:text-gray-400">{props.detail}</div>
  </div>
);

const PolicyField: Component<{ label: string; value: string }> = (props) => (
  <div class="rounded-sm border border-gray-100 bg-gray-50 px-3 py-3 dark:border-gray-800 dark:bg-zinc-900">
    <div class="text-[10px] font-bold uppercase tracking-[0.16em] text-gray-400 dark:text-gray-500">{props.label}</div>
    <div class="mt-2 text-sm font-semibold text-gray-900 dark:text-gray-100">{props.value}</div>
  </div>
);

const InspectorRow: Component<{ label: string; value: string }> = (props) => (
  <div class="flex items-center justify-between rounded-sm border border-gray-100 bg-gray-50 px-4 py-3 dark:border-gray-800 dark:bg-zinc-900">
    <span class="text-sm text-gray-500 dark:text-gray-400">{props.label}</span>
    <span class="text-sm font-semibold text-gray-900 dark:text-gray-100">{props.value}</span>
  </div>
);

const AutomationPill: Component<{ tone: AutomationTone; children: string }> = (props) => (
  <span class={`inline-flex rounded-full px-2.5 py-1 text-[10px] font-bold uppercase tracking-[0.18em] ${
    props.tone === 'emerald'
      ? 'bg-emerald-100 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300'
      : props.tone === 'amber'
        ? 'bg-amber-100 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300'
        : props.tone === 'violet'
          ? 'bg-violet-100 text-violet-700 dark:bg-violet-950/40 dark:text-violet-300'
          : 'bg-blue-100 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300'
  }`}>
    {props.children}
  </span>
);

export default Automations;
