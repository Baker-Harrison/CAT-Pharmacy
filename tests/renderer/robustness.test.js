const test = require("node:test");
const assert = require("node:assert/strict");

const {
  createUploadRenderer,
  createDashboardRenderer,
  createLessonsRenderer,
  createLearningRenderer,
} = require("../../src/renderer");

function createMockElement(tagName = "div") {
  const listeners = new Map();
  const element = {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: {},
    style: {},
    className: "",
    textContent: "",
    value: "",
    disabled: false,
    files: null,
  };

  element.style.setProperty = (key, value) => {
    element.style[key] = value;
  };

  element.addEventListener = (event, handler) => {
    if (!listeners.has(event)) {
      listeners.set(event, []);
    }
    listeners.get(event).push(handler);
  };

  element.dispatchEvent = (event) => {
    const handlers = listeners.get(event.type) || [];
    handlers.forEach((handler) => handler(event));
  };

  element.appendChild = (child) => {
    element.children.push(child);
    return child;
  };

  Object.defineProperty(element, "innerHTML", {
    get() {
      return "";
    },
    set() {
      element.children = [];
    },
  });

  return element;
}

function createMockDocument(ids) {
  const elements = new Map();
  ids.forEach((id) => {
    elements.set(`#${id}`, createMockElement());
  });
  return {
    querySelector: (selector) => elements.get(selector) || null,
    createElement: (tagName) => createMockElement(tagName),
  };
}

test("upload renderer handles null API response gracefully", async () => {
  const doc = createMockDocument([
    "uploadDropzone",
    "uploadInput",
    "uploadStatus",
    "uploadFileName",
    "openUpload",
    "uploadView",
  ]);

  const api = {
    processUpload: async () => null,
    onUploadStatus: () => {},
  };

  const uploader = createUploadRenderer(doc, api);
  uploader.initialize();

  await uploader.handleFilePath("/tmp/sample.pptx", "sample.pptx");
  assert.equal(doc.querySelector("#uploadStatus").textContent, "Upload complete, but no summary returned.");
});

test("dashboard sync shows Python not found and resets button", async () => {
  const doc = createMockDocument([
    "syncButton",
    "syncStatus",
    "nodeCount",
    "edgeCount",
    "sidebarNodeCount",
    "sidebarEdgeCount",
    "sidebarMasteryFill",
    "graphSource",
    "lastUpdated",
    "lastStudied",
    "timeToMastery",
    "dueCount",
    "nextReviewAt",
    "typeList",
    "pulseSummary",
    "masteryList",
    "recentTopics",
  ]);

  global.window = {
    catApi: {
      syncBackend: async () => {
        throw new Error("PYTHON_NOT_FOUND: Python executable not found.");
      },
    },
  };

  const dashboard = createDashboardRenderer(doc);
  await dashboard.syncBackend();

  assert.equal(doc.querySelector("#syncStatus").textContent, "Python not found. Install Python 3 and restart CAT-Pharmacy.");
  assert.equal(doc.querySelector("#syncButton").disabled, false);
});

test("lessons renderer handles null lessons payload", async () => {
  const doc = createMockDocument([
    "lessonsList",
    "lessonEmpty",
    "lessonTitle",
    "lessonSummary",
    "lessonSlides",
    "lessonKeyPoints",
    "lessonMeta",
  ]);

  const api = {
    getLessons: async () => null,
  };

  const lessons = createLessonsRenderer(doc, api);
  await lessons.loadLessons();

  assert.equal(doc.querySelector("#lessonEmpty").textContent, "No lessons available yet. Generate lessons after ingesting content.");
});

test("learning renderer surfaces network disconnect errors", async () => {
  const doc = createMockDocument([
    "learningFeedback",
    "learningShell",
    "learningPhase",
    "learningPhaseChip",
    "learningPrompt",
    "learningAnswer",
    "learningProgressText",
    "learningProgressFill",
    "learningConceptTitle",
    "learningConceptSummary",
    "learningKeyPoints",
    "learningStartAssessment",
    "learningSubmitAnswer",
    "learningContinue",
  ]);

  const api = {
    startLearning: async () => {
      throw new Error("network disconnected");
    },
    processLearningResponse: async () => ({}),
  };

  const learning = createLearningRenderer(doc, api);
  await learning.startSession();

  assert.equal(doc.querySelector("#learningFeedback").textContent, "Network connection lost. Check your connection and try again.");
});

test("dashboard sync ignores rapid repeat calls", async () => {
  const doc = createMockDocument([
    "syncButton",
    "syncStatus",
    "nodeCount",
    "edgeCount",
    "sidebarNodeCount",
    "sidebarEdgeCount",
    "sidebarMasteryFill",
    "graphSource",
    "lastUpdated",
    "lastStudied",
    "timeToMastery",
    "dueCount",
    "nextReviewAt",
    "typeList",
    "pulseSummary",
    "masteryList",
    "recentTopics",
  ]);

  let resolvePromise;
  const gate = new Promise((resolve) => {
    resolvePromise = resolve;
  });
  let calls = 0;

  global.window = {
    catApi: {
      syncBackend: async () => {
        calls += 1;
        await gate;
        return {
          nodeCount: 0,
          edgeCount: 0,
          nodeTypes: {},
          masteryLevels: {},
          recentTopics: [],
          source: "test",
          lastUpdated: null,
        };
      },
    },
  };

  const dashboard = createDashboardRenderer(doc);
  const first = dashboard.syncBackend();
  dashboard.syncBackend();

  resolvePromise();
  await first;

  assert.equal(calls, 1);
});
