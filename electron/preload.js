const { contextBridge, ipcRenderer } = require('electron')

const listen = (channel) => (cb) => {
  const fn = (_e, data) => cb(data)
  ipcRenderer.on(channel, fn)
  return () => ipcRenderer.removeListener(channel, fn)
}

contextBridge.exposeInMainWorld('peninsula', {
  setHover: (over) => ipcRenderer.send('pill-hover', over),
  getState: () => ipcRenderer.invoke('get-state'),
  onState: listen('state'),
  onSignal: listen('signal'),
  decide: (id, behavior) => ipcRenderer.send('decide', id, behavior),
})
