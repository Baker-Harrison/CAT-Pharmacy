const test = require("node:test");
const assert = require("node:assert/strict");

const { InteractiveGraph, PredictivePlot } = require("../../src/renderer");

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

test("PredictivePlot renders line path when data is provided", () => {
  const mockDocument = createMockDocument();
  const container = createMockElement("div");

  const plot = new PredictivePlot({
    rootDocument: mockDocument,
    container,
    width: 360,
    height: 200,
  });

  plot.setData({
    horizon: 3,
    baselineTheta: -1.0,
    baselineStandardError: 0.4,
    finalTheta: -0.6,
    points: [
      { step: 1, expectedTheta: -0.9, lowerTheta: -1.3, upperTheta: -0.5 },
      { step: 2, expectedTheta: -0.75, lowerTheta: -1.1, upperTheta: -0.4 },
      { step: 3, expectedTheta: -0.6, lowerTheta: -1.0, upperTheta: -0.2 },
    ],
  });

  const linePath = plot.getLinePathData();
  assert.ok(linePath.length > 0);

  plot.setData(null);
  assert.equal(plot.getLinePathData(), "");
});
