const { app, BrowserWindow, ipcMain, screen } = require('electron')
const path = require('node:path')

// Big enough for the fully expanded pill; the window never resizes, only the pill inside animates.
const WIN_W = 520
const WIN_H = 360

let win

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

// Renderer tells us when the cursor is over the pill; everywhere else clicks go to the apps underneath.
ipcMain.on('pill-hover', (_e, over) => win?.setIgnoreMouseEvents(!over, { forward: true }))

app.whenReady().then(createWindow)
app.on('window-all-closed', () => app.quit())
