import { Component, createSignal, Switch, Match, Show, For } from 'solid-js';
import { api, type LocalUploadEntry } from '../services/api';
import { OrionButton } from './UI';
import { DirectoryPickerDialog } from './DirectoryPicker';

interface DeploymentWizardProps {
  onClose: () => void;
  onSuccess: () => void;
}

const DeploymentWizard: Component<DeploymentWizardProps> = (props) => {
  const [step, setStep] = createSignal(1);
  const [appName, setAppName] = createSignal('');
  const [sourceMode, setSourceMode] = createSignal<'git' | 'upload'>('git');
  const [repoUrl, setRepoUrl] = createSignal('');
  const [localFiles, setLocalFiles] = createSignal<LocalUploadEntry[]>([]);
  const [buildCommand, setBuildCommand] = createSignal('npm run build');
  const [runCommand, setRunCommand] = createSignal('npm start');
  const [buildFolder, setBuildFolder] = createSignal('dist');
  const [finalUrl, setFinalUrl] = createSignal('');
  const [isDeploying, setIsDeploying] = createSignal(false);
  const [error, setError] = createSignal<string | null>(null);
  const [showPicker, setShowPicker] = createSignal(false);
  let fallbackInput: HTMLInputElement | undefined;

  const inferUploadDefaults = (entries: LocalUploadEntry[]) => {
    const paths = entries.map((entry) => entry.path);
    const hasPackageJson = paths.some((path) => path === 'package.json' || path.endsWith('/package.json'));
    const hasPublicFolder = paths.some((path) => path.startsWith('public/'));
    const hasBuildFolder = paths.some((path) => path.startsWith('dist/') || path.startsWith('build/'));

    if (!hasPackageJson) {
      setBuildCommand('');
      setRunCommand('');
    }

    if (hasPublicFolder) {
      setBuildFolder('public');
    } else if (hasBuildFolder && paths.some((path) => path.startsWith('dist/'))) {
      setBuildFolder('dist');
    } else if (hasBuildFolder) {
      setBuildFolder('build');
    }
  };

  const handleLocalFileSelection = (files: FileList | null) => {
    const entries = Array.from(files || []).map((file) => ({
      file,
      path: (file as File & { webkitRelativePath?: string }).webkitRelativePath || file.name
    }));
    setLocalFiles(entries);
    inferUploadDefaults(entries);
  };

  const handleChooseFolder = async () => {
    if (!(window as Window & { showDirectoryPicker?: () => Promise<FileSystemDirectoryHandle> }).showDirectoryPicker) {
      fallbackInput?.click();
      return;
    }

    try {
      const dirHandle = await (window as Window & { showDirectoryPicker: () => Promise<FileSystemDirectoryHandle> }).showDirectoryPicker();
      const entries: LocalUploadEntry[] = [];

      const walk = async (handle: FileSystemDirectoryHandle, prefix = ''): Promise<void> => {
        for await (const [, childHandle] of handle.entries()) {
          if (childHandle.kind === 'file') {
            const file = await childHandle.getFile();
            entries.push({
              file,
              path: prefix ? `${prefix}/${file.name}` : file.name
            });
          } else {
            await walk(childHandle, prefix ? `${prefix}/${childHandle.name}` : childHandle.name);
          }
        }
      };

      await walk(dirHandle);
      setLocalFiles(entries);
      inferUploadDefaults(entries);
    } catch (err) {
      if ((err as DOMException)?.name !== 'AbortError') {
        setError(err instanceof Error ? err.message : 'Failed to read local folder.');
      }
    }
  };

  const handleDeploy = async () => {
    setIsDeploying(true);
    setError(null);
    try {
      const app = sourceMode() === 'upload'
        ? await api.createUploadedApp(appName(), localFiles(), buildCommand(), runCommand(), buildFolder())
        : await api.createApp(appName(), repoUrl(), buildCommand(), runCommand(), buildFolder());
      setFinalUrl(app.url || '');
      await api.triggerBuild(app.id);
      setStep(5); // Success step
      setTimeout(() => {
        // Leave it open for the user to see the link
      }, 5000);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Deployment failed');
      setIsDeploying(false);
    }
  };

  return (
    <div class="fixed left-64 inset-y-0 right-0 z-50 flex flex-col bg-gray-50 dark:bg-black animate-in fade-in duration-300 overflow-hidden text-gray-900 dark:text-gray-100 transition-colors duration-300">
      {/* Professional Header */}
      <header class="relative z-10 p-8 flex justify-between items-center border-b border-gray-100 dark:border-gray-800 bg-white dark:bg-zinc-900/50 shadow-sm transition-colors duration-300">
        <div class="flex items-center gap-4">
          <div class="w-10 h-10 bg-blue-600 rounded-sm shadow-lg shadow-blue-200 dark:shadow-blue-900/40 flex items-center justify-center text-white font-bold text-xl">O</div>
          <div>
            <h2 class="text-xl font-bold tracking-tight text-gray-900 dark:text-white">Deploy Application</h2>
            <p class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mt-0.5">Step {step()} of 4</p>
          </div>
        </div>
        
        <div class="flex items-center gap-8">
           <div class="flex gap-2">
             <For each={[1, 2, 3, 4]}>
               {(s: number) => (
                 <div 
                   class={`h-1.5 w-12 rounded-sm transition-all duration-300 ${step() >= s ? 'bg-blue-600 shadow-sm shadow-blue-300 dark:shadow-blue-900/40' : 'bg-gray-200 dark:bg-zinc-800'}`}
                 />
               )}
             </For>
           </div>
           <button 
              onClick={props.onClose}
              class="p-2 hover:bg-gray-100 dark:hover:bg-zinc-800 rounded-sm transition-colors group"
            >
              <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" class="text-gray-400 group-hover:text-gray-900 dark:group-hover:text-white transition-colors">
                <path d="M18 6L6 18M6 6l12 12" />
              </svg>
            </button>
        </div>
      </header>

      {/* Main Content Area */}
      <main class="relative z-10 flex-grow flex items-center justify-center p-12 overflow-hidden">
        <div class="w-full max-w-xl">
          <Switch>
            <Match when={step() === 1}>
              <div class="animate-in slide-in-from-bottom fade-in duration-400">
                <label class="text-[10px] font-bold text-blue-600 dark:text-blue-400 uppercase tracking-widest mb-3 block">Application Context</label>
                <h1 class="text-3xl font-bold text-gray-900 dark:text-white tracking-tighter mb-8">What is your application called?</h1>
                <input 
                  type="text" 
                  placeholder="e.g. My Scaleable App"
                  class="w-full bg-white dark:bg-zinc-900 border border-gray-200 dark:border-gray-800 rounded-sm px-6 py-5 text-xl font-semibold text-gray-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-4 focus:ring-blue-50/50 dark:focus:ring-blue-900/20 outline-none transition-all placeholder:text-gray-300 dark:placeholder:text-gray-700"
                  value={appName()}
                  onInput={(e) => setAppName(e.currentTarget.value)}
                  autofocus
                />
              </div>
            </Match>

            <Match when={step() === 2}>
              <div class="animate-in slide-in-from-bottom fade-in duration-400">
                <label class="text-[10px] font-bold text-blue-600 dark:text-blue-400 uppercase tracking-widest mb-3 block">Deployment Source</label>
                <h1 class="text-3xl font-bold text-gray-900 dark:text-white tracking-tighter mb-8">Choose where Orion should pull the source from.</h1>
                <div class="grid grid-cols-2 gap-4 mb-6">
                  <button
                    class={`rounded-sm border px-5 py-5 text-left transition-all ${sourceMode() === 'git' ? 'border-blue-300 bg-blue-50 dark:border-blue-800 dark:bg-blue-950/20' : 'border-gray-200 bg-white dark:border-gray-800 dark:bg-zinc-900'}`}
                    onClick={() => setSourceMode('git')}
                  >
                    <div class="text-sm font-bold text-gray-900 dark:text-white">Git Repository</div>
                    <div class="mt-2 text-xs text-gray-500 dark:text-gray-400">Pull from GitHub or any reachable git remote.</div>
                  </button>
                  <button
                    class={`rounded-sm border px-5 py-5 text-left transition-all ${sourceMode() === 'upload' ? 'border-blue-300 bg-blue-50 dark:border-blue-800 dark:bg-blue-950/20' : 'border-gray-200 bg-white dark:border-gray-800 dark:bg-zinc-900'}`}
                    onClick={() => setSourceMode('upload')}
                  >
                    <div class="text-sm font-bold text-gray-900 dark:text-white">Upload From Device</div>
                    <div class="mt-2 text-xs text-gray-500 dark:text-gray-400">Deploy a local folder directly for an offline-first workflow.</div>
                  </button>
                </div>

                <Show when={sourceMode() === 'git'} fallback={
                  <div class="rounded-sm border border-dashed border-gray-300 bg-white p-6 dark:border-gray-700 dark:bg-zinc-900">
                    <label class="mb-4 block text-[10px] font-bold uppercase tracking-widest text-gray-400 dark:text-gray-500">Local Source Folder</label>
                    <div class="flex items-center gap-3">
                      <OrionButton variant="primary" onclick={handleChooseFolder}>
                        Choose Folder
                      </OrionButton>
                      <button
                        type="button"
                        class="rounded-sm border border-gray-200 px-4 py-3 text-sm font-semibold text-gray-500 transition-colors hover:border-blue-200 hover:text-blue-600 dark:border-gray-800 dark:text-gray-300 dark:hover:border-blue-900 dark:hover:text-blue-400"
                        onClick={() => fallbackInput?.click()}
                      >
                        Browser Fallback
                      </button>
                    </div>
                    <input
                      ref={(el) => {
                        fallbackInput = el;
                        el.setAttribute('webkitdirectory', '');
                        el.setAttribute('directory', '');
                      }}
                      type="file"
                      multiple
                      class="hidden"
                      onChange={(e) => handleLocalFileSelection(e.currentTarget.files)}
                    />
                    <div class="mt-4 text-xs text-gray-500 dark:text-gray-400">
                      {localFiles().length > 0
                        ? `${localFiles().length} file${localFiles().length === 1 ? '' : 's'} selected from your local folder.`
                        : 'Choose a folder like `showcase/` and Orion will upload it directly from this device.'}
                    </div>
                  </div>
                }>
                  <input 
                    type="text" 
                    placeholder="https://github.com/user/repo"
                    class="w-full bg-white dark:bg-zinc-900 border border-gray-200 dark:border-gray-800 rounded-sm px-6 py-5 text-xl font-semibold text-gray-900 dark:text-white shadow-sm focus:border-blue-500 focus:ring-4 focus:ring-blue-50/50 dark:focus:ring-blue-900/20 outline-none transition-all placeholder:text-gray-300 dark:placeholder:text-gray-700"
                    value={repoUrl()}
                    onInput={(e) => setRepoUrl(e.currentTarget.value)}
                  />
                </Show>
              </div>
            </Match>

            <Match when={step() === 3}>
              <div class="animate-in slide-in-from-bottom fade-in duration-400">
                <label class="text-[10px] font-bold text-blue-600 dark:text-blue-400 uppercase tracking-widest mb-3 block">Build Configuration</label>
                <h1 class="text-3xl font-bold text-gray-900 dark:text-white tracking-tighter mb-8">Configure build & run commands.</h1>
                <div class="space-y-6">
                  <div>
                    <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-2 block">Build Command</label>
                    <input 
                      type="text" 
                      class="w-full bg-white dark:bg-zinc-900 border border-gray-200 dark:border-gray-800 rounded-sm px-4 py-3 text-sm font-semibold text-gray-900 dark:text-white focus:border-blue-500 outline-none transition-all"
                      value={buildCommand()}
                      onInput={(e) => setBuildCommand(e.currentTarget.value)}
                    />
                  </div>
                  <div>
                    <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-2 block">Run Command</label>
                    <input 
                      type="text" 
                      class="w-full bg-white dark:bg-zinc-900 border border-gray-200 dark:border-gray-800 rounded-sm px-4 py-3 text-sm font-semibold text-gray-900 dark:text-white focus:border-blue-500 outline-none transition-all"
                      value={runCommand()}
                      onInput={(e) => setRunCommand(e.currentTarget.value)}
                    />
                  </div>
                  <div>
                    <label class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-2 block">Build Folder (Output)</label>
                    <div class="flex gap-2">
                      <input 
                        type="text" 
                        placeholder="e.g. dist, build, out"
                        class="flex-1 bg-white dark:bg-zinc-900 border border-gray-200 dark:border-gray-800 rounded-sm px-4 py-3 text-sm font-semibold text-gray-900 dark:text-white focus:border-blue-500 outline-none transition-all"
                        value={buildFolder()}
                        onInput={(e) => setBuildFolder(e.currentTarget.value)}
                      />
                      <Show when={sourceMode() === 'git'}>
                        <OrionButton variant="ghost" onclick={() => setShowPicker(true)}>
                          <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
                            <path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                          </svg>
                        </OrionButton>
                      </Show>
                    </div>
                  </div>
                </div>
              </div>
            </Match>

            <Match when={step() === 4}>
              <div class="animate-in slide-in-from-bottom fade-in duration-400">
                <label class="text-[10px] font-bold text-blue-600 dark:text-blue-400 uppercase tracking-widest mb-3 block">Final Verification</label>
                <h1 class="text-3xl font-bold text-gray-900 dark:text-white tracking-tighter mb-10">Review your configuration.</h1>
                
                <div class="space-y-4 bg-white dark:bg-zinc-900 p-8 rounded-sm border border-gray-100 dark:border-gray-800 shadow-md">
                  <div class="flex justify-between items-center pb-4 border-b border-gray-50 dark:border-gray-800/50">
                    <span class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Application Name</span>
                    <span class="text-lg font-bold text-gray-900 dark:text-white">{appName()}</span>
                  </div>
                  <div class="flex justify-between items-center pb-4 border-b border-gray-50 dark:border-gray-800/50">
                    <span class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Source</span>
                    <span class="text-sm font-semibold text-blue-600 dark:text-blue-400 truncate max-w-[260px]">
                      {sourceMode() === 'upload' ? `${localFiles().length} local file${localFiles().length === 1 ? '' : 's'}` : repoUrl()}
                    </span>
                  </div>
                  <div class="flex justify-between items-center pb-4 border-b border-gray-50 dark:border-gray-800/50">
                    <span class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Build Command</span>
                    <span class="text-sm font-mono font-bold text-gray-700 dark:text-gray-300">{buildCommand()}</span>
                  </div>
                  <div class="flex justify-between items-center pb-4 border-b border-gray-50 dark:border-gray-800/50">
                    <span class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Run Command</span>
                    <span class="text-sm font-mono font-bold text-gray-700 dark:text-gray-300">{runCommand()}</span>
                  </div>
                  <div class="flex justify-between items-center">
                    <span class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest">Build Folder</span>
                    <span class="text-sm font-mono font-bold text-gray-700 dark:text-gray-300">{buildFolder()}</span>
                  </div>
                </div>

                <Show when={error()}>
                  <div class="mt-8 p-4 bg-red-50 dark:bg-red-900/10 border border-red-100 dark:border-red-900/20 text-red-600 dark:text-red-400 rounded-sm text-xs font-bold uppercase tracking-wide">
                    {error()}
                  </div>
                </Show>
              </div>
            </Match>

            <Match when={step() === 5}>
              <div class="text-center animate-in zoom-in-95 fade-in duration-600">
                <div class="w-24 h-24 bg-emerald-50 dark:bg-emerald-900/10 border-2 border-emerald-100 dark:border-emerald-900/20 rounded-sm flex items-center justify-center mx-auto mb-8 shadow-xl shadow-emerald-100 dark:shadow-emerald-900/20">
                   <svg width="40" height="40" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3" class="text-emerald-500">
                     <polyline points="20 6 9 17 4 12"/>
                   </svg>
                </div>
                <h3 class="text-3xl font-bold text-gray-900 dark:text-white tracking-tighter mb-2">Deployment Success</h3>
                <p class="text-gray-500 dark:text-gray-400 font-medium max-w-sm mx-auto mb-8 transition-colors">Your application is live and accessible at the following URL.</p>
                
                <a 
                  href={finalUrl()} 
                  target="_blank" 
                  class="inline-flex items-center gap-3 bg-blue-600 hover:bg-blue-700 text-white px-8 py-4 rounded-sm font-bold text-lg shadow-lg shadow-blue-200 transition-all group"
                >
                  {finalUrl()}
                  <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="transition-transform group-hover:translate-x-1">
                    <path d="M5 12h14M12 5l7 7-7 7"/>
                  </svg>
                </a>
                <div class="mt-8">
                  <button onClick={props.onClose} class="text-gray-400 dark:text-gray-500 font-bold text-xs uppercase tracking-widest hover:text-gray-900 dark:hover:text-white transition-colors">Return to Dashboard</button>
                </div>
              </div>
            </Match>
          </Switch>
        </div>
      </main>

      {/* Footer Navigation */}
      <footer class="relative z-10 p-8 border-t border-gray-100 dark:border-gray-800 bg-white dark:bg-zinc-900 text-gray-900 dark:text-gray-100 transition-colors duration-300">
        <Show when={step() < 5}>
          <div class="flex justify-between items-center max-w-xl mx-auto w-full">
            <Show 
              when={step() > 1} 
              fallback={<div />}
            >
              <button 
                onclick={() => setStep(step() - 1)}
                class="text-gray-400 hover:text-gray-900 dark:hover:text-white font-bold text-xs uppercase tracking-widest transition-all flex items-center gap-2 group"
              >
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3">
                  <path d="M19 12H5M12 19l-7-7 7-7"/>
                </svg>
                Go Back
              </button>
            </Show>

            <OrionButton 
              variant="primary"
              onclick={step() === 4 ? handleDeploy : () => setStep(step() + 1)}
              class="!px-12 !py-4 !text-base !rounded-sm shadow-lg shadow-blue-100 dark:shadow-blue-900/20"
              disabled={
                (step() === 1 && !appName().trim()) ||
                (step() === 2 && ((sourceMode() === 'git' && !repoUrl().trim()) || (sourceMode() === 'upload' && localFiles().length === 0))) ||
                isDeploying()
              }
            >
              <Show when={isDeploying()} fallback={step() === 4 ? 'Start Deployment' : 'Continue Step'}>
                Deploying...
              </Show>
            </OrionButton>
          </div>
        </Show>
      </footer>

      <Show when={showPicker()}>
        <DirectoryPickerDialog 
          repoUrl={repoUrl()} 
          onSelect={(path) => setBuildFolder(path)}
          onClose={() => setShowPicker(false)}
        />
      </Show>
    </div>
  );
};

export default DeploymentWizard;
