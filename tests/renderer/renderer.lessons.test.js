const assert = require("node:assert/strict");
const { createLessonsRenderer } = require("./renderer");

function createMockElement(tagName = "div") {
  const listeners = new Map();
  const element = {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: {},
    style: {},
    className: "",
    textContent: "",
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

function createMockDocument() {
  const elements = new Map();
  const register = (id, tagName) => {
    const el = createMockElement(tagName);
    elements.set(`#${id}`, el);
    return el;
  };

  register("lessonsList");
  register("lessonEmpty");
  register("lessonTitle");
  register("lessonSummary");
  register("lessonSlides");
  register("lessonKeyPoints");
  register("lessonMeta");

  return {
    querySelector: (selector) => elements.get(selector) || null,
    createElement: (tagName) => createMockElement(tagName),
  };
}

async function runTest() {
  const mockDocument = createMockDocument();
  const mockApi = {
    getLessons: async () => ({
      lessons: [
        {
          Id: "lesson-1",
          Title: "Cardiology Basics",
          Summary: "Review core cardiology concepts.",
          EstimatedReadMinutes: 18,
          ProgressPercent: 45,
          KeyPoints: ["Cardiac output", "Vascular resistance"],
          Sections: [
            {
              Heading: "Slide 1",
              Body: "Introduction to cardiac physiology.",
              Prompts: [{ Prompt: "Define cardiac output." }],
            },
          ],
        },
      ],
    }),
  };

  const lessonsRenderer = createLessonsRenderer(mockDocument, mockApi);
  await lessonsRenderer.loadLessons();

  const list = mockDocument.querySelector("#lessonsList");
  assert.equal(list.children.length, 1);

  const title = mockDocument.querySelector("#lessonTitle");
  assert.equal(title.textContent, "Cardiology Basics");

  const summary = mockDocument.querySelector("#lessonSummary");
  assert.equal(summary.textContent, "Review core cardiology concepts.");

  const keyPoints = mockDocument.querySelector("#lessonKeyPoints");
  assert.equal(keyPoints.children.length, 2);
  assert.equal(keyPoints.children[0].textContent, "Cardiac output");
}

runTest().catch((error) => {
  console.error(error);
  process.exitCode = 1;
});
