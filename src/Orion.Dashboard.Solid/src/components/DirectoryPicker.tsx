import { Component, createSignal, createResource, For, Show, createMemo } from 'solid-js';
import { api } from '../services/api';
import { OrionButton } from './UI';

interface DirectoryPickerDialogProps {
  repoUrl: string;
  onSelect: (path: string) => void;
  onClose: () => void;
}

interface FolderNode {
  name: string;
  path: string;
  children: FolderNode[];
}

const buildTree = (paths: string[]): FolderNode[] => {
  const root: FolderNode[] = [];
  const map: Record<string, FolderNode> = {};

  paths.forEach(path => {
    const parts = path.split(/[/\\]/);
    let currentPath = '';
    let currentLevel = root;

    parts.forEach((part, index) => {
      currentPath = currentPath ? `${currentPath}/${part}` : part;
      
      if (!map[currentPath]) {
        const newNode: FolderNode = { name: part, path: currentPath, children: [] };
        map[currentPath] = newNode;
        currentLevel.push(newNode);
      }
      
      currentLevel = map[currentPath].children;
    });
  });

  return root;
};

const FolderItem: Component<{ node: FolderNode; onSelect: (path: string) => void; onConfirm: (path: string) => void; level: number }> = (props) => {
  const [isOpen, setIsOpen] = createSignal(props.level < 1); // Open first level by default

  return (
    <div>
      <div 
        class="flex items-center group hover:bg-white dark:hover:bg-zinc-800 transition-all cursor-pointer border-b border-gray-50 dark:border-gray-800/30 last:border-0"
        style={{ "padding-left": `${props.level * 1.25 + 1}rem` }}
        onClick={() => props.onSelect(props.node.path)}
        onDblClick={() => props.onConfirm(props.node.path)}
      >
        <button 
          class="p-2 text-gray-400 hover:text-gray-900 dark:hover:text-white transition-colors"
          onClick={(e) => {
            e.stopPropagation();
            setIsOpen(!isOpen());
          }}
        >
          <Show when={props.node.children.length > 0} fallback={<div class="w-4" />}>
            <svg 
              width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="3"
              class={`transition-transform duration-200 ${isOpen() ? 'rotate-90' : ''}`}
            >
              <path d="M9 5l7 7-7 7" />
            </svg>
          </Show>
        </button>
        
        <div 
          class="flex-1 py-3 flex items-center gap-3 text-sm font-medium text-gray-700 dark:text-gray-300"
        >
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="text-gray-400 group-hover:text-blue-500 transition-colors">
            <path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
          </svg>
          <span class="group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors">{props.node.name}</span>
        </div>
      </div>
      
      <Show when={isOpen() && props.node.children.length > 0}>
        <div class="animate-in slide-in-from-top-1 duration-200">
          <For each={props.node.children}>
            {(child) => <FolderItem node={child} onSelect={props.onSelect} onConfirm={props.onConfirm} level={props.level + 1} />}
          </For>
        </div>
      </Show>
    </div>
  );
};

export const DirectoryPickerDialog: Component<DirectoryPickerDialogProps> = (props) => {
  const [directories] = createResource(() => props.repoUrl, api.exploreRepo);
  const [search, setSearch] = createSignal('');

  const tree = createMemo(() => buildTree(directories() || []));

  const filteredDirs = () => {
    const list = directories() || [];
    if (!search()) return [];
    return list.filter(d => d.toLowerCase().includes(search().toLowerCase()));
  };

  return (
    <div class="fixed inset-0 z-[100] flex items-center justify-center p-6 animate-in fade-in duration-300">
      <div 
        class="absolute inset-0 bg-black/60 backdrop-blur-sm" 
        onClick={props.onClose}
      />
      <div class="relative w-full max-w-lg bg-white dark:bg-zinc-900 rounded-sm shadow-2xl overflow-hidden border border-gray-100 dark:border-gray-800 flex flex-col max-h-[80vh]">
        <header class="p-6 border-b border-gray-100 dark:border-gray-800 flex justify-between items-center bg-gray-50/50 dark:bg-zinc-900/50">
          <div>
            <h3 class="text-lg font-bold text-gray-900 dark:text-white">Select Build Folder</h3>
            <p class="text-[10px] font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mt-0.5">Exploring repository structure</p>
          </div>
          <button onClick={props.onClose} class="text-gray-400 hover:text-gray-900 dark:hover:text-white transition-colors">
            <svg width="20" height="20" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5">
              <path d="M18 6L6 18M6 6l12 12" />
            </svg>
          </button>
        </header>

        <div class="p-6 space-y-4 flex-1 flex flex-col overflow-hidden">
          <div class="relative">
            <input 
              type="text" 
              placeholder="Search directories..."
              class="w-full bg-gray-50 dark:bg-black border border-gray-100 dark:border-gray-800 rounded-sm px-4 py-2.5 pl-10 text-sm font-medium outline-none focus:border-blue-500 transition-all"
              value={search()}
              onInput={(e) => setSearch(e.currentTarget.value)}
            />
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="absolute left-3.5 top-3 text-gray-400">
              <circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/>
            </svg>
          </div>

          <div class="flex-1 overflow-y-auto border border-gray-100 dark:border-gray-800 rounded-sm bg-gray-50/50 dark:bg-black/50">
            <Show when={directories.loading}>
              <div class="p-16 text-center space-y-4">
                <div class="w-10 h-10 border-2 border-blue-600 border-t-transparent rounded-full animate-spin mx-auto shadow-sm"></div>
                <p class="text-[10px] font-bold text-gray-400 uppercase tracking-[0.2em]">Cloning Metadata...</p>
              </div>
            </Show>

            <Show when={!directories.loading}>
              <div class="divide-y divide-gray-100 dark:divide-gray-800">
                <Show when={!search()}>
                  <button 
                    class="w-full text-left p-4 px-6 text-sm font-bold text-blue-600 dark:text-blue-400 hover:bg-white dark:hover:bg-zinc-800 transition-all flex items-center gap-3 border-b border-gray-100 dark:border-gray-800"
                    onClick={() => { props.onSelect('.'); props.onClose(); }}
                  >
                    <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="opacity-70">
                      <path d="M3 9l9-7 9 7v11a2 2 0 01-2 2H5a2 2 0 01-2-2z" />
                    </svg>
                    Root Directory (.)
                  </button>
                  <div class="py-1">
                    <For each={tree()}>
                      {(node) => <FolderItem node={node} onSelect={props.onSelect} onConfirm={(p) => { props.onSelect(p); props.onClose(); }} level={0} />}
                    </For>
                  </div>
                </Show>

                <Show when={search()}>
                   <div class="p-2">
                     <For each={filteredDirs()} fallback={
                       <div class="p-12 text-center">
                         <p class="text-[10px] font-bold text-gray-400 uppercase tracking-widest">No matching folders</p>
                       </div>
                     }>
                       {(dir) => (
                         <button 
                           class="w-full text-left p-3 px-4 text-sm font-medium text-gray-700 dark:text-gray-300 hover:bg-white dark:hover:bg-zinc-800 rounded-sm transition-all flex items-center gap-3 group"
                           onClick={() => props.onSelect(dir)}
                           onDblClick={() => { props.onSelect(dir); props.onClose(); }}
                         >
                           <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5" class="text-gray-400 group-hover:text-blue-500 transition-colors">
                             <path d="M3 7v10a2 2 0 002 2h14a2 2 0 002-2V9a2 2 0 00-2-2h-6l-2-2H5a2 2 0 00-2 2z" />
                           </svg>
                           <span class="truncate">{dir}</span>
                         </button>
                       )}
                     </For>
                   </div>
                </Show>
              </div>
            </Show>
          </div>
        </div>

        <footer class="p-4 bg-gray-50 dark:bg-zinc-950/50 border-t border-gray-100 dark:border-gray-800/50 text-center">
          <p class="text-[9px] font-bold text-gray-400 uppercase tracking-widest leading-relaxed px-6">
            Tip: Use <span class="text-blue-500">Blobless Clone</span> mode ensures we only download tree metadata, not file contents.
          </p>
        </footer>
      </div>
    </div>
  );
};
