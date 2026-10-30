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
  onDecidedByKey: listen('decided-by-key'),
  decide: (id, behavior) => ipcRenderer.send('decide', id, behavior),
  widgets: (cwd) => ipcRenderer.invoke('widgets', cwd),
  onMedia: listen('media'),
  media: (action) => ipcRenderer.send('media', action),
  getSettings: () => ipcRenderer.invoke('get-settings'),
  setSetting: (key, value) => ipcRenderer.invoke('set-setting', key, value),
})
