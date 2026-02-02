const assert = require("node:assert/strict");
const { createUploadRenderer } = require("./renderer");

function createMockElement(tagName = "div") {
  const listeners = new Map();
  const element = {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: {},
    style: {},
    className: "",
    textContent: "",
    files: null,
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

  element.click = () => {
    element.dispatchEvent({ type: "click" });
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

  register("uploadDropzone");
  register("uploadInput", "input");
  register("uploadStatus");
  register("uploadFileName");
  register("openUpload", "button");
  register("uploadView");

  return {
    querySelector: (selector) => elements.get(selector) || null,
    createElement: (tagName) => createMockElement(tagName),
  };
}

const mockDocument = createMockDocument();
const calls = [];
const mockApi = {
  processUpload: (filePath) => {
    calls.push(filePath);
    return Promise.resolve({ unitCount: 3 });
  },
  onUploadStatus: () => {},
};

const uploader = createUploadRenderer(mockDocument, mockApi);
uploader.initialize();

const input = mockDocument.querySelector("#uploadInput");
input.files = [{ path: "/tmp/sample.pptx", name: "sample.pptx" }];
input.dispatchEvent({ type: "change" });

assert.equal(calls.length, 1);
assert.equal(calls[0], "/tmp/sample.pptx");
