const assert = require("node:assert/strict");
const { test } = require("node:test");
const { createNavigationManager } = require("../../src/renderer");

function createMockElement(tagName = "div") {
  return {
    tagName: tagName.toUpperCase(),
    children: [],
    dataset: {},
    classList: {
      list: new Set(),
      add(cls) { this.list.add(cls); },
      remove(cls) { this.list.delete(cls); },
      contains(cls) { return this.list.has(cls); }
    },
    addEventListener(event, handler) {
      if (!this.handlers) this.handlers = {};
      if (!this.handlers[event]) this.handlers[event] = [];
      this.handlers[event].push(handler);
    },
    click() {
      if (this.handlers && this.handlers.click) {
        this.handlers.click.forEach(h => h());
      }
    }
  };
}

function createMockDocument() {
  const main = createMockElement("main");
  const navItems = [
    { ...createMockElement("button"), dataset: { view: "home" } },
    { ...createMockElement("button"), dataset: { view: "ingest" } },
    { ...createMockElement("button"), dataset: { view: "analytics" } }
  ];
  
  const headerPill = createMockElement();
  const headerTitle = createMockElement();
  const headerSub = createMockElement();

  return {
    querySelector: (selector) => {
      if (selector === "main") return main;
      if (selector === ".main-header .pill") return headerPill;
      if (selector === ".main-header h1") return headerTitle;
      if (selector === ".main-header .subtitle") return headerSub;
      return null;
    },
    querySelectorAll: (selector) => {
      if (selector === ".nav-item") return navItems;
      return [];
    },
    getElementById: (id) => null,
    main,
    navItems,
    headerPill,
    headerTitle,
    headerSub
  };
}

test("NavigationManager switches views and updates header", () => {
  const doc = createMockDocument();
  const nav = createNavigationManager(doc);
  nav.initialize();

  // Initial state (home)
  assert.equal(doc.main.dataset.activeView, "home");
  assert.equal(doc.headerTitle.textContent, "Learning Dashboard");
  assert.ok(doc.navItems[0].classList.contains("active"));

  // Switch to analytics
  nav.switchView("analytics");
  assert.equal(doc.main.dataset.activeView, "analytics");
  assert.equal(doc.headerTitle.textContent, "Performance Insights");
  assert.ok(doc.navItems[2].classList.contains("active"));
  assert.ok(!doc.navItems[0].classList.contains("active"));
});

test("NavigationManager handles click events on nav items", () => {
  const doc = createMockDocument();
  const nav = createNavigationManager(doc);
  nav.initialize();

  // Click ingest
  doc.navItems[1].click();
  assert.equal(doc.main.dataset.activeView, "ingest");
  assert.equal(doc.headerTitle.textContent, "Content Ingestion");
});
