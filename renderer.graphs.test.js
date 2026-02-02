const test = require("node:test");
const assert = require("node:assert/strict");

const { InteractiveGraph } = require("./renderer");

function createMockElement(tagName = "div") {
  const element = {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: {},
    style: {},
    className: "",
    textContent: "",
    attributes: {},
    appendChild(child) {
      this.children.push(child);
      return child;
    },
    setAttribute(name, value) {
      this.attributes[name] = String(value);
    },
    getAttribute(name) {
      return this.attributes[name];
    },
    addEventListener() {},
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
  return {
    createElement: (tagName) => createMockElement(tagName),
    createElementNS: (namespace, tagName) => createMockElement(tagName),
  };
}

test("InteractiveGraph recalculates SVG path when parameters change", () => {
  const mockDocument = createMockDocument();
  const container = createMockElement("div");

  const graph = new InteractiveGraph({
    rootDocument: mockDocument,
    container,
    functionLogic: "A * exp(-k * t)",
    width: 400,
    height: 260,
    xRange: [0, 10],
    parameters: [
      { name: "A", min: 1, max: 10, step: 0.5, value: 8, precision: 2 },
      { name: "k", min: 0.05, max: 1.2, step: 0.05, value: 0.2, precision: 2 },
    ],
  });

  const initialPath = graph.getPathData();
  graph.updateParameter("k", 0.9);
  const updatedPath = graph.getPathData();

  assert.ok(initialPath.length > 0);
  assert.notEqual(updatedPath, initialPath);
});
