const { app, BrowserWindow, ipcMain, screen, Tray, Menu, nativeImage, dialog, globalShortcut, protocol, net, shell } = require('electron')
const { pathToFileURL } = require('node:url')
const fs = require('node:fs')
const path = require('node:path')
const { createServer, HOOK_PATH } = require('./server')
const hooks = require('./hooks')
const { systemStats } = require('./widgets/system')
const { gitInfo } = require('./widgets/git')
const { createUsage } = require('./widgets/usage')
const { listPhotos, resolvePhoto } = require('./widgets/photos')

// Photos are served through our own scheme so the renderer never gets general file:// access.
protocol.registerSchemesAsPrivileged([{ scheme: 'modak-photo', privileges: { standard: true, secure: true } }])

// Big enough for the fully expanded pill; the window never resizes, only the pill inside animates.
const WIN_W = 1040
const WIN_H = 480
// shortcut: fixed port, make it configurable if anyone hits a clash
const PORT = 47821
const HOOK_URL = `http://127.0.0.1:${PORT}${HOOK_PATH}`

let win, tray
const send = (channel, data) => win?.webContents.send(channel, data)
const server = createServer({ onChange: (s) => { send('state', s); approvalKeys(s.approvals[0]) }, onSignal: (s) => send('signal', s) })

// Global keys exist only while an approval waits, so they never steal these combos the rest of the time.
// The window itself never takes focus: Enter typed into a terminal must not approve anything.
const KEYS = { 'CommandOrControl+Alt+Y': 'allow', 'CommandOrControl+Alt+N': 'deny' }
let keysFor = null
function approvalKeys(approval) {
  const id = approval?.id || null
  if (id === keysFor) return
  for (const k of Object.keys(KEYS)) globalShortcut.unregister(k)
  keysFor = id
  if (!id) return
  for (const [k, behavior] of Object.entries(KEYS)) {
    globalShortcut.register(k, () => { send('decided-by-key', behavior); server.decide(id, behavior) })
  }
}

const prefsFile = () => path.join(app.getPath('userData'), 'prefs.json')
const prefs = () => { try { return JSON.parse(fs.readFileSync(prefsFile(), 'utf8')) } catch { return {} } }
const savePrefs = (p) => fs.writeFileSync(prefsFile(), JSON.stringify({ ...prefs(), ...p }))

function createWindow() {
  const { bounds } = screen.getPrimaryDisplay()
  win = new BrowserWindow({
    width: WIN_W,
    height: WIN_H,
    x: Math.round(bounds.x + (bounds.width - WIN_W) / 2),
    y: bounds.y, // screen top edge, not workArea, so the pill joins the edge even with a top taskbar
    transparent: true,
    backgroundColor: '#00000000',
    frame: false,
    resizable: false,
    movable: false,
    hasShadow: false,
    skipTaskbar: true,
    focusable: false,
    alwaysOnTop: true,
    show: false,
    webPreferences: { preload: path.join(__dirname, 'preload.js') },
  })
  win.setAlwaysOnTop(true, 'screen-saver') // above fullscreen apps too
  win.setIgnoreMouseEvents(true, { forward: true })

  if (process.env.VITE_DEV_URL) win.loadURL(process.env.VITE_DEV_URL)
  else win.loadFile(path.join(__dirname, '..', 'dist', 'index.html'))

  win.once('ready-to-show', () => win.showInactive())
}

// 16x16 soft pink dot drawn in code, so the tray needs no image file.
function trayIcon() {
  const size = 16, buf = Buffer.alloc(size * size * 4)
  for (let y = 0; y < size; y++) for (let x = 0; x < size; x++) {
    const d = Math.hypot(x - 7.5, y - 8), a = Math.max(0, Math.min(1, 7 - d))
    buf.set([0xe4, 0xd9, 0xf4, Math.round(a * 255)], (y * size + x) * 4) // BGRA
  }
  return nativeImage.createFromBitmap(buf, { width: size, height: size })
}

function connect() {
  try {
    hooks.install(HOOK_URL)
    savePrefs({ declined: false })
  } catch (err) {
    dialog.showErrorBox('Modak', `Could not update ${hooks.SETTINGS}.\nIt may not be valid JSON, so it was left untouched.\n\n${err.message}`)
  }
  refreshTray()
}

function disconnect() {
  try { hooks.uninstall() } catch (err) { dialog.showErrorBox('Modak', err.message) }
  savePrefs({ declined: true })
  refreshTray()
}

function refreshTray(status = 'Watching Claude Code') {
  const connected = hooks.isInstalled()
  tray.setToolTip(`Modak: ${connected ? status : 'not connected'}`)
  tray.setContextMenu(Menu.buildFromTemplate([
    { label: 'Connected to Claude Code', type: 'checkbox', checked: connected, click: () => (connected ? disconnect() : connect()) },
    { label: 'Start with Windows', type: 'checkbox', checked: app.getLoginItemSettings().openAtLogin, click: (i) => app.setLoginItemSettings({ openAtLogin: i.checked }) },
    { type: 'separator' },
    { label: 'Quit Modak', click: () => app.quit() },
  ]))
}

async function askToConnect() {
  if (hooks.isStale(HOOK_URL)) return connect() // hooks from the Peninsula days: swap to the current URL silently
  if (hooks.isInstalled() || prefs().declined) return
  const { response } = await dialog.showMessageBox({
    type: 'question',
    buttons: ['Connect', 'Not now'],
    defaultId: 0,
    cancelId: 1,
    title: 'Modak',
    message: 'Connect Modak to Claude Code?',
    detail: `This adds Modak's hooks to ${hooks.SETTINGS} (a backup is kept next to it). Your other settings and hooks stay as they are. You can disconnect any time from the tray icon.`,
  })
  if (response === 0) connect()
  else savePrefs({ declined: true })
}

// Renderer tells us when the cursor is over the pill; everywhere else clicks go to the apps underneath.
ipcMain.on('pill-hover', (_e, over) => win?.setIgnoreMouseEvents(!over, { forward: true }))
ipcMain.on('decide', (_e, id, behavior) => server.decide(id, behavior))
ipcMain.handle('get-state', () => server.state())

// Dashboard widgets: polled by the renderer only while the dashboard is open.
const usageToday = createUsage()
ipcMain.handle('widgets', async (_e, cwd) => ({ sys: systemStats(), git: await gitInfo(cwd), usage: usageToday() }))

const photosDir = () => prefs().photosDir || path.join(app.getPath('pictures'), 'Modak')
ipcMain.handle('photos', () => listPhotos(photosDir()).map((n) => `modak-photo://img/${encodeURIComponent(n)}`))
ipcMain.on('photos-open', () => { fs.mkdirSync(photosDir(), { recursive: true }); shell.openPath(photosDir()) })

const settings = () => ({ photosDir: photosDir(), connected: hooks.isInstalled(), autostart: app.getLoginItemSettings().openAtLogin, sound: prefs().sound !== false })
ipcMain.handle('get-settings', settings)
ipcMain.handle('set-setting', (_e, key, value) => {
  if (key === 'connected') value ? connect() : disconnect()
  if (key === 'autostart') app.setLoginItemSettings({ openAtLogin: !!value })
  if (key === 'sound') savePrefs({ sound: !!value })
  if (key === 'photosDir') {
    const picked = dialog.showOpenDialogSync({ title: 'Folder with your favourite photos', defaultPath: photosDir(), properties: ['openDirectory'] })
    if (picked?.[0]) savePrefs({ photosDir: picked[0] })
  }
  refreshTray()
  return settings()
})

if (!app.requestSingleInstanceLock()) app.quit()
else {
  app.whenReady().then(async () => {
    fs.mkdirSync(photosDir(), { recursive: true })
    protocol.handle('modak-photo', (req) => {
      const file = resolvePhoto(photosDir(), req.url)
      return file ? net.fetch(pathToFileURL(file).toString()) : new Response('not found', { status: 404 })
    })
    createWindow()
    tray = new Tray(trayIcon())
    refreshTray()
    try {
      await server.listen(PORT)
    } catch (err) {
      refreshTray(`port ${PORT} is busy`)
      dialog.showErrorBox('Modak', `Port ${PORT} is already in use, so Modak cannot hear Claude Code.\nIs another copy running?\n\n${err.message}`)
    }
    askToConnect()
  })
  app.on('before-quit', () => { globalShortcut.unregisterAll(); server.close() }) // open approvals go back to the terminal prompt
  app.on('window-all-closed', () => app.quit())
}
