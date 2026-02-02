const assert = require("node:assert/strict");
const { createDashboardRenderer } = require("../../src/renderer");

function createMockElement(tagName = "div") {
  const element = {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: {},
    style: {},
    className: "",
    textContent: "",
  };

  element.style.setProperty = (key, value) => {
    element.style[key] = value;
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
  const register = (id) => {
    const el = createMockElement();
    elements.set(`#${id}`, el);
    return el;
  };

  register("nodeCount");
  register("edgeCount");
  register("sidebarNodeCount");
  register("sidebarEdgeCount");
  register("graphSource");
  register("lastUpdated");
  register("typeList");
  register("pulseSummary");
  register("masteryList");
  register("recentTopics");

  return {
    querySelector: (selector) => elements.get(selector) || null,
    createElement: (tagName) => createMockElement(tagName),
  };
}

const mockDocument = createMockDocument();
const dashboard = createDashboardRenderer(mockDocument);

dashboard.renderSummary({
  nodeCount: 128,
  edgeCount: 412,
  nodeTypes: { Concept: 88, Skill: 40 },
  masteryLevels: { Advanced: 12, Proficient: 38, Developing: 44, Novice: 24, Unknown: 10 },
  source: "mock-graph.json",
  lastUpdated: "2025-01-12T10:30:00Z",
  recentTopics: [
    { title: "Cardiology Foundations", level: "Proficient", lastAssessed: "2025-01-11T09:00:00Z" },
    { title: "Renal Pharmacokinetics", level: "Developing", lastAssessed: "2025-01-10T16:22:00Z" },
  ],
});

assert.equal(mockDocument.querySelector("#nodeCount").textContent, "128");
assert.equal(mockDocument.querySelector("#edgeCount").textContent, "412");
assert.equal(mockDocument.querySelector("#graphSource").textContent, "mock-graph.json");

const masteryList = mockDocument.querySelector("#masteryList");
assert.equal(masteryList.children.length, 5);
assert.equal(masteryList.children[0].children[0].textContent, "Advanced");

const recentTopics = mockDocument.querySelector("#recentTopics");
assert.equal(recentTopics.children.length, 2);
assert.equal(recentTopics.children[0].children[0].children[0].textContent, "Cardiology Foundations");
