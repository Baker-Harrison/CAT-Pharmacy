const { contextBridge, ipcRenderer } = require('electron');

contextBridge.exposeInMainWorld('catApi', {
  processUpload: (filePath) => ipcRenderer.invoke('process-upload', filePath),
  onUploadStatus: (callback) =>
    ipcRenderer.on('upload:status', (_event, payload) => {
      if (typeof callback === 'function') {
        callback(payload);
      }
    }),
  getLessons: () => ipcRenderer.invoke('lessons:list'),
  generateLesson: () => ipcRenderer.invoke('lessons:generate'),
  getExams: () => ipcRenderer.invoke('exams:list'),
  generateExam: () => ipcRenderer.invoke('exams:generate'),
});
