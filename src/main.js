const { app, BrowserWindow, dialog, ipcMain } = require('electron');
const { spawn } = require('child_process');
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

function runPythonGraphSummary() {
  return new Promise((resolve, reject) => {
    const args = ['-m', 'backend.session', '--summary'];
    if (process.env.CAT_DATA_DIR) {
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

  ipcMain.handle('backend:sync', async () => {
    return runPythonGraphSummary();
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
