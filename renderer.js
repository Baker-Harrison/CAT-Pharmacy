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

function getValue(source, ...keys) {
  if (!source || typeof source !== "object") return undefined;
  for (const key of keys) {
    if (key in source) return source[key];
  }
  return undefined;
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

function createLessonsRenderer(rootDocument, api) {
  const elements = {
    list: rootDocument.querySelector("#lessonsList"),
    empty: rootDocument.querySelector("#lessonEmpty"),
    title: rootDocument.querySelector("#lessonTitle"),
    summary: rootDocument.querySelector("#lessonSummary"),
    slides: rootDocument.querySelector("#lessonSlides"),
    keyPoints: rootDocument.querySelector("#lessonKeyPoints"),
    meta: rootDocument.querySelector("#lessonMeta"),
  };

  const state = {
    lessons: [],
    activeLessonId: null,
  };

  function clearElement(target) {
    if (!target) return;
    target.innerHTML = "";
  }

  function setEmptyState(isVisible, message) {
    if (!elements.empty) return;
    const baseClass = "lesson-empty";
    elements.empty.className = isVisible ? `${baseClass} is-visible` : baseClass;
    if (message) {
      elements.empty.textContent = message;
    }
  }

  function normalizeLesson(rawLesson, index) {
    const lesson = rawLesson && typeof rawLesson === "object" ? rawLesson : {};
    const id = getValue(lesson, "id", "Id") ?? `lesson-${index}`;
    const title = getValue(lesson, "title", "Title") || "Untitled lesson";
    const summary =
      getValue(lesson, "summary", "Summary", "description", "Description") || "No summary provided.";
    const estimatedReadMinutes = toNumber(
      getValue(lesson, "estimatedReadMinutes", "EstimatedReadMinutes"),
      0
    );
    const progressPercent = toNumber(getValue(lesson, "progressPercent", "ProgressPercent"), 0);

    const sectionsRaw = getValue(lesson, "sections", "Sections");
    const sections = Array.isArray(sectionsRaw)
      ? sectionsRaw.map((section, sectionIndex) => {
          const sectionValue = section && typeof section === "object" ? section : {};
          const heading =
            getValue(sectionValue, "heading", "Heading") || `Section ${sectionIndex + 1}`;
          const body = getValue(sectionValue, "body", "Body") || "";
          const promptsRaw = getValue(sectionValue, "prompts", "Prompts");
          const prompts = Array.isArray(promptsRaw)
            ? promptsRaw
                .map((prompt) => {
                  if (typeof prompt === "string") return prompt;
                  if (prompt && typeof prompt === "object") {
                    return getValue(prompt, "prompt", "Prompt") || "";
                  }
                  return "";
                })
                .filter(Boolean)
            : [];

          return {
            id: getValue(sectionValue, "id", "Id") ?? `${id}-section-${sectionIndex}`,
            heading,
            body,
            prompts,
          };
        })
      : [];

    const keyPointsRaw = getValue(lesson, "keyPoints", "KeyPoints");
    const keyPoints = Array.isArray(keyPointsRaw)
      ? keyPointsRaw
          .map((point) => (typeof point === "string" ? point.trim() : ""))
          .filter(Boolean)
      : [];

    if (keyPoints.length === 0) {
      sections.forEach((section) => {
        section.prompts.forEach((prompt) => {
          if (prompt) keyPoints.push(prompt);
        });
      });
    }

    return {
      id: String(id),
      title,
      summary,
      estimatedReadMinutes,
      progressPercent,
      sections,
      keyPoints,
    };
  }

  function renderLessonList() {
    if (!elements.list) return;
    clearElement(elements.list);

    if (state.lessons.length === 0) {
      setEmptyState(true, "No lessons available yet. Generate lessons after ingesting content.");
      return;
    }

    setEmptyState(false);
    state.lessons.forEach((lesson) => {
      const button = rootDocument.createElement("button");
      const isActive = lesson.id === state.activeLessonId;
      button.className = isActive ? "lesson-item active" : "lesson-item";
      button.dataset.lessonId = lesson.id;

      const title = rootDocument.createElement("div");
      title.className = "lesson-item-title";
      title.textContent = lesson.title;

      const meta = rootDocument.createElement("div");
      meta.className = "lesson-item-meta";
      const minutesLabel = lesson.estimatedReadMinutes
        ? `${lesson.estimatedReadMinutes} min`
        : "Read time n/a";
      meta.textContent = `${minutesLabel} • ${Math.round(lesson.progressPercent)}% complete`;

      button.appendChild(title);
      button.appendChild(meta);

      button.addEventListener("click", () => {
        selectLesson(lesson.id);
      });

      elements.list.appendChild(button);
    });
  }

  function renderLessonDetails(lesson) {
    if (elements.title) elements.title.textContent = lesson?.title || "Select a lesson";
    if (elements.summary)
      elements.summary.textContent =
        lesson?.summary || "Choose a module to review slides, summaries, and key points.";

    if (elements.meta) {
      clearElement(elements.meta);
      if (lesson) {
        const chips = [];
        if (lesson.estimatedReadMinutes) {
          chips.push(`Estimated read: ${lesson.estimatedReadMinutes} min`);
        }
        chips.push(`Progress: ${Math.round(lesson.progressPercent)}%`);
        chips.push(`Sections: ${lesson.sections.length}`);

        chips.forEach((text) => {
          const chip = rootDocument.createElement("div");
          chip.className = "lesson-chip";
          chip.textContent = text;
          elements.meta.appendChild(chip);
        });
      }
    }

    if (elements.slides) {
      clearElement(elements.slides);
      if (!lesson || lesson.sections.length === 0) {
        const empty = rootDocument.createElement("div");
        empty.className = "lesson-slide";
        empty.textContent = "No slide content available for this lesson.";
        elements.slides.appendChild(empty);
      } else {
        lesson.sections.forEach((section) => {
          const slide = rootDocument.createElement("div");
          slide.className = "lesson-slide";

          const heading = rootDocument.createElement("div");
          heading.className = "lesson-slide-title";
          heading.textContent = section.heading;

          const body = rootDocument.createElement("div");
          body.className = "lesson-slide-body";
          body.textContent = section.body || "No slide summary available.";

          slide.appendChild(heading);
          slide.appendChild(body);

          if (section.prompts.length > 0) {
            const promptList = rootDocument.createElement("ul");
            promptList.className = "lesson-slide-prompts";
            section.prompts.forEach((prompt) => {
              const li = rootDocument.createElement("li");
              li.textContent = prompt;
              promptList.appendChild(li);
            });
            slide.appendChild(promptList);
          }

          elements.slides.appendChild(slide);
        });
      }
    }

    if (elements.keyPoints) {
      clearElement(elements.keyPoints);
      const points = lesson?.keyPoints ?? [];
      if (points.length === 0) {
        const empty = rootDocument.createElement("li");
        empty.textContent = "No key points available yet.";
        elements.keyPoints.appendChild(empty);
      } else {
        points.forEach((point) => {
          const li = rootDocument.createElement("li");
          li.textContent = point;
          elements.keyPoints.appendChild(li);
        });
      }
    }
  }

  function selectLesson(lessonId) {
    state.activeLessonId = lessonId;
    const lesson = state.lessons.find((item) => item.id === lessonId) || null;
    renderLessonList();
    renderLessonDetails(lesson);
  }

  async function loadLessons() {
    if (!api?.getLessons) {
      setEmptyState(true, "Lessons service not available.");
      return;
    }

    try {
      const payload = await api.getLessons();
      const rawLessons = Array.isArray(payload?.lessons) ? payload.lessons : [];
      state.lessons = rawLessons.map((lesson, index) => normalizeLesson(lesson, index));
      renderLessonList();
      if (state.lessons.length > 0) {
        selectLesson(state.lessons[0].id);
      } else {
        renderLessonDetails(null);
      }
    } catch (error) {
      setEmptyState(true, error?.message || "Failed to load lessons.");
    }
  }

  function initialize() {
    renderLessonDetails(null);
    loadLessons();
  }

  return {
    elements,
    initialize,
    loadLessons,
    selectLesson,
  };
}

function createLearningStateMachine() {
  const state = {
    phase: "learning",
    currentUnit: null,
    nextUnit: null,
    progress: { completed: 0, total: 0, percent: 0 },
    result: null,
    ability: null,
    masteryLevels: null,
    isComplete: false,
  };

  function getState() {
    return state;
  }

  function setUnit(unit, progress, ability, masteryLevels, isComplete = false) {
    state.currentUnit = unit || null;
    state.nextUnit = null;
    state.progress = progress || state.progress;
    state.result = null;
    state.phase = "learning";
    state.ability = ability || state.ability;
    state.masteryLevels = masteryLevels || state.masteryLevels;
    state.isComplete = Boolean(isComplete);
  }

  function beginAssessment() {
    if (!state.currentUnit || state.isComplete) return;
    state.phase = "assessment";
  }

  function recordResult(result, nextUnit, progress, ability, masteryLevels, isComplete = false) {
    state.result = result || null;
    state.nextUnit = nextUnit || null;
    state.progress = progress || state.progress;
    state.phase = "result";
    state.ability = ability || state.ability;
    state.masteryLevels = masteryLevels || state.masteryLevels;
    state.isComplete = Boolean(isComplete);
  }

  function advance() {
    if (state.isComplete || !state.nextUnit) return;
    state.currentUnit = state.nextUnit;
    state.nextUnit = null;
    state.result = null;
    state.phase = "learning";
  }

  return {
    getState,
    setUnit,
    beginAssessment,
    recordResult,
    advance,
  };
}

function createLearningRenderer(rootDocument, api) {
  const elements = {
    shell: rootDocument.querySelector("#learningShell"),
    title: rootDocument.querySelector("#learningTitle"),
    subtitle: rootDocument.querySelector("#learningSubtitle"),
    progressText: rootDocument.querySelector("#learningProgressText"),
    progressFill: rootDocument.querySelector("#learningProgressFill"),
    phase: rootDocument.querySelector("#learningPhase"),
    phaseChip: rootDocument.querySelector("#learningPhaseChip"),
    conceptTitle: rootDocument.querySelector("#learningConceptTitle"),
    conceptSummary: rootDocument.querySelector("#learningConceptSummary"),
    keyPoints: rootDocument.querySelector("#learningKeyPoints"),
    prompt: rootDocument.querySelector("#learningPrompt"),
    answer: rootDocument.querySelector("#learningAnswer"),
    feedback: rootDocument.querySelector("#learningFeedback"),
    startAssessment: rootDocument.querySelector("#learningStartAssessment"),
    submitAnswer: rootDocument.querySelector("#learningSubmitAnswer"),
    continueButton: rootDocument.querySelector("#learningContinue"),
  };

  const machine = createLearningStateMachine();
  const emptyPrompt = "Review the concept summary and begin the quick check when ready.";

  function setFeedback(message, tone = "neutral") {
    if (!elements.feedback) return;
    elements.feedback.textContent = message || "";
    elements.feedback.dataset.tone = tone;
  }

  function renderKeyPoints(points) {
    if (!elements.keyPoints) return;
    elements.keyPoints.innerHTML = "";
    const list = Array.isArray(points) ? points : [];
    if (list.length === 0) {
      const empty = rootDocument.createElement("li");
      empty.textContent = "No key points available for this concept yet.";
      elements.keyPoints.appendChild(empty);
      return;
    }
    list.forEach((point) => {
      const item = rootDocument.createElement("li");
      item.textContent = point;
      elements.keyPoints.appendChild(item);
    });
  }

  function renderProgress(progress) {
    const completed = toNumber(progress?.completed, 0);
    const total = Math.max(toNumber(progress?.total, 0), 0);
    const percent = Math.max(0, Math.min(100, toNumber(progress?.percent, 0)));
    if (elements.progressText) {
      elements.progressText.textContent = `${completed} of ${total} complete`;
    }
    if (elements.progressFill) {
      elements.progressFill.style.width = `${percent}%`;
    }
  }

  function buildPrompt(unit) {
    if (!unit) return emptyPrompt;
    const topic = unit.topic || "this concept";
    return `In your own words, explain ${topic} and include the key points.`;
  }

  function render() {
    const state = machine.getState();
    const phaseLabel = state.phase.charAt(0).toUpperCase() + state.phase.slice(1);
    if (elements.shell) {
      elements.shell.dataset.phase = state.phase;
    }
    if (elements.phase) elements.phase.textContent = phaseLabel;
    if (elements.phaseChip) elements.phaseChip.textContent = phaseLabel;

    if (elements.conceptTitle) {
      elements.conceptTitle.textContent = state.currentUnit?.topic || "Awaiting session";
    }
    if (elements.conceptSummary) {
      elements.conceptSummary.textContent =
        state.currentUnit?.summary || "Start a session to load the next adaptive concept.";
    }
    renderKeyPoints(state.currentUnit?.keyPoints);
    renderProgress(state.progress);

    if (elements.prompt) {
      elements.prompt.textContent = state.phase === "assessment" ? buildPrompt(state.currentUnit) : emptyPrompt;
    }

    if (elements.answer) {
      elements.answer.disabled = state.phase !== "assessment";
      if (state.phase !== "assessment") {
        elements.answer.value = "";
      }
    }

    if (state.phase === "result" && state.result) {
      const tone = state.result.isCorrect ? "success" : "error";
      setFeedback(state.result.feedback || (state.result.isCorrect ? "Correct response." : "Incorrect response."), tone);
    } else {
      setFeedback("", "neutral");
    }

    if (state.isComplete && elements.prompt) {
      elements.prompt.textContent = "Session complete. You have mastered the current knowledge set.";
    }
  }

  async function startSession() {
    if (!api?.startLearning) {
      setFeedback("Learning service not available.", "error");
      return;
    }
    try {
      const payload = await api.startLearning();
      machine.setUnit(payload?.currentUnit, payload?.progress, payload?.ability, payload?.masteryLevels);
      render();
    } catch (error) {
      setFeedback(error?.message || "Failed to start learning session.", "error");
    }
  }

  async function submitAnswer() {
    const state = machine.getState();
    if (!api?.processLearningResponse) {
      setFeedback("Learning service not available.", "error");
      return;
    }
    if (!state.currentUnit) {
      setFeedback("No active concept to assess.", "error");
      return;
    }

    try {
      const payload = await api.processLearningResponse({
        action: "response",
        unitId: state.currentUnit.id,
        answer: elements.answer?.value || "",
      });
      machine.recordResult(
        payload?.result,
        payload?.nextUnit,
        payload?.progress,
        payload?.ability,
        payload?.masteryLevels,
        payload?.isComplete
      );
      render();
    } catch (error) {
      setFeedback(error?.message || "Failed to process response.", "error");
    }
  }

  function bindEvents() {
    if (elements.startAssessment) {
      elements.startAssessment.addEventListener("click", () => {
        machine.beginAssessment();
        render();
      });
    }
    if (elements.submitAnswer) {
      elements.submitAnswer.addEventListener("click", () => {
        submitAnswer();
      });
    }
    if (elements.continueButton) {
      elements.continueButton.addEventListener("click", () => {
        machine.advance();
        render();
      });
    }
  }

  function initialize() {
    bindEvents();
    render();
    startSession();
  }

  return {
    elements,
    initialize,
    render,
    startSession,
    submitAnswer,
    stateMachine: machine,
  };
}

if (typeof window !== "undefined" && typeof document !== "undefined") {
  const dashboard = createDashboardRenderer(document);
  const upload = createUploadRenderer(document, window.catApi, (summary) => {
    dashboard.renderSummary(summary);
  });
  dashboard.initialize();
  upload.initialize();
  const lessons = createLessonsRenderer(document, window.catApi);
  lessons.initialize();
  const learning = createLearningRenderer(document, window.catApi);
  learning.initialize();
}

if (typeof module !== "undefined" && module.exports) {
  module.exports = {
    createDashboardRenderer,
    createUploadRenderer,
    createLessonsRenderer,
    createLearningRenderer,
    createLearningStateMachine,
    formatTimestamp,
  };
}
