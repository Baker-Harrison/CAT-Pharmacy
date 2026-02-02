const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const { spawn } = require('child_process');
const { randomUUID } = require('crypto');
const fs = require('fs');
const path = require('path');

const DEFAULT_PYTHON = process.env.PYTHON || (process.platform === 'win32' ? 'python' : 'python3');

function createWindow() {
  const win = new BrowserWindow({
    width: 1400,
    height: 900,
    minWidth: 1100,
    minHeight: 720,
    backgroundColor: '#0f1412',
    webPreferences: {
      preload: path.join(__dirname, 'preload.js'),
      contextIsolation: true,
      nodeIntegration: false,
    },
  });

  win.loadFile(path.join(__dirname, 'index.html'));
}

function runPythonParser(filePath) {
  return new Promise((resolve, reject) => {
    const args = ['-m', 'backend.parser', filePath];
    const python = spawn(DEFAULT_PYTHON, args, {
      cwd: path.resolve(__dirname, '..'),
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    let stdout = '';
    let stderr = '';

    python.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    python.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    python.on('error', (error) => {
      reject(error);
    });

    python.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(stderr || `Parser exited with code ${code}`));
        return;
      }
      try {
        const parsed = JSON.parse(stdout);
        resolve(parsed);
      } catch (error) {
        reject(error);
      }
    });
  });
}

function runPythonGraphSummary(dataDir) {
  return new Promise((resolve, reject) => {
    const args = ['-m', 'backend.session', '--summary'];
    if (dataDir) {
      args.push('--data-dir', dataDir);
    } else if (process.env.CAT_DATA_DIR) {
      args.push('--data-dir', process.env.CAT_DATA_DIR);
    }

    const python = spawn(DEFAULT_PYTHON, args, {
      cwd: path.resolve(__dirname, '..'),
      stdio: ['ignore', 'pipe', 'pipe'],
    });

    let stdout = '';
    let stderr = '';

    python.stdout.on('data', (data) => {
      stdout += data.toString();
    });

    python.stderr.on('data', (data) => {
      stderr += data.toString();
    });

    python.on('error', (error) => {
      reject(error);
    });

    python.on('close', (code) => {
      if (code !== 0) {
        reject(new Error(stderr || `Summary exited with code ${code}`));
        return;
      }
      try {
        const parsed = JSON.parse(stdout);
        resolve(parsed);
      } catch (error) {
        reject(error);
      }
    });
  });
}

function ensureDataDir() {
  const dataDir = path.join(app.getPath('userData'), 'data');
  fs.mkdirSync(dataDir, { recursive: true });
  return dataDir;
}

function writeJson(filePath, payload) {
  fs.writeFileSync(filePath, JSON.stringify(payload, null, 2), 'utf-8');
}

function buildGraphFromUnits(units) {
  const nodes = (Array.isArray(units) ? units : []).map((unit) => {
    const nodeId = unit?.id || randomUUID();
    return {
      id: nodeId,
      title: unit?.topic || unit?.summary || 'Untitled topic',
      type: 'Concept',
    };
  });

  return {
    Nodes: nodes,
    Edges: [],
  };
}

function createTimestamp() {
  return new Date().toISOString().replace(/[:.]/g, '-');
}

app.whenReady().then(() => {
  createWindow();

  ipcMain.handle('dialog:openPptx', async () => {
    const result = await dialog.showOpenDialog({
      properties: ['openFile'],
      filters: [{ name: 'PowerPoint', extensions: ['pptx'] }],
    });
    return result.canceled ? null : result.filePaths[0];
  });

  ipcMain.handle('parser:parsePptx', async (_event, filePath) => {
    return runPythonParser(filePath);
  });

  ipcMain.handle('process-upload', async (event, filePath) => {
    if (!filePath) {
      throw new Error('No file path provided');
    }

    const dataDir = ensureDataDir();
    event.sender.send('upload:status', {
      message: 'Parsing PPTX...',
      tone: 'neutral',
      state: 'busy',
    });

    try {
      const units = await runPythonParser(filePath);
      const timestamp = createTimestamp();
      const unitsPath = path.join(dataDir, 'knowledge-units.json');
      const graphPath = path.join(dataDir, `knowledge-graph-${timestamp}.json`);
      const graphPayload = buildGraphFromUnits(units);

      writeJson(unitsPath, {
        updatedAt: new Date().toISOString(),
        sourceFile: filePath,
        units: units,
      });
      writeJson(graphPath, graphPayload);

      event.sender.send('upload:status', {
        message: 'Knowledge graph updated',
        tone: 'success',
        state: 'busy',
        fileName: path.basename(filePath),
      });

      const summary = await runPythonGraphSummary(dataDir);

      event.sender.send('upload:status', {
        message: 'Upload complete',
        tone: 'success',
        state: 'idle',
      });

      return {
        unitCount: Array.isArray(units) ? units.length : 0,
        summary,
        unitsPath,
        graphPath,
      };
    } catch (error) {
      event.sender.send('upload:status', {
        message: error.message || 'Upload failed',
        tone: 'error',
        state: 'idle',
      });
      throw error;
    }
  });

  ipcMain.handle('backend:sync', async () => {
    const dataDir = ensureDataDir();
    return runPythonGraphSummary(dataDir);
  });

  app.on('activate', () => {
    if (BrowserWindow.getAllWindows().length === 0) {
      createWindow();
    }
  });
});

app.on('window-all-closed', () => {
  if (process.platform !== 'darwin') {
    app.quit();
  }
});
