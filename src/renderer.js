const syncButton = document.querySelector('#syncButton');
const syncStatus = document.querySelector('#syncStatus');
const nodeCountLabel = document.querySelector('#nodeCount');
const edgeCountLabel = document.querySelector('#edgeCount');
const sidebarNodeCountLabel = document.querySelector('#sidebarNodeCount');
const sidebarEdgeCountLabel = document.querySelector('#sidebarEdgeCount');
const graphSourceLabel = document.querySelector('#graphSource');
const lastUpdatedLabel = document.querySelector('#lastUpdated');
const typeList = document.querySelector('#typeList');
const pulseSummary = document.querySelector('#pulseSummary');

const state = {
  isSyncing: false,
};

function setSyncStatus(message, tone = 'neutral') {
  syncStatus.textContent = message;
  syncStatus.dataset.tone = tone;
}

function formatTimestamp(value) {
  if (!value) return 'Not available';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return 'Not available';
  return date.toLocaleString();
}

function renderTypeList(nodeTypes) {
  typeList.innerHTML = '';
  const entries = Object.entries(nodeTypes || {}).sort((a, b) => b[1] - a[1]);

  if (entries.length === 0) {
    const empty = document.createElement('div');
    empty.className = 'type-row type-row--empty';
    empty.textContent = 'No nodes available yet.';
    typeList.appendChild(empty);
    return;
  }

  entries.forEach(([type, count]) => {
    const row = document.createElement('div');
    row.className = 'type-row';
    row.innerHTML = `
      <span class="type-label">${type}</span>
      <span class="type-count">${count}</span>
    `;
    typeList.appendChild(row);
  });
}

function renderSummary(summary) {
  const nodeCount = summary.nodeCount?.toString() ?? '0';
  const edgeCount = summary.edgeCount?.toString() ?? '0';
  nodeCountLabel.textContent = nodeCount;
  edgeCountLabel.textContent = edgeCount;
  sidebarNodeCountLabel.textContent = nodeCount;
  sidebarEdgeCountLabel.textContent = edgeCount;
  graphSourceLabel.textContent = summary.source || 'No graph data found';
  lastUpdatedLabel.textContent = formatTimestamp(summary.lastUpdated);

  renderTypeList(summary.nodeTypes);

  const score = summary.nodeCount ?? 0;
  const readiness = score > 0 ? 'Active knowledge graph loaded.' : 'Waiting for content ingestion.';
  pulseSummary.textContent = readiness;
}

async function syncBackend() {
  if (state.isSyncing) return;
  state.isSyncing = true;
  syncButton.disabled = true;
  setSyncStatus('Syncing with Python backend...', 'neutral');

  try {
    const summary = await window.catApi.syncBackend();
    renderSummary(summary);
    setSyncStatus('Sync complete', 'success');
  } catch (error) {
    setSyncStatus(error.message || 'Sync failed', 'error');
  } finally {
    state.isSyncing = false;
    syncButton.disabled = false;
  }
}

syncButton.addEventListener('click', syncBackend);

setSyncStatus('Ready to sync', 'neutral');
renderSummary({ nodeCount: 0, edgeCount: 0, nodeTypes: {}, source: 'Awaiting sync', lastUpdated: null });

syncBackend();
