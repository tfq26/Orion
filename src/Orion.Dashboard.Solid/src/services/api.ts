export interface AppSummary {
  id: string;
  name: string;
  status: string;
  latestBuildStatus: string;
  latestBuildAt?: string;
  stability: string;
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

export interface NodeTelemetrySample {
  timestamp: string;
  cpuUsage: number;
  memoryUsagePercent: number;
  memoryUsageGb: number;
  storageUsagePercent: number;
  storageUsageGb: number;
  networkTrafficMbps: number;
}

export interface NodeTelemetrySnapshot {
  nodeName: string;
  architecture: string;
  samples: NodeTelemetrySample[];
  hourlySamples: NodeTelemetrySample[];
  dailySamples: NodeTelemetrySample[];
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

export interface Deployment {
  id: string;
  appId: string;
  ownerId: string;
  status: 'Pending' | 'Building' | 'Deploying' | 'Running' | 'Failed' | 'Paused';
  imageTag?: string;
  sourceVersion?: string;
  port?: number;
  createdAt: string;
}

export interface DeploymentAssessmentReport {
  appId: string;
  appName: string;
  stability: string;
  recommendedAction: string;
  currentReplicas: number;
  recommendedReplicas: number;
  cpuUsage: number;
  memoryUsageMb: number;
  allocatedCpuCores: number;
  allocatedMemoryMb: number;
  review: string;
  findings: string[];
}

export interface RefreshResult {
  startedBuild: boolean;
  message: string;
  latestRevision?: string;
  currentRevision?: string;
  deployment?: Deployment;
}

export interface AppLogEntry {
  id: string;
  appId?: string;
  deploymentId?: string;
  ownerId: string;
  message: string;
  level: string;
  timestamp: string;
}

export interface LocalUploadEntry {
  file: File;
  path: string;
}

const API_BASE = '/dashboard'; // This should be configured for the backend URL

export const api = {
  async getSummary(): Promise<DashboardSummary> {
    const res = await fetch(`${API_BASE}/summary`);
    if (!res.ok) throw new Error('Failed to fetch summary');
    return res.json();
  },

  async getNodeTelemetry(): Promise<NodeTelemetrySnapshot> {
    const res = await fetch(`${API_BASE}/node-telemetry`);
    if (!res.ok) throw new Error('Failed to fetch node telemetry');
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

  async createUploadedApp(name: string, files: LocalUploadEntry[], buildCommand?: string, runCommand?: string, buildFolder?: string): Promise<App> {
    const formData = new FormData();
    formData.append('name', name);
    formData.append('buildCommand', buildCommand ?? '');
    formData.append('runCommand', runCommand ?? '');
    formData.append('buildFolder', buildFolder || 'dist');

    files.forEach((entry) => {
      formData.append('files', entry.file);
      formData.append('paths', entry.path);
    });

    const res = await fetch('/apps/upload', {
      method: 'POST',
      body: formData
    });
    if (!res.ok) throw new Error(await res.text() || 'Failed to upload local app');

    const app = await res.json();
    return {
      ...app,
      url: `https://${name.toLowerCase().replace(/\s+/g, '-')}.orion.run`,
      buildCommand: buildCommand ?? undefined,
      runCommand: runCommand ?? undefined,
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

  async deleteApp(id: string): Promise<void> {
    const res = await fetch(`/apps/${id}`, {
      method: 'DELETE'
    });
    if (!res.ok) {
      const message = await res.text();
      throw new Error(message || 'Failed to delete app');
    }
  },

  async triggerBuild(appId: string): Promise<Deployment> {
    const res = await fetch(`/apps/${appId}/build`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to trigger build');
    return res.json();
  },

  async pauseDeployment(appId: string): Promise<{ paused: boolean; message: string; stoppedReplicas: number }> {
    const res = await fetch(`/apps/${appId}/pause`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to pause deployment');
    return res.json();
  },

  async refreshDeployment(appId: string): Promise<RefreshResult> {
    const res = await fetch(`/apps/${appId}/refresh`, { method: 'POST' });
    if (!res.ok) throw new Error('Failed to refresh deployment');
    return res.json();
  },

  async assessDeployment(appId: string): Promise<DeploymentAssessmentReport> {
    const res = await fetch(`/apps/${appId}/assess`);
    if (!res.ok) throw new Error('Failed to assess deployment');
    return res.json();
  },

  async getDeployments(appId: string): Promise<Deployment[]> {
    const res = await fetch(`/apps/${appId}/deployments`);
    if (!res.ok) throw new Error('Failed to fetch deployments');
    return res.json();
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

  streamAppLogs(id: string, onMessage: (log: AppLogEntry) => void): () => void {
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
