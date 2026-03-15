import { Component } from 'solid-js';
import { Router, Route } from "@solidjs/router";
import Sidebar from './components/Sidebar';
import Home from './pages/Home';
import Apps from './pages/Apps';
import Infrastructure from './pages/Infrastructure';
import AppDetails from './pages/AppDetails';
import { AuthProvider, useAuth } from './contexts/AuthContext';
import { ThemeProvider, useTheme } from './contexts/ThemeContext';
import { Show } from 'solid-js';

const Layout: Component<{ children?: any }> = (props) => {
  return (
    <div class="min-h-screen bg-gray-50 dark:bg-black text-gray-900 dark:text-gray-100 font-sans selection:bg-blue-100 transition-colors duration-300">
      <Sidebar />
      <main class="ml-64 p-8 min-h-screen relative z-10">
        <div class="max-w-full mx-auto">
          {props.children}
        </div>
      </main>
    </div>
  );
};

const AppContent: Component = () => {
  const auth = useAuth();
  const { theme } = useTheme();

  return (
    <Show
      when={!auth.loading()}
      fallback={<div class="flex items-center justify-center min-h-screen bg-gray-50 dark:bg-black font-bold text-blue-600 animate-pulse uppercase tracking-widest text-xs">Initializing Orion Control Plane...</div>}
    >
      <Show
        when={auth.user()?.isAuthenticated}
        fallback={
          <div class="flex flex-col items-center justify-center min-h-screen bg-gray-50 dark:bg-black p-12 text-center transition-colors duration-300">
            <div class="w-16 h-16 bg-blue-600 rounded-2xl shadow-xl shadow-blue-200 dark:shadow-blue-900/20 mb-8 flex items-center justify-center">
                <div class="w-6 h-6 bg-white rounded-md"></div>
            </div>
            <h1 class="text-4xl font-black text-gray-900 dark:text-white tracking-tighter mb-4">Orion</h1>
            <p class="text-gray-500 dark:text-gray-400 font-bold text-xs max-w-md mb-12 uppercase tracking-[0.3em] opacity-60 italic">Hybrid Cloud Orchestration</p>
            <button
              onClick={() => auth.login()}
              class="px-10 py-4 bg-blue-600 text-white font-bold text-sm uppercase tracking-widest hover:bg-blue-700 active:bg-blue-800 transition-all shadow-lg shadow-blue-200 rounded-xl"
            >
              Sign In
            </button>
          </div>
        }
      >
        <Router root={Layout}>
          <Route path="/" component={Home} />
          <Route path="/apps" component={Apps} />
          <Route path="/apps/:id" component={AppDetails} />
          <Route path="/nodes" component={Infrastructure} />
          <Route path="/workflows" component={() => <div class="p-20 text-center font-bold text-gray-400 dark:text-gray-500 uppercase tracking-widest text-xs opacity-50">Automation Engine Synchronizing...</div>} />
        </Router>
      </Show>
    </Show>
  );
};

const App: Component = () => {
  return (
    <ThemeProvider>
      <AuthProvider>
        <AppContent />
      </AuthProvider>
    </ThemeProvider>
  );
};

export default App;
