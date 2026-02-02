const DEFAULT_LEVEL_ORDER = ["Advanced", "Proficient", "Developing", "Novice", "Unknown"];

function formatTimestamp(value) {
  if (!value) return "Not available";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "Not available";
  return date.toLocaleString();
}

function toNumber(value, fallback = 0) {
  if (typeof value === "number" && Number.isFinite(value)) return value;
  const parsed = Number.parseFloat(value);
  return Number.isFinite(parsed) ? parsed : fallback;
}

function createUploadRenderer(rootDocument, api, onSummaryUpdated) {
  const elements = {
    dropzone: rootDocument.querySelector("#uploadDropzone"),
    input: rootDocument.querySelector("#uploadInput"),
    status: rootDocument.querySelector("#uploadStatus"),
    fileName: rootDocument.querySelector("#uploadFileName"),
    openUploadButton: rootDocument.querySelector("#openUpload"),
    uploadView: rootDocument.querySelector("#uploadView"),
  };

  const state = { isUploading: false };

  function setStatus(message, tone = "neutral") {
    if (!elements.status) return;
    elements.status.textContent = message;
    elements.status.dataset.tone = tone;
  }

  function setDropzoneState(nextState) {
    if (!elements.dropzone) return;
    elements.dropzone.dataset.state = nextState;
  }

  function setFileName(name) {
    if (!elements.fileName) return;
    elements.fileName.textContent = name || "No file selected";
  }

  function isSupportedFile(filePath) {
    return typeof filePath === "string" && filePath.toLowerCase().endsWith(".pptx");
  }

  async function handleFilePath(filePath, displayName) {
    if (!filePath) return;
    if (!isSupportedFile(filePath)) {
      setStatus("Only .pptx files are supported", "error");
      return;
    }
    if (!api?.processUpload) {
      setStatus("Upload service not available", "error");
      return;
    }
    if (state.isUploading) return;

    state.isUploading = true;
    setDropzoneState("busy");
    setFileName(displayName || filePath.split(/[\\/]/).pop());
    setStatus("Sending file to parser...", "neutral");

    try {
      const result = await api.processUpload(filePath);
      const unitCount = typeof result?.unitCount === "number" ? result.unitCount : 0;
      setStatus(`Parsed ${unitCount} knowledge units`, "success");
      if (typeof onSummaryUpdated === "function" && result?.summary) {
        onSummaryUpdated(result.summary);
      }
    } catch (error) {
      setStatus(error?.message || "Upload failed", "error");
    } finally {
      state.isUploading = false;
      setDropzoneState("idle");
    }
  }

  function handleFileInputChange() {
    if (!elements.input) return;
    const file = elements.input.files?.[0];
    const filePath = file?.path || file?.name;
    handleFilePath(filePath, file?.name);
  }

  function handleDrop(event) {
    event.preventDefault();
    setDropzoneState("idle");
    const file = event.dataTransfer?.files?.[0];
    const filePath = file?.path || file?.name;
    handleFilePath(filePath, file?.name);
  }

  function handleDragOver(event) {
    event.preventDefault();
    setDropzoneState("drag");
  }

  function handleDragLeave(event) {
    event.preventDefault();
    if (state.isUploading) return;
    setDropzoneState("idle");
  }

  function bindDropzone() {
    if (elements.dropzone) {
      elements.dropzone.addEventListener("click", () => {
        if (elements.input) elements.input.click();
      });
      elements.dropzone.addEventListener("dragover", handleDragOver);
      elements.dropzone.addEventListener("dragleave", handleDragLeave);
      elements.dropzone.addEventListener("drop", handleDrop);
    }
    if (elements.input) {
      elements.input.addEventListener("change", handleFileInputChange);
    }
  }

  function bindUploadButton() {
    if (!elements.openUploadButton || !elements.uploadView) return;
    elements.openUploadButton.addEventListener("click", () => {
      elements.uploadView.scrollIntoView({ behavior: "smooth", block: "start" });
    });
  }

  function bindStatusUpdates() {
    if (!api?.onUploadStatus) return;
    api.onUploadStatus((payload) => {
      if (!payload) return;
      if (payload.state) {
        setDropzoneState(payload.state);
      }
      if (payload.fileName) {
        setFileName(payload.fileName);
      }
      if (payload.message) {
        setStatus(payload.message, payload.tone || "neutral");
      }
    });
  }

  function initialize() {
    bindDropzone();
    bindUploadButton();
    bindStatusUpdates();
    setStatus("Ready to ingest", "neutral");
    setDropzoneState("idle");
  }

  return {
    elements,
    initialize,
    handleFilePath,
  };
}

function createDashboardRenderer(rootDocument) {
  const elements = {
    syncButton: rootDocument.querySelector("#syncButton"),
    syncStatus: rootDocument.querySelector("#syncStatus"),
    nodeCountLabel: rootDocument.querySelector("#nodeCount"),
    edgeCountLabel: rootDocument.querySelector("#edgeCount"),
    sidebarNodeCountLabel: rootDocument.querySelector("#sidebarNodeCount"),
    sidebarEdgeCountLabel: rootDocument.querySelector("#sidebarEdgeCount"),
    graphSourceLabel: rootDocument.querySelector("#graphSource"),
    lastUpdatedLabel: rootDocument.querySelector("#lastUpdated"),
    typeList: rootDocument.querySelector("#typeList"),
    pulseSummary: rootDocument.querySelector("#pulseSummary"),
    masteryList: rootDocument.querySelector("#masteryList"),
    recentTopics: rootDocument.querySelector("#recentTopics"),
  };

  const state = { isSyncing: false };

  function setSyncStatus(message, tone = "neutral") {
    if (!elements.syncStatus) return;
    elements.syncStatus.textContent = message;
    elements.syncStatus.dataset.tone = tone;
  }

  function clearElement(target) {
    if (!target) return;
    target.innerHTML = "";
  }

  function renderTypeList(nodeTypes) {
    if (!elements.typeList) return;
    clearElement(elements.typeList);
    const entries = Object.entries(nodeTypes || {}).sort((a, b) => b[1] - a[1]);

    if (entries.length === 0) {
      const empty = rootDocument.createElement("div");
      empty.className = "type-row type-row--empty";
      empty.textContent = "No nodes available yet.";
      elements.typeList.appendChild(empty);
      return;
    }

    entries.forEach(([type, count]) => {
      const row = rootDocument.createElement("div");
      row.className = "type-row";

      const label = rootDocument.createElement("span");
      label.className = "type-label";
      label.textContent = type;

      const value = rootDocument.createElement("span");
      value.className = "type-count";
      value.textContent = `${count}`;

      row.appendChild(label);
      row.appendChild(value);
      elements.typeList.appendChild(row);
    });
  }

  function renderMasteryList(masteryLevels) {
    if (!elements.masteryList) return;
    clearElement(elements.masteryList);

    const totals = DEFAULT_LEVEL_ORDER.reduce((sum, level) => {
      return sum + toNumber(masteryLevels?.[level], 0);
    }, 0);

    DEFAULT_LEVEL_ORDER.forEach((level) => {
      const count = toNumber(masteryLevels?.[level], 0);
      const percent = totals > 0 ? Math.round((count / totals) * 100) : 0;

      const row = rootDocument.createElement("div");
      row.className = "mastery-row";

      const label = rootDocument.createElement("div");
      label.className = "mastery-label";
      label.textContent = level;

      const value = rootDocument.createElement("div");
      value.className = "mastery-value";
      value.textContent = `${count}`;

      const bar = rootDocument.createElement("div");
      bar.className = "mastery-bar";
      bar.style.setProperty("--percent", `${percent}%`);

      const barFill = rootDocument.createElement("span");
      bar.appendChild(barFill);

      row.appendChild(label);
      row.appendChild(value);
      row.appendChild(bar);
      elements.masteryList.appendChild(row);
    });
  }

  function renderRecentTopics(topics) {
    if (!elements.recentTopics) return;
    clearElement(elements.recentTopics);
    const entries = Array.isArray(topics) ? topics : [];

    if (entries.length === 0) {
      const empty = rootDocument.createElement("div");
      empty.className = "recent-row recent-row--empty";
      empty.textContent = "No recent study activity yet.";
      elements.recentTopics.appendChild(empty);
      return;
    }

    entries.forEach((topic) => {
      const row = rootDocument.createElement("div");
      row.className = "recent-row";

      const details = rootDocument.createElement("div");
      details.className = "recent-details";

      const title = rootDocument.createElement("div");
      title.className = "recent-title";
      title.textContent = topic.title || "Untitled topic";

      const meta = rootDocument.createElement("div");
      meta.className = "recent-meta";
      const when = topic.lastAssessed ? formatTimestamp(topic.lastAssessed) : "No timestamp";
      meta.textContent = when;

      details.appendChild(title);
      details.appendChild(meta);

      const badge = rootDocument.createElement("div");
      badge.className = "recent-badge";
      badge.dataset.level = (topic.level || "Unknown").toLowerCase();
      badge.textContent = topic.level || "Unknown";

      row.appendChild(details);
      row.appendChild(badge);
      elements.recentTopics.appendChild(row);
    });
  }

  function renderSummary(summary) {
    const nodeCount = toNumber(summary?.nodeCount, 0);
    const edgeCount = toNumber(summary?.edgeCount, 0);

    if (elements.nodeCountLabel) elements.nodeCountLabel.textContent = `${nodeCount}`;
    if (elements.edgeCountLabel) elements.edgeCountLabel.textContent = `${edgeCount}`;
    if (elements.sidebarNodeCountLabel) elements.sidebarNodeCountLabel.textContent = `${nodeCount}`;
    if (elements.sidebarEdgeCountLabel) elements.sidebarEdgeCountLabel.textContent = `${edgeCount}`;

    if (elements.graphSourceLabel) {
      elements.graphSourceLabel.textContent = summary?.source || "No graph data found";
    }
    if (elements.lastUpdatedLabel) {
      elements.lastUpdatedLabel.textContent = formatTimestamp(summary?.lastUpdated);
    }

    renderTypeList(summary?.nodeTypes);
    renderMasteryList(summary?.masteryLevels);
    renderRecentTopics(summary?.recentTopics);

    if (elements.pulseSummary) {
      const masteryCount = Object.values(summary?.masteryLevels || {}).reduce(
        (sum, value) => sum + toNumber(value, 0),
        0
      );
      if (nodeCount === 0) {
        elements.pulseSummary.textContent = "Waiting for content ingestion.";
      } else if (masteryCount === 0) {
        elements.pulseSummary.textContent = "Graph is live. Mastery tracking will activate after sessions.";
      } else {
        elements.pulseSummary.textContent = `Tracking mastery across ${masteryCount} nodes.`;
      }
    }
  }

  async function syncBackend() {
    if (state.isSyncing) return;
    if (typeof window === "undefined" || !window.catApi?.syncBackend) {
      setSyncStatus("Python backend not available", "error");
      return;
    }

    state.isSyncing = true;
    if (elements.syncButton) elements.syncButton.disabled = true;
    setSyncStatus("Syncing with Python backend...", "neutral");

    try {
      const summary = await window.catApi.syncBackend();
      renderSummary(summary);
      setSyncStatus("Sync complete", "success");
    } catch (error) {
      setSyncStatus(error.message || "Sync failed", "error");
    } finally {
      state.isSyncing = false;
      if (elements.syncButton) elements.syncButton.disabled = false;
    }
  }

  function initialize() {
    if (elements.syncButton) {
      elements.syncButton.addEventListener("click", syncBackend);
    }
    setSyncStatus("Ready to sync", "neutral");
    renderSummary({
      nodeCount: 0,
      edgeCount: 0,
      nodeTypes: {},
      masteryLevels: {},
      recentTopics: [],
      source: "Awaiting sync",
      lastUpdated: null,
    });
    syncBackend();
  }

  return {
    elements,
    initialize,
    renderSummary,
    syncBackend,
  };
}

if (typeof window !== "undefined" && typeof document !== "undefined") {
  const dashboard = createDashboardRenderer(document);
  const upload = createUploadRenderer(document, window.catApi, (summary) => {
    dashboard.renderSummary(summary);
  });
  dashboard.initialize();
  upload.initialize();
}

if (typeof module !== "undefined" && module.exports) {
  module.exports = { createDashboardRenderer, createUploadRenderer, formatTimestamp };
}
