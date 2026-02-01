const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('catApi', {
  openPptxDialog: () => ipcRenderer.invoke('dialog:openPptx'),
  parsePptx: (filePath) => ipcRenderer.invoke('parser:parsePptx', filePath),
  syncBackend: () => ipcRenderer.invoke('backend:sync'),
});
