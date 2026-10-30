const { spawn, execFile } = require('node:child_process')
const path = require('node:path')

// In a packaged app the script sits outside the asar archive (see build.asarUnpack).
const SCRIPT = path.join(__dirname, 'media.ps1').replace('app.asar', 'app.asar.unpacked')
const ps = (mode) => ['-NoProfile', '-NonInteractive', '-ExecutionPolicy', 'Bypass', '-File', SCRIPT, mode]

// Streams what's playing to onChange; returns a stop function.
function watchMedia(onChange) {
  let child, stopped = false, buf = ''
  const start = () => {
    child = spawn('powershell.exe', ps('watch'), { windowsHide: true })
    child.stdout.on('data', (d) => {
      buf += d
      let i
      while ((i = buf.indexOf('\n')) >= 0) {
        const line = buf.slice(0, i).trim()
        buf = buf.slice(i + 1)
        try { if (line) onChange(JSON.parse(line)) } catch {}
      }
    })
    child.on('exit', () => { if (!stopped) setTimeout(start, 5000) }) // restart if PowerShell dies
  }
  start()
  return () => { stopped = true; child?.kill() }
}

// action: toggle | next | prev
const mediaControl = (action) => execFile('powershell.exe', ps(action), { windowsHide: true }, () => {})

module.exports = { watchMedia, mediaControl }
