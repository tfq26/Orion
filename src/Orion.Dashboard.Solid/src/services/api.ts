export interface AppSummary {
  id: string;
  name: string;
  status: string;
  cpuUsage: number;
  memoryUsageMb: number;
  activeReplicas: number;
}

export interface DashboardSummary {
  totalApps: number;
  connectedPeers: number;
  pilotStatus: string;
  apps: AppSummary[];
}

export interface App {
  id: string;
  name: string;
  repoUrl: string;
  ownerId: string;
  status: string;
  buildCommand?: string;
  runCommand?: string;
  buildFolder?: string;
  url?: string;
}

const API_BASE = '/dashboard'; // This should be configured for the backend URL

export const api = {
  async getSummary(): Promise<DashboardSummary> {
    const res = await fetch(`${API_BASE}/summary`);
    if (!res.ok) throw new Error('Failed to fetch summary');
    return res.json();
  },

  async scaleApp(id: string, replicas: number): Promise<void> {
    const res = await fetch(`/apps/${id}/scale?replicas=${replicas}`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to scale app');
  },

  async createApp(name: string, repoUrl: string, buildCommand?: string, runCommand?: string, buildFolder?: string): Promise<App> {
    const res = await fetch('/apps', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ 
        name, 
        repoUrl,
        buildCommand: buildCommand || 'npm run build',
        runCommand: runCommand || 'npm start',
        buildFolder: buildFolder || 'dist'
      })
    });
    if (!res.ok) throw new Error('Failed to create app');
    
    const app = await res.json();
    // Simulate URL generation for the dashboard
    return {
      ...app,
      url: `https://${name.toLowerCase().replace(/\s+/g, '-')}.orion.run`,
      buildCommand,
      runCommand,
      buildFolder
    };
  },

  async exploreRepo(repoUrl: string): Promise<string[]> {
    const res = await fetch('/apps/explore', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ repoUrl })
    });
    if (!res.ok) throw new Error('Failed to explore repository');
    return res.json();
  },

  async updateApp(id: string, app: Partial<App>): Promise<App> {
    const res = await fetch(`/apps/${id}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(app)
    });
    if (!res.ok) throw new Error('Failed to update app');
    return res.json();
  },

  async triggerBuild(appId: string): Promise<void> {
    const res = await fetch(`/apps/${appId}/build`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to trigger build');
  },

  async getAppSecrets(id: string): Promise<Record<string, string>> {
     const res = await fetch(`/apps/${id}/secrets`);
     if (!res.ok) return {};
     return res.json();
  },

  async updateAppSecrets(id: string, secrets: Record<string, string>): Promise<void> {
    const res = await fetch(`/apps/${id}/secrets`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(secrets)
    });
    if (!res.ok) throw new Error('Failed to update secrets');
  },

  streamAppLogs(id: string, onMessage: (log: any) => void): () => void {
    const eventSource = new EventSource(`/apps/${id}/logs`);
    eventSource.onmessage = (event) => {
      try {
        const data = JSON.parse(event.data);
        onMessage(data);
      } catch (e) {
        console.error('Failed to parse log entry', e);
      }
    };
    return () => eventSource.close();
  },

  async getAppMetrics(id: string): Promise<{ timestamp: string; cpu: number; memory: number }[]> {
    const res = await fetch(`/apps/${id}/telemetry`);
    if (!res.ok) return [];
    return res.json();
  },

  async getApp(id: string): Promise<App> {
    const res = await fetch(`/apps/${id}`);
    if (!res.ok) throw new Error('Failed to fetch app');
    const app = await res.json();
    return {
      ...app,
      url: `https://${app.name.toLowerCase().replace(/\s+/g, '-')}.orion.run`
    };
  }
};
