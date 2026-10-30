import { useEffect, useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import { Play, Pause, Branch, Chip, Spark, Image, Reset } from './icons.jsx'
import { mmss } from './focus.js'

const api = window.modak
const fmtTokens = (n) => (n >= 1e6 ? (n / 1e6).toFixed(1) + 'M' : n >= 1e3 ? Math.round(n / 1e3) + 'K' : String(n))

// Colored rounded square holding a white icon, like an app icon.
export const IconSquare = ({ color, children }) => <span className="isq" style={{ background: color }}>{children}</span>

export function Ring({ value = 0, size = 56, stroke = 6, color = '#8fb4ff', track = '#26262c', children }) {
  const r = (size - stroke) / 2
  const c = 2 * Math.PI * r
  return (
    <div className="ring" style={{ width: size, height: size }}>
      <svg width={size} height={size}>
        <circle cx={size / 2} cy={size / 2} r={r} stroke={track} strokeWidth={stroke} fill="none" />
        {value > 0 && <circle
          cx={size / 2} cy={size / 2} r={r} stroke={color} strokeWidth={stroke} fill="none" strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={c * (1 - Math.min(1, Math.max(0, value)))}
          style={{ transition: 'stroke-dashoffset 0.6s ease', transform: 'rotate(-90deg)', transformOrigin: 'center' }}
        />}
      </svg>
      <div className="ring-inner">{children}</div>
    </div>
  )
}

const SLIDE_MS = 6000
let slideIndex = 0 // survives the dashboard closing, so the show continues where it was

// Your favourite photos, crossfading with a slow zoom. Click for the next one.
export function Photos() {
  const [photos, setPhotos] = useState(null)
  const [i, setI] = useState(slideIndex)
  useEffect(() => { api?.photos().then(setPhotos) }, [])
  useEffect(() => {
    if (!photos?.length) return
    const t = setInterval(() => setI((n) => (slideIndex = (n + 1) % photos.length)), SLIDE_MS)
    return () => clearInterval(t)
  }, [photos])

  if (photos && !photos.length) {
    return (
      <button className="tile photos empty" onClick={() => api?.openPhotos()}>
        <IconSquare color="#f59e0b"><Image size={15} /></IconSquare>
        <b>Add your favourite photos</b>
        <span className="tile-sub">Click, then drop images in the folder that opens</span>
      </button>
    )
  }
  const n = photos?.length || 0
  const cur = n ? photos[i % n] : null
  return (
    <button className="tile photos" onClick={() => n && setI((x) => (slideIndex = (x + 1) % n))} aria-label="Next photo">
      <AnimatePresence initial={false}>
        {cur && (
          <motion.img key={cur} src={cur} alt="" className="slide" initial={{ opacity: 0 }} animate={{ opacity: 1 }} exit={{ opacity: 0 }} transition={{ duration: 0.9 }} />
        )}
      </AnimatePresence>
      {n > 1 && (
        <span className="slide-dots">
          {n <= 8 ? photos.map((p, k) => <i key={p} className={k === i % n ? 'on' : ''} />) : <em>{(i % n) + 1} / {n}</em>}
        </span>
      )}
    </button>
  )
}

function AnalogClock({ now }) {
  const hand = (deg, len, w, color) => {
    const a = (deg * Math.PI) / 180
    return <line x1="23" y1="23" x2={23 + len * Math.sin(a)} y2={23 - len * Math.cos(a)} stroke={color} strokeWidth={w} strokeLinecap="round" />
  }
  return (
    <svg width="50" height="50" viewBox="0 0 46 46" className="analog" aria-hidden="true">
      <circle cx="23" cy="23" r="21" fill="#1c1c21" stroke="#2c2c33" />
      {[...Array(12)].map((_, i) => {
        const a = (i * 30 * Math.PI) / 180
        return <circle key={i} cx={23 + 17 * Math.sin(a)} cy={23 - 17 * Math.cos(a)} r={i % 3 ? 0.7 : 1.3} fill="#6b6b75" />
      })}
      {hand((now.getHours() % 12) * 30 + now.getMinutes() * 0.5, 10, 2.4, '#f2f2f3')}
      {hand(now.getMinutes() * 6, 15, 1.8, '#f2f2f3')}
      {hand(now.getSeconds() * 6, 16, 1, '#ff9f43')}
      <circle cx="23" cy="23" r="1.8" fill="#ff9f43" />
    </svg>
  )
}

export function Clock() {
  const [now, setNow] = useState(new Date())
  useEffect(() => { const t = setInterval(() => setNow(new Date()), 1000); return () => clearInterval(t) }, [])
  return (
    <div className="tile small row-tile clock">
      <AnalogClock now={now} />
      <div>
        <div className="tile-big">{now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}</div>
        <div className="tile-sub">{now.toLocaleDateString([], { weekday: 'short', day: 'numeric', month: 'short' })}</div>
      </div>
    </div>
  )
}

// The ring starts and pauses; the reset button goes back to 25:00. State comes from useFocus in App.
export function Focus({ focus }) {
  const { left, running, toggle, reset, progress } = focus
  return (
    <div className={`tile small row-tile focus ${running ? 'on' : ''}`}>
      <button className="ring-btn" onClick={toggle} aria-label={running ? 'Pause focus timer' : 'Start focus timer'}>
        <Ring value={progress} size={54} stroke={5} color="#ff9f43">
          {running ? <Pause size={14} /> : <Play size={14} />}
        </Ring>
      </button>
      <div className="focus-text">
        <div className="tile-big">{mmss(left)}</div>
        <div className="tile-sub">{running ? 'Focusing' : progress > 0 ? 'Paused' : 'Focus · 25 min'}</div>
      </div>
      {progress > 0 && (
        <button className="icon-btn reset" onClick={reset} title="Reset to 25:00" aria-label="Reset focus timer"><Reset size={14} /></button>
      )}
    </div>
  )
}

const MIX = [
  ['output', 'Output', '#ec4899'],
  ['input', 'Input', '#f59e0b'],
  ['cacheWrite', 'Cache write', '#8b5cf6'],
  ['cacheRead', 'Cache read', '#38bdf8'],
]

export function Usage({ usage }) {
  const hours = usage?.hours || new Array(24).fill(0)
  const max = Math.max(1, ...hours)
  const nowH = new Date().getHours()
  const total = usage?.total || 1
  return (
    <div className="tile wide usage">
      <div className="usage-left">
        <div className="tile-head">
          <IconSquare color="#ec4899"><Spark size={15} /></IconSquare>
          <div>
            <div className="tile-big">{usage ? fmtTokens(usage.output) : '·'}<span className="unit"> out today</span></div>
            <div className="tile-sub">{usage ? `${usage.messages} replies · ${fmtTokens(usage.total)} processed` : 'Reading usage…'}</div>
          </div>
        </div>
        <div className="mix-bar" aria-hidden="true">
          {MIX.map(([k, , c]) => <i key={k} style={{ flexGrow: usage ? Math.max(usage[k], total * 0.015) : 1, background: c }} />)}
        </div>
        <div className="mix-legend">
          {MIX.map(([k, label, c]) => (
            <span key={k}><i style={{ background: c }} />{label} <b>{usage ? fmtTokens(usage[k]) : '·'}</b></span>
          ))}
        </div>
      </div>
      <div className="chart">
        <div className="spark" aria-label="Output tokens per hour today">
          {hours.map((v, h) => (
            <i key={h} title={`${h}:00 · ${fmtTokens(v)}`} className={h === nowH ? 'now' : h > nowH ? 'future' : ''} style={{ height: `${Math.max(5, (100 * v) / max)}%` }} />
          ))}
        </div>
        <div className="axis"><span>0h</span><span>6h</span><span>12h</span><span>18h</span><span>24h</span></div>
      </div>
    </div>
  )
}

export function Git({ git }) {
  if (!git) {
    return (
      <div className="tile wide git">
        <div className="tile-head">
          <IconSquare color="#8b5cf6"><Branch size={15} /></IconSquare>
          <div><div className="tile-main">Not a git repo</div><div className="tile-sub">This session's folder has no repository</div></div>
        </div>
      </div>
    )
  }
  return (
    <div className="tile wide git">
      <div className="tile-head">
        <IconSquare color="#8b5cf6"><Branch size={15} /></IconSquare>
        <div className="clip">
          <div className="tile-main clip">{git.branch}</div>
          <div className="git-badges">
            {git.ahead > 0 && <span className="badge up">↑ {git.ahead}</span>}
            {git.behind > 0 && <span className="badge down">↓ {git.behind}</span>}
            {git.ahead === 0 && git.behind === 0 && <span className="badge">in sync</span>}
            {git.ahead === null && <span className="badge">local only</span>}
            {git.changed === 0 && <span className="chip clean">✓ clean</span>}
            {git.added > 0 && <span className="chip add">+{git.added}</span>}
            {git.modified > 0 && <span className="chip mod">~{git.modified}</span>}
            {git.deleted > 0 && <span className="chip del">−{git.deleted}</span>}
          </div>
        </div>
      </div>
      <div className="git-files">
        {git.files.map((f) => (
          <div key={f.path} className="git-file clip"><span className={`fk ${f.kind}`}>{f.kind === 'add' ? 'A' : f.kind === 'del' ? 'D' : 'M'}</span>{f.path}</div>
        ))}
        {git.changed > git.files.length && <div className="git-file more">+{git.changed - git.files.length} more</div>}
      </div>
      <div className="git-commit clip" title={git.subject}><span className="commit-dot" />{git.subject || 'No commits yet'} <span className="muted">· {git.author} · {git.when}</span></div>
    </div>
  )
}

export function System({ sys }) {
  return (
    <div className="tile system">
      <div className="gauge">
        <Ring value={(sys?.cpu || 0) / 100} size={58} stroke={6} color="#38bdf8"><b>{sys ? sys.cpu : '·'}</b><small>%</small></Ring>
        <span><Chip size={11} /> CPU</span>
      </div>
      <div className="gauge">
        <Ring value={(sys?.ram || 0) / 100} size={58} stroke={6} color="#a78bfa"><b>{sys ? sys.ram : '·'}</b><small>%</small></Ring>
        <span>RAM {sys ? `${sys.ramUsedGb} of ${sys.ramTotalGb} GB` : ''}</span>
      </div>
    </div>
  )
}

const Toggle = ({ label, hint, on, onChange }) => (
  <button className="setting" onClick={() => onChange(!on)} role="switch" aria-checked={on}>
    <span><b>{label}</b><small>{hint}</small></span>
    <span className={`switch ${on ? 'on' : ''}`}><span /></span>
  </button>
)

export function Settings({ settings, onChange }) {
  if (!settings) return null
  return (
    <div className="settings">
      <Toggle label="Connected to Claude Code" hint="Modak's hooks in ~/.claude/settings.json" on={settings.connected} onChange={(v) => onChange('connected', v)} />
      <Toggle label="Sound" hint="Chime when work is done" on={settings.sound} onChange={(v) => onChange('sound', v)} />
      <Toggle label="Start with Windows" hint="Open Modak when you sign in" on={settings.autostart} onChange={(v) => onChange('autostart', v)} />
      <div className="setting">
        <span className="clip"><b>Photos folder</b><small className="clip">{settings.photosDir}</small></span>
        <span className="setting-actions">
          <button className="link" onClick={() => api?.openPhotos()}>Open</button>
          <button className="link" onClick={() => onChange('photosDir')}>Change</button>
        </span>
      </div>
      <div className="setting-tip">Tips: hold <kbd>Shift</kbd> over Modak to click what is behind it. While an approval waits, <kbd>Ctrl+Alt+Y</kbd> allows and <kbd>Ctrl+Alt+N</kbd> denies.</div>
    </div>
  )
}
