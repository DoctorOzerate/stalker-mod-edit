const { contextBridge, ipcRenderer } = require('electron');
contextBridge.exposeInMainWorld('xrayForge', {
  saveProjectFile: function (payload) { return ipcRenderer.invoke('xray:save-project', payload); }
});
