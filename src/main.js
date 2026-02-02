/**
 * CAT-Pharmacy Electron Main Process
 * Handles application lifecycle, IPC communication, and Python backend integration.
 */

const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const { spawn } = require('child_process');
const { randomUUID } = require('crypto');
const fs = require('fs');
const path = require('path');

const activePythonProcesses = new Set();
let mainWindow = null;

// Resolve Python executable based on platform
const DEFAULT_PYTHON = process.env.PYTHON || (process.platform === 'win32' ? 'python' : 'python3');

function sleepSync(ms) {
  Atomics.wait(new Int32Array(new SharedArrayBuffer(4)), 0, 0, ms);
}

function withFileLock(targetPath, work, timeoutMs = 2000) {
  const lockPath = `${targetPath}.lock`;
  const startedAt = Date.now();
  while (true) {
    try {
      const fd = fs.openSync(lockPath, 'wx');
      try {
        return work();
      } finally {
        fs.closeSync(fd);
        fs.unlinkSync(lockPath);
      }
    } catch (error) {
      if (error.code !== 'EEXIST') {
        throw error;
      }
      if (Date.now() - startedAt >= timeoutMs) {
        throw new Error('Database locked');
      }
      sleepSync(50);
    }
  }
}

function writeJsonAtomic(filePath, payload) {
  const dir = path.dirname(filePath);
  fs.mkdirSync(dir, { recursive: true });
  const tempPath = path.join(
    dir,
    `.${path.basename(filePath)}.${process.pid}.${Date.now()}.tmp`
  );

  return withFileLock(filePath, () => {
    try {
      fs.writeFileSync(tempPath, JSON.stringify(payload, null, 2), 'utf-8');
      try {
        fs.renameSync(tempPath, filePath);
      } catch (error) {
        if (error.code === 'EEXIST' || error.code === 'EPERM') {
          if (fs.existsSync(filePath)) {
            fs.unlinkSync(filePath);
          }
          fs.renameSync(tempPath, filePath);
        } else {
          throw error;
        }
      }
    } finally {
      if (fs.existsSync(tempPath)) {
        fs.unlinkSync(tempPath);
      }
    }
  });
}

function trackPythonProcess(processRef) {
  activePythonProcesses.add(processRef);
  const cleanup = () => activePythonProcesses.delete(processRef);
  processRef.once('exit', cleanup);
  processRef.once('close', cleanup);
  processRef.once('error', cleanup);
}

function terminatePythonProcess(processRef) {
  if (!processRef || processRef.killed) return;
  try {
    processRef.kill('SIGTERM');
  } catch (error) {
    return;
  }
  setTimeout(() => {
    if (!processRef.killed) {
      try {
        processRef.kill('SIGKILL');
      } catch (killError) {
        // Best effort only.
      }
    }
  }, 2000);
}

function terminateAllPythonProcesses() {
  for (const processRef of activePythonProcesses) {
    terminatePythonProcess(processRef);
  }
  activePythonProcesses.clear();
}

function normalizePythonErrorMessage(raw) {
  const message = String(raw || '');
  const lower = message.toLowerCase();
  if (lower.includes('enoent') || lower.includes('python') && lower.includes('not found')) {
    return 'PYTHON_NOT_FOUND: Python executable not found.';
  }
  if (
    lower.includes('pptx') && lower.includes('required') ||
    lower.includes('badzipfile') ||
    lower.includes('package') && lower.includes('not found') ||
    lower.includes('file is not a zip file')
  ) {
    return 'PPTX_INVALID: PPTX format invalid.';
  }
  if (lower.includes('database locked') || lower.includes('lock')) {
    return 'DATABASE_LOCKED: Database locked.';
  }
  return message;
}

function parseJsonFromStdout(stdout) {
  const trimmed = String(stdout || '').trim();
  if (!trimmed) {
    throw new Error('Background engine returned no data.');
  }
  try {
    return JSON.parse(trimmed);
  } catch (error) {
    // Attempt to extract the last JSON object/array from mixed stdout.
    const endIndex = Math.max(trimmed.lastIndexOf('}'), trimmed.lastIndexOf(']'));
    if (endIndex === -1) {
      throw error;
    }
    for (let i = endIndex; i >= 0; i -= 1) {
      const char = trimmed[i];
      if (char !== '{' && char !== '[') continue;
      const slice = trimmed.slice(i, endIndex + 1);
      try {
        return JSON.parse(slice);
      } catch (innerError) {
        // Keep scanning for a valid JSON boundary.
      }
    }
    throw error;
  }
}

/**
 * Creates the main application window with professional desktop settings.
 */
function createWindow() {
  const win = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1100,
    minHeight: 720,
    backgroundColor: '#0f1412',
    title: 'CAT-Pharmacy Mastery Engine',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  win.loadFile(path.join(__dirname, 'index.html'));
  win.on('closed', () => {
    terminateAllPythonProcesses();
  });
  mainWindow = win;
}

/**
 * Executes a Python module with given arguments and returns parsed JSON result.
 * @param {string} module - Python module name (e.g., 'backend.parser')
 * @param {string[]} args - Command line arguments for the module
 * @param {string} [stdinData] - Optional data to write to Python's stdin
 * @returns {Promise<any>} Parsed JSON output from the Python script
 */
function runPython(module, args = [], stdinData = null) {
  return new Promise((resolve, reject) => {
    const pythonArgs = ['-m', module, ...args];
    const python = spawn(DEFAULT_PYTHON, pythonArgs, {
      cwd: path.resolve(__dirname, '..'),
      stdio: [stdinData ? 'pipe' : 'ignore', 'pipe', 'pipe'],
    });

    trackPythonProcess(python);

    const stdoutChunks = [];
    const stderrChunks = [];

    python.stdout.on('data', (data) => {
      stdoutChunks.push(data);
    });

    python.stderr.on('data', (data) => {
      stderrChunks.push(data);
    });

    if (stdinData) {
      python.stdin.write(stdinData);
      python.stdin.end();
    }

    python.on('error', (error) => {
      console.error(`Failed to start Python process (${module}):`, error);
      reject(new Error(normalizePythonErrorMessage(`Failed to start background engine: ${error.message}`)));
    });

    python.on('close', (code) => {
      const stdout = Buffer.concat(stdoutChunks).toString('utf-8');
      const stderr = Buffer.concat(stderrChunks).toString('utf-8');
      if (code !== 0) {
        console.error(`Python process (${module}) exited with code ${code}. Error: ${stderr}`);
        reject(new Error(normalizePythonErrorMessage(stderr.trim() || `Background engine exited with code ${code}`)));
        return;
      }
      if (stderr.trim()) {
        console.warn(`Python process (${module}) stderr:`, stderr.trim());
      }
      try {
        const parsed = parseJsonFromStdout(stdout);
        resolve(parsed);
      } catch (error) {
        console.error(`Failed to parse Python output as JSON:`, stdout);
        reject(new Error('Background engine returned invalid data format.'));
      }
    });
  });
}

/**
 * Ensures the application data directory exists.
 * @returns {string} Path to the data directory
 */
function ensureDataDir() {
  const dataDir = path.join(app.getPath('userData'), 'data');
  fs.mkdirSync(dataDir, { recursive: true });
  return dataDir;
}

/**
 * Helper to write objects to JSON files with consistent formatting.
 */
function writeJson(filePath, payload) {
  writeJsonAtomic(filePath, payload);
}

/**
 * Builds a skeleton knowledge graph from parsed units.
 */
function buildGraphFromUnits(units) {
  const nodes = (Array.isArray(units) ? units : []).map((unit) => ({
    id: unit?.id || randomUUID(),
    title: unit?.topic || unit?.summary || 'Untitled topic',
    type: 'Concept',
  }));

  return { Nodes: nodes, Edges: [] };
}

/**
 * Wrapper for IPC handlers to provide centralized error handling and logging.
 */
async function handleIpc(name, handler) {
  ipcMain.handle(name, async (event, ...args) => {
    try {
      return await handler(event, ...args);
    } catch (error) {
      console.error(`IPC Error in [${name}]:`, error);
      throw error; // Re-throw so the renderer receives the error
    }
  });
}

app.whenReady().then(() => {
  createWindow();

  // Dialog Handlers
  handleIpc('dialog:openPptx', async () => {
    const result = await dialog.showOpenDialog({
      properties: ['openFile'],
      filters: [{ name: 'PowerPoint', extensions: ['pptx'] }],
    });
    return result.canceled ? null : result.filePaths[0];
  });

  // Processing Handlers
  handleIpc('process-upload', async (event, filePath) => {
    if (!filePath) throw new Error('No file path provided');

    const dataDir = ensureDataDir();
    event.sender.send('upload:status', { message: 'Parsing PPTX...', tone: 'neutral', state: 'busy' });

    const units = await runPython('backend.parser', [filePath]);
    const unitsPath = path.join(dataDir, 'knowledge-units.json');
    const graphPath = path.join(dataDir, `knowledge-graph-${Date.now()}.json`);
    
    writeJson(unitsPath, { updatedAt: new Date().toISOString(), sourceFile: filePath, units });
    writeJson(graphPath, buildGraphFromUnits(units));

    event.sender.send('upload:status', { 
      message: 'Upload complete', 
      tone: 'success', 
      state: 'idle', 
      fileName: path.basename(filePath) 
    });

    return { unitCount: units.length, summary: await runPython('backend.session', ['--summary', '--data-dir', dataDir]) };
  });

  // Backend Sync Handlers
  handleIpc('backend:sync', () => runPython('backend.session', ['--summary', '--data-dir', ensureDataDir()]));
  handleIpc('lessons:list', () => runPython('backend.lessons', ['--data-dir', ensureDataDir()]));
  handleIpc('learning:start', () => runPython('backend.session', ['--process-response', '--data-dir', ensureDataDir()], JSON.stringify({ action: 'start' })));
  handleIpc('learning:processResponse', (event, payload) =>
    runPython('backend.session', ['--process-response', '--data-dir', ensureDataDir()], JSON.stringify(payload))
  );

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
});

app.on('window-all-closed', () => {
  terminateAllPythonProcesses();
  if (process.platform !== 'darwin') app.quit();
});

app.on('before-quit', () => {
  terminateAllPythonProcesses();
});
