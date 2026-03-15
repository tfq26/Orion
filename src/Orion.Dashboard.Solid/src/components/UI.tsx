import type { Component, JSX } from 'solid-js';

export const OrionCard: Component<{ children: JSX.Element; class?: string; title?: string }> = (props) => {
  return (
    <div class={`professional-card ${props.class || ''} transition-colors duration-300`}>
      {props.title && (
        <h3 class="text-xs font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest mb-4 border-b border-gray-100 dark:border-gray-800 pb-3">
          {props.title}
        </h3>
      )}
      {props.children}
    </div>
  );
};

export const OrionButton: Component<{ variant?: 'primary' | 'ghost'; children: JSX.Element; onclick?: () => void; class?: string; disabled?: boolean }> = (props) => {
  const styles = props.variant === 'primary' ? 'btn-primary' : 'btn-ghost';

  return (
    <button
      class={`${styles} ${props.class || ''} transition-colors duration-300 ${props.disabled ? 'opacity-50 cursor-not-allowed pointer-events-none' : ''}`}
      onClick={props.onclick}
      disabled={props.disabled}
    >
      {props.children}
    </button>
  );
};

export const OrionBadge: Component<{ status: 'success' | 'default'; children: JSX.Element }> = (props) => {
  const colorStyles = props.status === 'success' 
    ? 'bg-emerald-50 dark:bg-emerald-950/30 text-emerald-700 dark:text-emerald-400 border-emerald-200 dark:border-emerald-800/50' 
    : 'bg-gray-100 dark:bg-zinc-800 text-gray-700 dark:text-gray-400 border-gray-200 dark:border-zinc-700';
    
  return (
    <div class={`inline-flex items-center gap-1.5 px-2.5 py-1 rounded-sm text-[11px] font-bold border transition-colors duration-300 ${colorStyles}`}>
      <div class={`w-1.5 h-1.5 rounded-full ${props.status === 'success' ? 'bg-emerald-500 shadow-sm shadow-emerald-500/50' : 'bg-gray-400 dark:bg-zinc-500'}`}></div>
      {props.children}
    </div>
  );
};
