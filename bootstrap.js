const { spawn, execSync } = require('child_process');
const fs = require('fs');
const path = require('path');

const PORT = 5031;
const DASHBOARD_PORT = 3000;
const TMP_DIR = path.join(__dirname, 'local_tmp');

function cleanupPort(port) {
    console.log(`[BOOTSTRAP] Cleaning up port ${port}...`);
    try {
        const pids = execSync(`lsof -t -i :${port}`).toString().trim().split('\n');
        for (const pid of pids) {
            if (pid) {
                console.log(`[BOOTSTRAP] Killing process ${pid} using port ${port}...`);
                execSync(`kill -9 ${pid}`);
            }
        }
    } catch (e) {
        // lsof returns exit code 1 if no process found
    }
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

    console.log('[BOOTSTRAP] Starting SolidJS Dashboard...');
    console.log(`[BOOTSTRAP] Dashboard will be available at: http://localhost:${DASHBOARD_PORT}`);

    const dashboard = spawn('npm', ['run', 'dev'], {
        cwd: path.join(__dirname, 'src', 'Orion.Dashboard.Solid'),
        env: commonEnv,
        stdio: 'inherit',
        shell: true
    });

    const handleExit = (code, name) => {
        console.log(`[BOOTSTRAP] ${name} exited with code ${code}`);
        api.kill();
        dashboard.kill();
        process.exit(code);
    };

    api.on('close', (code) => handleExit(code, 'Orion.Api'));
    dashboard.on('close', (code) => handleExit(code, 'Dashboard'));

    // Handle process termination
    process.on('SIGINT', () => {
        console.log('[BOOTSTRAP] Shutting down...');
        api.kill();
        dashboard.kill();
        process.exit();
    });
}

startSystem().catch(err => {
    console.error('[BOOTSTRAP] Fatal error:', err);
    process.exit(1);
});
