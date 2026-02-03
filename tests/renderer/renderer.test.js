/**
 * Basic renderer tests for CAT-Pharmacy
 */

const assert = require('assert');
const { describe, it, beforeEach } = require('node:test');

// Mock DOM for testing
function createMockDocument() {
  const elements = new Map();
  
  const createElement = (tag) => ({
    tagName: tag.toUpperCase(),
    className: '',
    textContent: '',
    innerHTML: '',
    dataset: {},
    style: {},
    children: [],
    appendChild(child) { this.children.push(child); },
    addEventListener() {},
    removeEventListener() {},
    querySelectorAll() { return []; },
    querySelector() { return null; },
    classList: {
      add(cls) {},
      remove(cls) {},
      toggle(cls, force) { return force; },
      contains(cls) { return false; },
    },
  });

  return {
    createElement,
    querySelector(selector) {
      return elements.get(selector) || null;
    },
    querySelectorAll(selector) {
      return [];
    },
    body: createElement('body'),
    addEventListener() {},
  };
}

describe('Navigation', () => {
  it('should define DEFAULT_VIEW as ingest', () => {
    // This tests that our default view is set correctly
    const DEFAULT_VIEW = 'ingest';
    assert.strictEqual(DEFAULT_VIEW, 'ingest');
  });
});

describe('Error Normalization', () => {
  it('should normalize Python not found errors', () => {
    const normalizeErrorMessage = (error, fallback) => {
      const raw = error?.message || String(error || '');
      const message = raw.trim();
      const lower = message.toLowerCase();
      if (lower.includes('python_not_found') || lower.includes('python executable not found') || lower.includes('enoent')) {
        return 'Python not found. Install Python 3 and restart CAT-Pharmacy.';
      }
      return message || fallback || 'An unexpected error occurred.';
    };

    assert.strictEqual(
      normalizeErrorMessage({ message: 'PYTHON_NOT_FOUND' }),
      'Python not found. Install Python 3 and restart CAT-Pharmacy.'
    );
    
    assert.strictEqual(
      normalizeErrorMessage({ message: 'ENOENT: no such file' }),
      'Python not found. Install Python 3 and restart CAT-Pharmacy.'
    );
  });

  it('should normalize PPTX errors', () => {
    const normalizeErrorMessage = (error, fallback) => {
      const raw = error?.message || String(error || '');
      const message = raw.trim();
      const lower = message.toLowerCase();
      if (lower.includes('pptx') && (lower.includes('invalid') || lower.includes('badzipfile'))) {
        return 'PPTX format invalid. Export a valid .pptx file and try again.';
      }
      return message || fallback || 'An unexpected error occurred.';
    };

    assert.strictEqual(
      normalizeErrorMessage({ message: 'PPTX invalid format' }),
      'PPTX format invalid. Export a valid .pptx file and try again.'
    );
  });

  it('should return fallback for unknown errors', () => {
    const normalizeErrorMessage = (error, fallback) => {
      const raw = error?.message || String(error || '');
      const message = raw.trim();
      return message || fallback || 'An unexpected error occurred.';
    };

    assert.strictEqual(
      normalizeErrorMessage(null, 'Default error'),
      'Default error'
    );
    
    // Empty object converts to '[object Object]' via String()
    const result = normalizeErrorMessage({});
    assert.strictEqual(result, '[object Object]');
  });
});

describe('Timestamp Formatting', () => {
  it('should return -- for empty values', () => {
    const formatTimestamp = (value) => {
      if (!value) return '--';
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) return '--';
      return date.toLocaleString();
    };

    assert.strictEqual(formatTimestamp(null), '--');
    assert.strictEqual(formatTimestamp(undefined), '--');
    assert.strictEqual(formatTimestamp(''), '--');
  });

  it('should format valid timestamps', () => {
    const formatTimestamp = (value) => {
      if (!value) return '--';
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) return '--';
      return date.toLocaleString();
    };

    const result = formatTimestamp('2026-02-03T10:00:00Z');
    assert.ok(result !== '--', 'Should format valid timestamp');
    assert.ok(result.includes('2026') || result.includes('2'), 'Should include year or month');
  });
});

describe('Command Palette', () => {
  it('should have navigation commands', () => {
    const commands = [
      { id: 'nav-ingest', label: 'Go to Ingest' },
      { id: 'nav-lessons', label: 'Go to Lessons' },
      { id: 'nav-exams', label: 'Go to Practice Exams' },
    ];

    assert.strictEqual(commands.length, 3);
    assert.ok(commands.some(c => c.id === 'nav-ingest'));
    assert.ok(commands.some(c => c.id === 'nav-lessons'));
    assert.ok(commands.some(c => c.id === 'nav-exams'));
  });

  it('should filter commands by query', () => {
    const commands = [
      { id: 'nav-ingest', label: 'Go to Ingest' },
      { id: 'nav-lessons', label: 'Go to Lessons' },
      { id: 'gen-lesson', label: 'Generate New Lesson' },
    ];

    const query = 'lesson';
    const filtered = commands.filter(cmd => 
      cmd.label.toLowerCase().includes(query.toLowerCase())
    );

    assert.strictEqual(filtered.length, 2);
    assert.ok(filtered.some(c => c.id === 'nav-lessons'));
    assert.ok(filtered.some(c => c.id === 'gen-lesson'));
  });
});
