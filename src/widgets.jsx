import { useEffect, useState } from 'react'
import { Play, Pause, Next, Prev, Branch, Chip, Spark, Music } from './icons.jsx'
import { mmss } from './focus.js'

const api = window.peninsula
const fmtTokens = (n) => (n >= 1e6 ? (n / 1e6).toFixed(1) + 'M' : n >= 1e3 ? Math.round(n / 1e3) + 'K' : String(n))

// "Spotify.exe" or "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" -> "Spotify"
const appName = (id = '') => {
  const s = id.split('!').pop().replace(/\.exe$/i, '').split('.').pop().replace(/_.*/, '')
  return s.toLowerCase() === 'msedge' ? 'Edge' : s ? s[0].toUpperCase() + s.slice(1) : 'Media'
}

// Colored rounded square holding a white icon, like an app icon.
export const IconSquare = ({ color, children }) => <span className="isq" style={{ background: color }}>{children}</span>

export function Ring({ value = 0, size = 56, stroke = 6, color = '#8fb4ff', track = '#26262c', children }) {
  const r = (size - stroke) / 2
  const c = 2 * Math.PI * r
  return (
    <div className="ring" style={{ width: size, height: size }}>
      <svg width={size} height={size}>
        <circle cx={size / 2} cy={size / 2} r={r} stroke={track} strokeWidth={stroke} fill="none" />
        <circle
          cx={size / 2} cy={size / 2} r={r} stroke={color} strokeWidth={stroke} fill="none" strokeLinecap="round"
          strokeDasharray={c} strokeDashoffset={c * (1 - Math.min(1, Math.max(0, value)))}
          style={{ transition: 'stroke-dashoffset 0.6s ease', transform: 'rotate(-90deg)', transformOrigin: 'center' }}
        />
      </svg>
      <div className="ring-inner">{children}</div>
    </div>
  )
}

const Equalizer = ({ on }) => (
  <span className={`eq ${on ? 'on' : ''}`} aria-hidden="true"><i /><i /><i /><i /></span>
)

export function NowPlaying() {
  const [m, setM] = useState(null)
  useEffect(() => api?.onMedia(setM), [])
  const has = m?.title
  return (
    <div className="tile small media">
      {m?.art && <div className="media-bg" style={{ backgroundImage: `url(${m.art})` }} />}
      <div className="media-top">
        {m?.art ? <img className="art" src={m.art} alt="" /> : <IconSquare color="#1db954"><Music size={15} /></IconSquare>}
        <div className="media-text">
          <div className="tile-main clip" title={has ? `${m.title} · ${m.artist}` : ''}>{has ? m.title : 'Nothing playing'}</div>
          <div className="tile-sub clip">{has ? m.artist || appName(m.app) : 'Play music in any app'}</div>
        </div>
      </div>
      {has && (
        <div className="media-bottom">
          <Equalizer on={m.playing} />
          <span className="media-btns">
            <button onClick={() => api.media('prev')} aria-label="Previous"><Prev size={13} /></button>
            <button className="main" onClick={() => api.media('toggle')} aria-label={m.playing ? 'Pause' : 'Play'}>{m.playing ? <Pause size={13} /> : <Play size={13} />}</button>
            <button onClick={() => api.media('next')} aria-label="Next"><Next size={13} /></button>
          </span>
        </div>
      )}
    </div>
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
    <div className="tile small row-tile">
      <AnalogClock now={now} />
      <div>
        <div className="tile-big">{now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}</div>
        <div className="tile-sub">{now.toLocaleDateString([], { weekday: 'short', day: 'numeric', month: 'short' })}</div>
      </div>
    </div>
  )
}

// Click to start or pause, double click to reset. State comes from useFocus in App.
export function Focus({ focus }) {
  const { left, running, toggle, reset, progress } = focus
  return (
    <button className={`tile small row-tile focus ${running ? 'on' : ''}`} onClick={toggle} onDoubleClick={reset} title="Click to start or pause, double click to reset">
      <Ring value={progress} size={54} stroke={5} color="#ff9f43">
        {running ? <Pause size={14} /> : <Play size={14} />}
      </Ring>
      <div>
        <div className="tile-big">{mmss(left)}</div>
        <div className="tile-sub">{running ? 'Focusing' : progress > 0 ? 'Paused' : 'Focus · 25 min'}</div>
      </div>
    </button>
  )
}

export function Git({ git }) {
  return (
    <div className="tile small">
      <div className="tile-head">
        <IconSquare color="#8b5cf6"><Branch size={15} /></IconSquare>
        <div className="clip">
          <div className="tile-main clip">{git ? git.branch : 'Not a repo'}</div>
          <div className="tile-sub clip" title={git?.subject}>{git ? git.when || 'no commits yet' : 'for this session'}</div>
        </div>
      </div>
      {git && (
        <div className="chips">
          {git.changed === 0 && <span className="chip clean">✓ clean</span>}
          {git.added > 0 && <span className="chip add">+{git.added}</span>}
          {git.modified > 0 && <span className="chip mod">~{git.modified}</span>}
          {git.deleted > 0 && <span className="chip del">−{git.deleted}</span>}
        </div>
      )}
    </div>
  )
}

export function Usage({ usage }) {
  const hours = usage?.hours || new Array(24).fill(0)
  const max = Math.max(1, ...hours)
  const nowH = new Date().getHours()
  return (
    <div className="tile small">
      <div className="tile-head">
        <IconSquare color="#ec4899"><Spark size={15} /></IconSquare>
        <div className="clip">
          <div className="tile-main">{usage ? fmtTokens(usage.output) : '·'} <span className="unit">out today</span></div>
          <div className="tile-sub">{usage ? `${usage.messages} replies` : 'Reading usage…'}</div>
        </div>
      </div>
      <div className="spark" aria-label="Output tokens per hour today">
        {hours.map((v, h) => (
          <i key={h} className={h === nowH ? 'now' : h > nowH ? 'future' : ''} style={{ height: `${Math.max(8, (100 * v) / max)}%` }} />
        ))}
      </div>
    </div>
  )
}

export function System({ sys }) {
  return (
    <div className="tile small system">
      <div className="gauge">
        <Ring value={(sys?.cpu || 0) / 100} size={52} stroke={5} color="#38bdf8"><b>{sys ? sys.cpu : '·'}</b><small>%</small></Ring>
        <span><Chip size={11} /> CPU</span>
      </div>
      <div className="gauge">
        <Ring value={(sys?.ram || 0) / 100} size={52} stroke={5} color="#a78bfa"><b>{sys ? sys.ramUsedGb : '·'}</b><small>G</small></Ring>
        <span>RAM</span>
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
      <Toggle label="Connected to Claude Code" hint="Peninsula's hooks in ~/.claude/settings.json" on={settings.connected} onChange={(v) => onChange('connected', v)} />
      <Toggle label="Sound" hint="Chime when work is done" on={settings.sound} onChange={(v) => onChange('sound', v)} />
      <Toggle label="Start with Windows" hint="Open Peninsula when you sign in" on={settings.autostart} onChange={(v) => onChange('autostart', v)} />
      <div className="setting-tip">Tips: hold <kbd>Shift</kbd> over Peninsula to click what is behind it. While an approval waits, <kbd>Ctrl+Alt+Y</kbd> allows and <kbd>Ctrl+Alt+N</kbd> denies.</div>
    </div>
  )
}
