const DEFAULT_VIEW = "ingest";

function formatTimestamp(value) {
  if (!value) return "--";
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return "--";
  return date.toLocaleString();
}

function normalizeErrorMessage(error, fallback) {
  const raw = error?.message || String(error || "");
  const message = raw.trim();
  const lower = message.toLowerCase();
  if (lower.includes("python_not_found") || lower.includes("python executable not found") || lower.includes("enoent")) {
    return "Python not found. Install Python 3 and restart CAT-Pharmacy.";
  }
  if (lower.includes("pptx") && (lower.includes("invalid") || lower.includes("badzipfile"))) {
    return "PPTX format invalid. Export a valid .pptx file and try again.";
  }
  if (lower.includes("database locked") || lower.includes("locked")) {
    return "Database locked. Please retry in a moment.";
  }
  return message || fallback || "An unexpected error occurred.";
}

function setupNavigation(documentRoot) {
  const navItems = Array.from(documentRoot.querySelectorAll(".nav-item"));
  const views = {
    ingest: documentRoot.querySelector("#ingestView"),
    lessons: documentRoot.querySelector("#lessonsView"),
    exams: documentRoot.querySelector("#examsView"),
  };

  function setActiveView(viewId) {
    Object.entries(views).forEach(([key, view]) => {
      if (!view) return;
      view.classList.toggle("is-active", key === viewId);
    });
    navItems.forEach((item) => {
      item.classList.toggle("active", item.dataset.view === viewId);
    });
  }

  navItems.forEach((item) => {
    item.addEventListener("click", () => setActiveView(item.dataset.view));
  });

  setActiveView(DEFAULT_VIEW);
  return { setActiveView };
}

function createUploadRenderer(documentRoot, api) {
  const elements = {
    dropzone: documentRoot.querySelector("#uploadDropzone"),
    input: documentRoot.querySelector("#uploadInput"),
    status: documentRoot.querySelector("#uploadStatus"),
    fileName: documentRoot.querySelector("#uploadFileName"),
    unitCount: documentRoot.querySelector("#ingestUnitCount"),
    objectiveCount: documentRoot.querySelector("#ingestObjectiveCount"),
    source: documentRoot.querySelector("#ingestSource"),
    updatedAt: documentRoot.querySelector("#ingestUpdatedAt"),
    sidebarUnitCount: documentRoot.querySelector("#sidebarUnitCount"),
    sidebarLastIngest: documentRoot.querySelector("#sidebarLastIngest"),
  };

  const state = { isUploading: false, requestId: 0 };

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

  function updateSummary(summary) {
    if (!summary) return;
    const unitCount = Number(summary.unitCount || 0);
    const objectiveCount = Number(summary.objectiveCount || 0);
    if (elements.unitCount) elements.unitCount.textContent = `${unitCount}`;
    if (elements.objectiveCount) elements.objectiveCount.textContent = `${objectiveCount}`;
    if (elements.source) elements.source.textContent = summary.sourceFile || "--";
    if (elements.updatedAt) elements.updatedAt.textContent = formatTimestamp(summary.updatedAt);
    if (elements.sidebarUnitCount) elements.sidebarUnitCount.textContent = `${unitCount}`;
    if (elements.sidebarLastIngest) elements.sidebarLastIngest.textContent = summary.sourceFile
      ? `Last ingest: ${summary.sourceFile}`
      : "No ingestion yet";
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
    const requestId = ++state.requestId;
    setDropzoneState("busy");
    setFileName(displayName || filePath.split(/[\\/]/).pop());
    setStatus("Sending file to parser...", "neutral");

    try {
      const result = await api.processUpload(filePath);
      if (requestId !== state.requestId) return;
      setStatus(`Parsed ${result?.unitCount || 0} knowledge units`, "success");
      updateSummary(result);
    } catch (error) {
      if (requestId !== state.requestId) return;
      setStatus(normalizeErrorMessage(error, "Upload failed"), "error");
    } finally {
      if (requestId === state.requestId) {
        state.isUploading = false;
        setDropzoneState("idle");
      }
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
      if (payload.summary) {
        updateSummary(payload.summary);
      }
    });
  }

  function initialize() {
    bindDropzone();
    bindStatusUpdates();
    setStatus("Ready to ingest", "neutral");
    setDropzoneState("idle");
  }

  return {
    initialize,
    updateSummary,
  };
}

function createLessonsRenderer(documentRoot, api) {
  const elements = {
    list: documentRoot.querySelector("#lessonsList"),
    empty: documentRoot.querySelector("#lessonsEmpty"),
    title: documentRoot.querySelector("#lessonTitle"),
    summary: documentRoot.querySelector("#lessonSummary"),
    objectives: documentRoot.querySelector("#lessonObjectives"),
    stageLabel: documentRoot.querySelector("#lessonStageLabel"),
    preQuiz: documentRoot.querySelector("#lessonPreQuiz"),
    core: documentRoot.querySelector("#lessonCore"),
    postQuiz: documentRoot.querySelector("#lessonPostQuiz"),
    startButton: documentRoot.querySelector("#lessonStartButton"),
    continueButton: documentRoot.querySelector("#lessonContinueButton"),
  };

  const state = {
    lessons: [],
    activeLessonId: null,
    stage: "idle",
    requestId: 0,
    isLoading: false,
  };

  function clearElement(target) {
    if (!target) return;
    target.innerHTML = "";
  }

  function renderSkeletons(container, count = 3) {
    if (!container) return;
    clearElement(container);
    for (let i = 0; i < count; i++) {
      const skeleton = documentRoot.createElement("div");
      skeleton.className = "list-item";
      skeleton.innerHTML = `
        <div class="skeleton skeleton-title" style="margin-bottom: 8px;"></div>
        <div class="skeleton skeleton-text" style="width: 60%;"></div>
      `;
      container.appendChild(skeleton);
    }
  }

  function setLoading(isLoading) {
    state.isLoading = isLoading;
    if (elements.startButton) {
      elements.startButton.disabled = isLoading;
      elements.startButton.textContent = isLoading ? "Loading..." : "Generate Lesson";
    }
  }

  function setStage(stage) {
    state.stage = stage;
    if (elements.stageLabel) {
      const labelMap = {
        idle: "Idle",
        pre: "Pre-Quiz",
        core: "Lesson",
        post: "Post-Quiz",
        complete: "Complete",
      };
      elements.stageLabel.textContent = labelMap[stage] || "Lesson";
    }

    const show = (element, isVisible) => {
      if (!element) return;
      element.style.display = isVisible ? "grid" : "none";
    };

    show(elements.preQuiz, stage === "pre");
    show(elements.core, stage === "core");
    show(elements.postQuiz, stage === "post");

    if (elements.continueButton) {
      elements.continueButton.style.display = ["pre", "core"].includes(stage) ? "inline-flex" : "none";
      elements.continueButton.textContent = stage === "pre" ? "Continue to Lesson" : "Continue to Post-Quiz";
    }
  }

  function renderLessonList() {
    if (!elements.list) return;
    clearElement(elements.list);
    if (!state.lessons.length) {
      if (elements.empty) elements.empty.style.display = "block";
      return;
    }
    if (elements.empty) elements.empty.style.display = "none";

    state.lessons.forEach((lesson) => {
      const button = documentRoot.createElement("div");
      button.className = lesson.id === state.activeLessonId ? "list-item active" : "list-item";
      button.dataset.lessonId = lesson.id;

      const title = documentRoot.createElement("div");
      title.className = "list-item-title";
      title.textContent = lesson.title || "Untitled lesson";

      const meta = documentRoot.createElement("div");
      meta.className = "list-item-meta";
      const minutes = lesson.estimatedReadMinutes ? `${lesson.estimatedReadMinutes} min` : "Read time n/a";
      meta.textContent = `${minutes} • ${lesson.sourceUnitId ? "Knowledge unit" : ""}`.trim();

      button.appendChild(title);
      button.appendChild(meta);

      button.addEventListener("click", () => selectLesson(lesson.id));
      elements.list.appendChild(button);
    });
  }

  function renderObjectives(objectives) {
    if (!elements.objectives) return;
    clearElement(elements.objectives);
    const items = Array.isArray(objectives) ? objectives : [];
    if (!items.length) {
      const placeholder = documentRoot.createElement("div");
      placeholder.className = "lesson-block-meta";
      placeholder.textContent = "No learning objectives supplied.";
      elements.objectives.appendChild(placeholder);
      return;
    }
    items.forEach((objective) => {
      const chip = documentRoot.createElement("div");
      chip.className = "lesson-objective-chip";
      chip.textContent = objective;
      elements.objectives.appendChild(chip);
    });
  }

  function renderQuiz(container, quiz, label) {
    if (!container) return;
    clearElement(container);
    if (!quiz || !Array.isArray(quiz.items) || !quiz.items.length) {
      const empty = documentRoot.createElement("div");
      empty.className = "lesson-block-meta";
      empty.textContent = `${label} quiz not available.`;
      container.appendChild(empty);
      return;
    }

    quiz.items.forEach((item, index) => {
      const block = documentRoot.createElement("div");
      block.className = "lesson-block";

      const title = documentRoot.createElement("div");
      title.className = "lesson-block-title";
      title.textContent = `${label} Q${index + 1}`;

      const prompt = documentRoot.createElement("div");
      prompt.textContent = item.prompt || item.stem || "";

      block.appendChild(title);
      block.appendChild(prompt);

      if (Array.isArray(item.choices) && item.choices.length) {
        const meta = documentRoot.createElement("div");
        meta.className = "lesson-block-meta";
        meta.textContent = "Choices: " + item.choices.map((choice) => choice.text || choice).join(" | ");
        block.appendChild(meta);
      }

      container.appendChild(block);
    });
  }

  function renderSections(sections) {
    if (!elements.core) return;
    clearElement(elements.core);
    if (!Array.isArray(sections) || !sections.length) {
      const empty = documentRoot.createElement("div");
      empty.className = "lesson-block-meta";
      empty.textContent = "No lesson sections generated yet.";
      elements.core.appendChild(empty);
      return;
    }

    sections.forEach((section) => {
      const block = documentRoot.createElement("div");
      block.className = "lesson-block";

      const title = documentRoot.createElement("div");
      title.className = "lesson-block-title";
      title.textContent = section.heading || "Lesson step";

      const body = documentRoot.createElement("div");
      body.textContent = section.body || "";

      block.appendChild(title);
      block.appendChild(body);

      if (section.checkpoint) {
        const checkpoint = documentRoot.createElement("div");
        checkpoint.className = "lesson-block-meta";
        checkpoint.textContent = `Checkpoint: ${section.checkpoint.prompt}`;
        block.appendChild(checkpoint);
      }

      elements.core.appendChild(block);
    });
  }

  function renderLessonDetails(lesson) {
    if (elements.title) elements.title.textContent = lesson?.title || "Select or start a lesson";
    if (elements.summary) {
      elements.summary.textContent =
        lesson?.summary || "Lessons include a diagnostic pre-quiz, interactive checkpoints, and a mastery post-quiz.";
    }
    renderObjectives(lesson?.objectives);
    renderQuiz(elements.preQuiz, lesson?.preQuiz, "Pre");
    renderSections(lesson?.sections);
    renderQuiz(elements.postQuiz, lesson?.postQuiz, "Post");
    setStage(lesson ? "pre" : "idle");
  }

  function selectLesson(lessonId) {
    state.activeLessonId = lessonId;
    const lesson = state.lessons.find((item) => item.id === lessonId) || null;
    renderLessonList();
    renderLessonDetails(lesson);
  }

  async function loadLessons() {
    if (!api?.getLessons) return;
    const requestId = ++state.requestId;
    setLoading(true);
    renderSkeletons(elements.list, 3);
    if (elements.empty) elements.empty.style.display = "none";
    
    try {
      const payload = await api.getLessons();
      if (requestId !== state.requestId) return;
      state.lessons = Array.isArray(payload?.lessons) ? payload.lessons : [];
      renderLessonList();
      if (state.lessons.length) {
        selectLesson(state.lessons[0].id);
      } else {
        renderLessonDetails(null);
      }
    } catch (error) {
      if (requestId !== state.requestId) return;
      clearElement(elements.list);
      if (elements.empty) {
        elements.empty.textContent = normalizeErrorMessage(error, "Failed to load lessons.");
        elements.empty.style.display = "block";
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleStart() {
    if (!api?.generateLesson) return;
    setLoading(true);
    
    if (!state.lessons.length) {
      try {
        await api.generateLesson();
      } catch (error) {
        if (elements.empty) {
          elements.empty.textContent = normalizeErrorMessage(error, "Failed to generate lesson.");
          elements.empty.style.display = "block";
        }
        setLoading(false);
        return;
      }
      await loadLessons();
      return;
    }

    const lesson = state.lessons.find((item) => item.id === state.activeLessonId) || state.lessons[0];
    if (!lesson) {
      setLoading(false);
      return;
    }
    renderLessonDetails(lesson);
    setLoading(false);
  }

  function handleContinue() {
    if (state.stage === "pre") {
      setStage("core");
    } else if (state.stage === "core") {
      setStage("post");
    } else if (state.stage === "post") {
      setStage("complete");
    }
  }

  function initialize() {
    if (elements.startButton) elements.startButton.addEventListener("click", handleStart);
    if (elements.continueButton) elements.continueButton.addEventListener("click", handleContinue);
    setStage("idle");
    loadLessons();
  }

  return {
    initialize,
    loadLessons,
  };
}

function createExamRenderer(documentRoot, api) {
  const elements = {
    list: documentRoot.querySelector("#examList"),
    empty: documentRoot.querySelector("#examEmpty"),
    title: documentRoot.querySelector("#examTitle"),
    summary: documentRoot.querySelector("#examSummary"),
    meta: documentRoot.querySelector("#examMeta"),
    sections: documentRoot.querySelector("#examSections"),
    generateButton: documentRoot.querySelector("#examGenerateButton"),
  };

  const state = {
    exams: [],
    activeExamId: null,
    requestId: 0,
    isLoading: false,
  };

  function clearElement(target) {
    if (!target) return;
    target.innerHTML = "";
  }

  function renderSkeletons(container, count = 3) {
    if (!container) return;
    clearElement(container);
    for (let i = 0; i < count; i++) {
      const skeleton = documentRoot.createElement("div");
      skeleton.className = "list-item";
      skeleton.innerHTML = `
        <div class="skeleton skeleton-title" style="margin-bottom: 8px;"></div>
        <div class="skeleton skeleton-text" style="width: 50%;"></div>
      `;
      container.appendChild(skeleton);
    }
  }

  function setLoading(isLoading) {
    state.isLoading = isLoading;
    if (elements.generateButton) {
      elements.generateButton.disabled = isLoading;
      elements.generateButton.textContent = isLoading ? "Generating..." : "Generate Exam";
    }
  }

  function renderExamList() {
    if (!elements.list) return;
    clearElement(elements.list);
    if (!state.exams.length) {
      if (elements.empty) elements.empty.style.display = "block";
      return;
    }
    if (elements.empty) elements.empty.style.display = "none";

    state.exams.forEach((exam) => {
      const button = documentRoot.createElement("div");
      button.className = exam.id === state.activeExamId ? "list-item active" : "list-item";

      const title = documentRoot.createElement("div");
      title.className = "list-item-title";
      title.textContent = exam.title || "Practice Exam";

      const meta = documentRoot.createElement("div");
      meta.className = "list-item-meta";
      meta.textContent = exam.createdAt ? `Created ${formatTimestamp(exam.createdAt)}` : "Draft exam";

      button.appendChild(title);
      button.appendChild(meta);
      button.addEventListener("click", () => selectExam(exam.id));
      elements.list.appendChild(button);
    });
  }

  function renderExamSections(exam) {
    if (!elements.sections) return;
    clearElement(elements.sections);
    if (!exam?.sections) {
      const empty = documentRoot.createElement("div");
      empty.className = "lesson-block-meta";
      empty.textContent = "No exam content yet.";
      elements.sections.appendChild(empty);
      return;
    }

    Object.entries(exam.sections).forEach(([sectionName, items]) => {
      const block = documentRoot.createElement("div");
      block.className = "lesson-block";

      const title = documentRoot.createElement("div");
      title.className = "lesson-block-title";
      title.textContent = sectionName.replace(/([A-Z])/g, " $1").trim();
      block.appendChild(title);

      if (Array.isArray(items)) {
        items.forEach((item, index) => {
          const prompt = documentRoot.createElement("div");
          prompt.className = "lesson-block-meta";
          prompt.textContent = `${index + 1}. ${item.prompt || item.stem || ""}`;
          block.appendChild(prompt);
        });
      }

      elements.sections.appendChild(block);
    });
  }

  function renderExamDetails(exam) {
    if (elements.title) elements.title.textContent = exam?.title || "Select an exam";
    if (elements.summary) {
      elements.summary.textContent = exam?.summary || "Each exam blends recall, synthesis, and clinical reasoning.";
    }
    if (elements.meta) {
      elements.meta.textContent = exam?.createdAt ? `Created ${formatTimestamp(exam.createdAt)}` : "Awaiting exam";
    }
    renderExamSections(exam);
  }

  function selectExam(examId) {
    state.activeExamId = examId;
    const exam = state.exams.find((item) => item.id === examId) || null;
    renderExamList();
    renderExamDetails(exam);
  }

  async function loadExams() {
    if (!api?.getExams) return;
    const requestId = ++state.requestId;
    setLoading(true);
    renderSkeletons(elements.list, 3);
    if (elements.empty) elements.empty.style.display = "none";
    
    try {
      const payload = await api.getExams();
      if (requestId !== state.requestId) return;
      state.exams = Array.isArray(payload?.exams) ? payload.exams : [];
      renderExamList();
      if (state.exams.length) {
        selectExam(state.exams[0].id);
      } else {
        renderExamDetails(null);
      }
    } catch (error) {
      if (requestId !== state.requestId) return;
      clearElement(elements.list);
      if (elements.empty) {
        elements.empty.textContent = normalizeErrorMessage(error, "Failed to load exams.");
        elements.empty.style.display = "block";
      }
    } finally {
      setLoading(false);
    }
  }

  async function handleGenerateExam() {
    if (!api?.generateExam) return;
    setLoading(true);
    try {
      await api.generateExam();
      await loadExams();
    } catch (error) {
      if (elements.empty) {
        elements.empty.textContent = normalizeErrorMessage(error, "Failed to generate exam.");
        elements.empty.style.display = "block";
      }
    } finally {
      setLoading(false);
    }
  }

  function initialize() {
    if (elements.generateButton) elements.generateButton.addEventListener("click", handleGenerateExam);
    loadExams();
  }

  return {
    initialize,
    loadExams,
  };
}

function createCommandPalette(documentRoot, navigation) {
  let overlay = null;
  let input = null;
  let results = null;
  let isOpen = false;
  let selectedIndex = 0;

  const commands = [
    { id: "nav-ingest", label: "Go to Ingest", shortcut: "⌘1", action: () => navigation.setActiveView("ingest") },
    { id: "nav-lessons", label: "Go to Lessons", shortcut: "⌘2", action: () => navigation.setActiveView("lessons") },
    { id: "nav-exams", label: "Go to Practice Exams", shortcut: "⌘3", action: () => navigation.setActiveView("exams") },
  ];

  function createPaletteElements() {
    overlay = documentRoot.createElement("div");
    overlay.className = "cmd-palette-overlay";
    overlay.innerHTML = `
      <div class="cmd-palette">
        <div class="cmd-palette-input-wrapper">
          <svg xmlns="http://www.w3.org/2000/svg" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><circle cx="11" cy="11" r="8"/><path d="m21 21-4.3-4.3"/></svg>
          <input type="text" class="cmd-palette-input" placeholder="Type a command..." autocomplete="off" spellcheck="false" />
        </div>
        <div class="cmd-palette-results"></div>
        <div class="cmd-palette-footer">
          <span><kbd>↑↓</kbd> Navigate</span>
          <span><kbd>↵</kbd> Select</span>
          <span><kbd>esc</kbd> Close</span>
        </div>
      </div>
    `;
    documentRoot.body.appendChild(overlay);
    input = overlay.querySelector(".cmd-palette-input");
    results = overlay.querySelector(".cmd-palette-results");

    overlay.addEventListener("click", (e) => {
      if (e.target === overlay) close();
    });

    input.addEventListener("input", () => {
      selectedIndex = 0;
      renderResults();
    });

    input.addEventListener("keydown", handleKeyDown);
  }

  function getFilteredCommands() {
    const query = input?.value?.toLowerCase() || "";
    if (!query) return commands;
    return commands.filter((cmd) => cmd.label.toLowerCase().includes(query));
  }

  function renderResults() {
    if (!results) return;
    const filtered = getFilteredCommands();
    results.innerHTML = filtered
      .map(
        (cmd, i) => `
        <div class="cmd-palette-item ${i === selectedIndex ? "selected" : ""}" data-index="${i}">
          <span class="cmd-palette-item-label">${cmd.label}</span>
          <span class="cmd-palette-item-shortcut">${cmd.shortcut || ""}</span>
        </div>
      `
      )
      .join("");

    results.querySelectorAll(".cmd-palette-item").forEach((item) => {
      item.addEventListener("click", () => {
        const index = parseInt(item.dataset.index, 10);
        executeCommand(index);
      });
    });
  }

  function handleKeyDown(e) {
    const filtered = getFilteredCommands();
    if (e.key === "ArrowDown") {
      e.preventDefault();
      selectedIndex = (selectedIndex + 1) % filtered.length;
      renderResults();
    } else if (e.key === "ArrowUp") {
      e.preventDefault();
      selectedIndex = (selectedIndex - 1 + filtered.length) % filtered.length;
      renderResults();
    } else if (e.key === "Enter") {
      e.preventDefault();
      executeCommand(selectedIndex);
    } else if (e.key === "Escape") {
      e.preventDefault();
      close();
    }
  }

  function executeCommand(index) {
    const filtered = getFilteredCommands();
    const cmd = filtered[index];
    if (cmd?.action) {
      cmd.action();
      close();
    }
  }

  function open() {
    if (!overlay) createPaletteElements();
    isOpen = true;
    overlay.classList.add("is-open");
    input.value = "";
    selectedIndex = 0;
    renderResults();
    setTimeout(() => input?.focus(), 50);
  }

  function close() {
    if (!overlay) return;
    isOpen = false;
    overlay.classList.remove("is-open");
  }

  function toggle() {
    isOpen ? close() : open();
  }

  return { open, close, toggle, isOpen: () => isOpen };
}

function setupKeyboardShortcuts(documentRoot, navigation, commandPalette) {
  documentRoot.addEventListener("keydown", (e) => {
    const isMac = navigator.platform.toUpperCase().includes("MAC");
    const mod = isMac ? e.metaKey : e.ctrlKey;

    // Command Palette: Cmd/Ctrl + K
    if (mod && e.key.toLowerCase() === "k") {
      e.preventDefault();
      commandPalette.toggle();
      return;
    }

    // Navigation: Cmd/Ctrl + 1/2/3
    if (mod && e.key === "1") {
      e.preventDefault();
      navigation.setActiveView("ingest");
      return;
    }
    if (mod && e.key === "2") {
      e.preventDefault();
      navigation.setActiveView("lessons");
      return;
    }
    if (mod && e.key === "3") {
      e.preventDefault();
      navigation.setActiveView("exams");
      return;
    }

    // Escape to close palette
    if (e.key === "Escape" && commandPalette.isOpen()) {
      e.preventDefault();
      commandPalette.close();
    }
  });
}

function initializeApp() {
  if (typeof document === "undefined") return;
  const api = typeof window !== "undefined" ? window.catApi : null;

  const navigation = setupNavigation(document);
  const commandPalette = createCommandPalette(document, navigation);
  setupKeyboardShortcuts(document, navigation, commandPalette);

  const uploadRenderer = createUploadRenderer(document, api);
  const lessonsRenderer = createLessonsRenderer(document, api);
  const examRenderer = createExamRenderer(document, api);

  uploadRenderer.initialize();
  lessonsRenderer.initialize();
  examRenderer.initialize();
}

initializeApp();
