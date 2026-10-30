// Renders Modak's moods to sprite sheets for the native app: `node_modules/electron/dist/electron.exe scripts/modak-sprites.js`
// Loads the approved design (src/modak.generated.js) in an offscreen window, freezes every CSS animation at each
// frame time, captures it and packs the frames into one PNG per mood and size in native/App/Assets/modak.
const { app, BrowserWindow, nativeImage } = require('electron')
const fs = require('node:fs')
const path = require('node:path')

const FPS = 20
const SECONDS = 4 // one blink cycle; every mood loops cleanly enough over it
const FRAMES = FPS * SECONDS
const COLS = 10
const SIZES = { small: 60, full: 104 } // 2x the notch (30) and tile (52) sizes, so they stay crisp on scaled displays
const OUT = path.join(__dirname, '..', 'native', 'App', 'Assets', 'modak')

app.disableHardwareAcceleration() // offscreen capture is deterministic in software
app.whenReady().then(async () => {
  const src = fs.readFileSync(path.join(__dirname, '..', 'src', 'modak.generated.js'), 'utf8').replace(/^export const /gm, 'window.')
  const win = new BrowserWindow({ width: 200, height: 200, show: false, transparent: true, backgroundColor: '#00000000', webPreferences: { offscreen: true } })
  await win.loadURL('data:text/html,<html><body style="margin:0;background:transparent"><div id="m"></div></body></html>')
  await win.webContents.executeJavaScript(`${src};
    document.body.insertAdjacentHTML('afterbegin', MODAK_DEFS);
    const style = document.createElement('style'); style.textContent = MODAK_CSS + ' *{animation-play-state:paused!important}'; document.head.append(style);
    true`)
  fs.mkdirSync(OUT, { recursive: true })
  const moods = await win.webContents.executeJavaScript('Object.keys(MODAK)')
  for (const mood of moods) {
    for (const [lod, px] of Object.entries(SIZES)) {
      await win.webContents.executeJavaScript(`document.getElementById('m').innerHTML = MODAK[${JSON.stringify(mood)}].${lod}.replaceAll('__S__', '${px}'); true`)
      await new Promise((r) => setTimeout(r, 400)) // offscreen paints after a markup swap are not tied to animation frames
      const rows = Math.ceil(FRAMES / COLS)
      const sheet = Buffer.alloc(COLS * px * rows * px * 4)
      for (let f = -1; f < FRAMES; f++) { // f = -1: a warm-up frame, so the new markup has surely painted before frame 0
        await win.webContents.executeJavaScript(`document.getAnimations().forEach((a) => (a.currentTime = ${(Math.max(0, f) * 1000) / FPS})); new Promise((r) => requestAnimationFrame(() => requestAnimationFrame(r)))`)
        await win.webContents.capturePage({ x: 0, y: 0, width: px, height: px }) // throwaway: offscreen capture can lag a paint behind
        const frame = (await win.webContents.capturePage({ x: 0, y: 0, width: px, height: px })).resize({ width: px, height: px })
        if (f < 0) continue
        const bits = frame.toBitmap() // BGRA
        const ox = (f % COLS) * px, oy = Math.floor(f / COLS) * px
        for (let y = 0; y < px; y++) bits.copy(sheet, ((oy + y) * COLS * px + ox) * 4, y * px * 4, (y + 1) * px * 4)
      }
      fs.writeFileSync(path.join(OUT, `${mood}-${lod}.png`), nativeImage.createFromBitmap(sheet, { width: COLS * px, height: rows * px }).toPNG())
      console.log(`${mood}-${lod}.png`)
    }
  }
  app.quit()
})
