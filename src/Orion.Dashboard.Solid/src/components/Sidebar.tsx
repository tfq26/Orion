import { Component, Show } from 'solid-js';
import { A, useLocation } from "@solidjs/router";
import { useAuth } from '../contexts/AuthContext';
import { useTheme } from '../contexts/ThemeContext';

const Sidebar: Component = () => {
  const location = useLocation();
  const auth = useAuth();
  const { theme, toggleTheme } = useTheme();

  return (
    <aside class="w-64 bg-[#f9fafb] dark:bg-black border-r border-gray-200 dark:border-gray-800 flex flex-col h-screen fixed left-0 top-0 z-50 transition-colors duration-300">
      <div class="p-8 flex items-center justify-between">
        <div class="flex items-center gap-3">
          <div class="w-8 h-8 bg-blue-600 rounded-sm shadow-sm"></div>
          <span class="text-xl font-bold tracking-tight text-gray-900 dark:text-white">Orion</span>
        </div>
        <button 
          onClick={toggleTheme}
          class="p-2 hover:bg-gray-200 dark:hover:bg-gray-800 rounded-sm transition-colors text-gray-500 dark:text-gray-400"
          title={`Switch to ${theme() === 'light' ? 'dark' : 'light'} mode`}
        >
          <Show when={theme() === 'light'} fallback={
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M12 3v1m0 16v1m9-9h-1M4 12H3m15.364-6.364l-.707.707M6.343 17.657l-.707.707m12.728 0l-.707-.707M6.343 6.343l-.707-.707M12 8a4 4 0 100 8 4 4 0 000-8z"/></svg>
          }>
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path d="M21 12.79A9 9 0 1111.21 3 7 7 0 0021 12.79z"/></svg>
          </Show>
        </button>
      </div>

      <nav class="flex-1 px-4 mt-4 space-y-1">
        <NavItem href="/" label="Control Plane" active={location.pathname === '/'} />
        <NavItem href="/apps" label="Applications" active={location.pathname === '/apps' || location.pathname.startsWith('/apps/')} />
        <NavItem href="/nodes" label="Infrastructure" active={location.pathname === '/nodes'} />
        <NavItem href="/workflows" label="Automations" active={location.pathname === '/workflows'} />
      </nav>

      <div class="p-4 border-t border-gray-200 dark:border-gray-800">
        <button
          onClick={() => auth.logout()}
          class="w-full p-2 hover:bg-white dark:hover:bg-zinc-900 hover:shadow-sm border border-transparent hover:border-gray-200 dark:hover:border-gray-800 rounded-sm transition-all text-left group"
        >
          <div class="flex items-center gap-3">
            <Show
              when={auth.user()?.picture}
              fallback={
                <div class="w-9 h-9 bg-gray-200 dark:bg-zinc-800 rounded-full flex items-center justify-center text-blue-600 dark:text-blue-400 font-bold text-sm">
                  {auth.user()?.name?.[0] || 'U'}
                </div>
              }
            >
              <img src={auth.user()?.picture} class="w-9 h-9 rounded-full border border-gray-200 dark:border-gray-800" alt="avatar" />
            </Show>
            <div class="flex flex-col min-w-0">
              <span class="text-sm font-semibold text-gray-900 dark:text-gray-100 truncate">
                {auth.user()?.name || 'User'}
              </span>
              <span class="text-[11px] font-medium text-gray-500 dark:text-gray-400 group-hover:text-blue-600 dark:group-hover:text-blue-400 transition-colors uppercase tracking-wider">
                Sign Out
              </span>
            </div>
          </div>
        </button>
      </div>
    </aside>
  );
};

const NavItem: Component<{ href: string; label: string; active: boolean }> = (props) => {
  return (
    <A
      href={props.href}
      class={`
        group flex items-center gap-3 px-4 py-2.5 rounded-sm transition-all duration-200
        ${props.active
          ? 'bg-blue-600/10 dark:bg-blue-400/10 text-blue-600 dark:text-blue-400 font-bold shadow-sm'
          : 'text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-white hover:bg-white dark:hover:bg-zinc-900 hover:shadow-sm'}
      `}
    >
      <span class="text-sm">
        {props.label}
      </span>
    </A>
  );
};

export default Sidebar;
