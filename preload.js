const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('catApi', {
  openPptxDialog: () => ipcRenderer.invoke('dialog:openPptx'),
  parsePptx: (filePath) => ipcRenderer.invoke('parser:parsePptx', filePath),
  processUpload: (filePath) => ipcRenderer.invoke('process-upload', filePath),
  onUploadStatus: (callback) =>
    ipcRenderer.on('upload:status', (_event, payload) => {
      if (typeof callback === 'function') {
        callback(payload);
      }
    }),
  syncBackend: () => ipcRenderer.invoke('backend:sync'),
  getLessons: () => ipcRenderer.invoke('lessons:list'),
  startLearning: () => ipcRenderer.invoke('learning:start'),
  processLearningResponse: (payload) => ipcRenderer.invoke('learning:processResponse', payload),
});
