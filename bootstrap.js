const { spawn, execSync } = require('child_process');
const fs = require('fs');
const path = require('path');
const net = require('net');

const PORT = 5031;
const DASHBOARD_PORT = 3000;
const TMP_DIR = path.join(__dirname, 'orion_tmp');
const args = process.argv.slice(2);
const useDesktopShell = args.includes('--desktop');
const SHUTDOWN_TIMEOUT_MS = 5000;

function cleanupPort(port) {
    console.log(`[BOOTSTRAP] Cleaning up port ${port}...`);
    try {
        if (process.platform === 'win32') {
            const output = execSync(`netstat -ano | findstr :${port}`).toString();
            const lines = output.split('\n');
            for (const line of lines) {
                if (line.includes('LISTENING')) {
                    const parts = line.trim().split(/\s+/);
                    const pid = parts[parts.length - 1];
                    if (pid && pid !== '0') {
                        console.log(`[BOOTSTRAP] Killing Windows process ${pid} using port ${port}...`);
                        execSync(`taskkill /F /PID ${pid}`);
                    }
                }
            }
        } else {
            const pids = execSync(`lsof -t -i :${port}`).toString().trim().split('\n');
            for (const pid of pids) {
                if (pid) {
                    console.log(`[BOOTSTRAP] Killing process ${pid} using port ${port}...`);
                    execSync(`kill -9 ${pid}`);
                }
            }
        }
    } catch (e) {
        // Command returns exit code 1 if no process found
    }
}

function waitForPort(port, timeout = 30000) {
    return new Promise((resolve) => {
        const start = Date.now();
        const check = () => {
            if (Date.now() - start > timeout) {
                console.log(`[BOOTSTRAP] Timeout waiting for port ${port}`);
                resolve(false);
                return;
            }
            const client = new net.Socket();
            client.setTimeout(1000);
            client.on('connect', () => {
                client.destroy();
                resolve(true);
            });
            client.on('error', () => {
                client.destroy();
                setTimeout(check, 1000);
            });
            client.on('timeout', () => {
                client.destroy();
                setTimeout(check, 1000);
            });
            client.connect(port, '127.0.0.1');
        };
        check();
    });
}

function ensureDirectories() {
    console.log('[BOOTSTRAP] Ensuring environment directories...');
    if (!fs.existsSync(TMP_DIR)) {
        fs.mkdirSync(TMP_DIR, { recursive: true });
    }
    const wasmStore = path.join(__dirname, 'src', 'Orion.Api', 'bin', 'Debug', 'net9.0', 'wasm-store');
    if (!fs.existsSync(wasmStore)) {
        fs.mkdirSync(wasmStore, { recursive: true });
    }
}

function delay(ms) {
    return new Promise((resolve) => setTimeout(resolve, ms));
}

function isChildRunning(child) {
    return Boolean(child && child.pid && child.exitCode === null && !child.killed);
}

function signalChild(child, signal = 'SIGTERM') {
    if (!isChildRunning(child)) {
        return;
    }

    try {
        if (process.platform === 'win32') {
            const winSignal = signal === 'SIGKILL' ? '/F' : '';
            execSync(`taskkill ${winSignal} /T /PID ${child.pid}`.trim(), { stdio: 'ignore' });
            return;
        }

        if (child.pid) {
            process.kill(child.pid, signal);
        }
    } catch (e) {
        // Ignore shutdown races where the child already exited.
    }
}

async function terminateChild(child, name, reason) {
    if (!isChildRunning(child)) {
        return;
    }

    console.log(`[BOOTSTRAP] Stopping ${name}...`);
    
    // On Windows, if we're shutting down due to SIGINT (Ctrl+C), the terminal 
    // sends the signal to the entire process group. We should give the child 
    // processes a moment to exit gracefully from that signal before we try 
    // to forcefully kill them, which can cause race-condition errors in 
    // WebView2/Chromium (like ERROR_CLASS_DOES_NOT_EXIST).
    if (process.platform === 'win32' && reason === 'SIGINT') {
        const start = Date.now();
        // Wait up to 2 seconds for natural exit from the console signal
        while (isChildRunning(child) && Date.now() - start < 2000) {
            await delay(100);
        }
    }

    if (!isChildRunning(child)) {
        return;
    }

    signalChild(child, 'SIGTERM');

    const start = Date.now();
    while (isChildRunning(child) && Date.now() - start < SHUTDOWN_TIMEOUT_MS) {
        await delay(100);
    }

    if (isChildRunning(child)) {
        console.log(`[BOOTSTRAP] Force stopping ${name}...`);
        signalChild(child, 'SIGKILL');
    }
}

async function startSystem() {
    ensureDirectories();
    cleanupPort(5000);
    cleanupPort(5001);
    cleanupPort(PORT);
    cleanupPort(DASHBOARD_PORT);

    const commonEnv = {
        ...process.env,
        TMPDIR: TMP_DIR,
        DOTNET_NOLOGO: '1',
        DOTNET_CLI_TELEMETRY_OPTOUT: '1',
        MSBUILDDISABLENODEREUSE: '1',
        ASPNETCORE_ENVIRONMENT: 'Development'
    };

    console.log('[BOOTSTRAP] Building environment...');
    try {
        ensureDirectories();
        console.log('[BOOTSTRAP] Environment ready.');
    } catch (e) {
        console.error('[BOOTSTRAP] Error: Failed to prepare environment.', e.message);
    }

    console.log('[BOOTSTRAP] Starting Orion.Api backend...');
    console.log(`[BOOTSTRAP] API will be available at: http://localhost:${PORT}`);
    
    const api = spawn('dotnet', ['watch', '--project', 'src/Orion.Api', 'run', '--no-launch-profile', '-p:NuGetAudit=false', '--urls', `http://localhost:${PORT}`], { 
        env: commonEnv,
        stdio: 'inherit'
    });

    console.log(`[BOOTSTRAP] Waiting for backend on port ${PORT}...`);
    await waitForPort(PORT);
    console.log('[BOOTSTRAP] Backend is ready.');

    const dashboardCommand = useDesktopShell ? 'tauri:dev' : 'dev';
    const dashboardLabel = useDesktopShell ? 'Orion Desktop' : 'SolidJS Dashboard';

    console.log(`[BOOTSTRAP] Starting ${dashboardLabel}...`);
    if (useDesktopShell) {
        console.log(`[BOOTSTRAP] Desktop shell will use the dashboard dev server at: http://localhost:${DASHBOARD_PORT}`);
    } else {
        console.log(`[BOOTSTRAP] Dashboard will be available at: http://localhost:${DASHBOARD_PORT}`);
    }

    const dashboard = spawn('npm', ['run', dashboardCommand], {
        cwd: path.join(__dirname, 'src', 'Orion.Dashboard.Solid'),
        env: commonEnv,
        stdio: 'inherit',
        shell: true
    });

    let shuttingDown = false;

    const shutdown = async (reason, exitCode = 0) => {
        if (shuttingDown) {
            return;
        }

        shuttingDown = true;
        console.log(`[BOOTSTRAP] Shutting down${reason ? ` (${reason})` : ''}...`);
        
        // Use a conservative order: stop dashboard (the UI) first, 
        // then the API (the backend).
        await terminateChild(dashboard, dashboardLabel, reason);
        await terminateChild(api, 'Orion.Api', reason);

        process.exit(exitCode);
    };

    const handleExit = (code, name) => {
        if (shuttingDown) {
            return;
        }

        console.log(`[BOOTSTRAP] ${name} exited with code ${code}`);
        shutdown(`${name} exited`, code ?? 0);
    };

    api.on('close', (code) => handleExit(code, 'Orion.Api'));
    dashboard.on('close', (code) => handleExit(code, dashboardLabel));

    process.on('SIGINT', () => {
        shutdown('SIGINT', 0);
    });
    process.on('SIGTERM', () => {
        shutdown('SIGTERM', 0);
    });
}

startSystem().catch(err => {
    console.error('[BOOTSTRAP] Fatal error:', err);
    process.exit(1);
});
