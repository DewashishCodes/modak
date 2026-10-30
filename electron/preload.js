const { contextBridge, ipcRenderer } = require('electron')

contextBridge.exposeInMainWorld('peninsula', {
  setHover: (over) => ipcRenderer.send('pill-hover', over),
})
