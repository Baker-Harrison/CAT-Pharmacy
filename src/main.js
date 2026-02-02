/**
 * CAT-Pharmacy Electron Main Process
 * Handles application lifecycle, IPC communication, and Python backend integration.
 */

const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const { spawn } = require('child_process');
const { randomUUID } = require('crypto');
const fs = require('fs');
const path = require('path');

// Resolve Python executable based on platform
const DEFAULT_PYTHON = process.env.PYTHON || (process.platform === 'win32' ? 'python' : 'python3');

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

    let stdout = '';
    let stderr = '';

    python.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    python.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    if (stdinData) {
      python.stdin.write(stdinData);
      python.stdin.end();
    }

    python.on('error', (error) => {
      console.error(`Failed to start Python process (${module}):`, error);
      reject(new Error(`Failed to start background engine: ${error.message}`));
    });

    python.on('close', (code) => {
      if (code !== 0) {
        console.error(`Python process (${module}) exited with code ${code}. Error: ${stderr}`);
        reject(new Error(stderr.trim() || `Background engine exited with code ${code}`));
        return;
      }
      try {
        const parsed = JSON.parse(stdout);
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
  fs.writeFileSync(filePath, JSON.stringify(payload, null, 2), 'utf-8');
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
  if (process.platform !== 'darwin') app.quit();
});
