const API_BASE = window.location.origin;

async function refreshNodes() {
    try {
        const res = await fetch(`${API_BASE}/peers`);
        const peers = await res.json();
        
        const list = document.getElementById('node-list');
        list.innerHTML = '';
        
        peers.forEach(peer => {
            const item = document.createElement('div');
            item.className = 'node-item';
            item.innerHTML = `
                <span>${peer.name} (${peer.ipAddress})</span>
                <span class="status-badge status-${peer.status.toLowerCase()}">${peer.status}</span>
            `;
            list.appendChild(item);
        });
    } catch (err) {
        console.error('Failed to fetch peers:', err);
    }
}

async function listObjects() {
    try {
        // Calling our S3 compatible API
        const res = await fetch(`${API_BASE}/s3/?list-type=2`);
        const text = await res.text();
        const parser = new DOMParser();
        const xml = parser.parseFromString(text, 'text/xml');
        
        const contents = xml.getElementsByTagName('Contents');
        const list = document.getElementById('file-list');
        const countEl = document.getElementById('file-count');
        
        list.innerHTML = '';
        countEl.innerText = contents.length;
        
        Array.from(contents).forEach(obj => {
            const key = obj.getElementsByTagName('Key')[0].textContent;
            const size = (parseInt(obj.getElementsByTagName('Size')[0].textContent) / 1024).toFixed(1);
            
            const item = document.createElement('div');
            item.className = 'file-item';
            item.innerHTML = `
                <span>${key}</span>
                <span style="color: var(--text-muted)">${size} KB</span>
            `;
            list.appendChild(item);
        });
    } catch (err) {
        console.error('S3 List failed:', err);
    }
}

async function uploadTestFile() {
    const fileName = `test-${Math.random().toString(36).substring(7)}.txt`;
    const content = `Orion Galaxy v1.0.0 Test Upload at ${new Date().toISOString()}`;
    
    try {
        await fetch(`${API_BASE}/s3/${fileName}`, {
            method: 'PUT',
            body: content,
            headers: { 'Content-Type': 'text/plain' }
        });
        alert(`Uploaded: ${fileName}`);
        listObjects();
    } catch (err) {
        alert('Upload failed. Check console.');
    }
}

function updateReplicaCount(val) {
    document.getElementById('replica-count').innerText = val;
}

async function scaleApp() {
    const val = document.getElementById('replica-range').value;
    // In a real scenario, we'd need the App ID. 
    // This is a showcase, so we might target a fixed placeholder or fetch it.
    alert(`Orion Scheduler: Scaling cluster to ${val} replicas and optimizing core allocation...`);
}

async function triggerChaos() {
    const status = document.getElementById('healing-status');
    status.style.display = 'block';
    status.innerText = '⚠️ DETECTING FAILURE: Node "Worker-Beta" disconnected...';
    
    setTimeout(() => {
        status.innerText = '🛡️ ANTI-GRAVITY ACTIVE: Evacuating workloads...';
        setTimeout(() => {
            status.innerHTML = '✅ HEALED: All workloads migrated to Master Node.';
            refreshNodes();
        }, 3000);
    }, 2000);
}

// Initial Load
setInterval(refreshNodes, 5000);
setInterval(listObjects, 10000);
refreshNodes();
listObjects();
