import { useEffect, useRef, useState } from 'react'
import { Play, Pause, Next, Prev, Branch, Chip, Spark, Timer, Music } from './icons.jsx'
import { chime } from './chime.js'

const api = window.peninsula
const fmtTokens = (n) => (n >= 1e6 ? (n / 1e6).toFixed(1) + 'M' : n >= 1e3 ? Math.round(n / 1e3) + 'K' : String(n))

// "Spotify.exe" or "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify" -> "Spotify"
const appName = (id = '') => {
  const s = id.split('!').pop().replace(/\.exe$/i, '').split('.').pop().replace(/_.*/, '')
  return s.toLowerCase() === 'msedge' ? 'Edge' : s ? s[0].toUpperCase() + s.slice(1) : 'Media'
}

export function NowPlaying() {
  const [m, setM] = useState(null)
  useEffect(() => api?.onMedia(setM), [])
  const has = m?.title
  return (
    <div className="tile small">
      <div className="tile-label"><Music size={13} />{has ? appName(m.app) : 'Now playing'}</div>
      <div className="tile-main clip" title={has ? `${m.title} · ${m.artist}` : ''}>{has ? m.title : 'Nothing playing'}</div>
      <div className="media-row">
        <span className="tile-sub clip">{has ? m.artist : ''}</span>
        {has && (
          <span className="media-btns">
            <button onClick={() => api.media('prev')} aria-label="Previous"><Prev size={12} /></button>
            <button onClick={() => api.media('toggle')} aria-label={m.playing ? 'Pause' : 'Play'}>{m.playing ? <Pause size={12} /> : <Play size={12} />}</button>
            <button onClick={() => api.media('next')} aria-label="Next"><Next size={12} /></button>
          </span>
        )}
      </div>
    </div>
  )
}

export function Clock() {
  const [now, setNow] = useState(new Date())
  useEffect(() => { const t = setInterval(() => setNow(new Date()), 1000); return () => clearInterval(t) }, [])
  return (
    <div className="tile small">
      <div className="tile-big">{now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit', hour12: false })}</div>
      <div className="tile-sub">{now.toLocaleDateString([], { weekday: 'short', day: 'numeric', month: 'short' })}</div>
    </div>
  )
}

const FOCUS_MIN = 25

// Click to start or pause, double click to reset. Chimes when it runs out.
export function Focus({ sound }) {
  const [left, setLeft] = useState(FOCUS_MIN * 60)
  const [running, setRunning] = useState(false)
  const end = useRef(0)
  useEffect(() => {
    if (!running) return
    const t = setInterval(() => {
      const s = Math.max(0, Math.round((end.current - Date.now()) / 1000))
      setLeft(s)
      if (!s) { setRunning(false); if (sound) chime() }
    }, 250)
    return () => clearInterval(t)
  }, [running, sound])
  const toggle = () => {
    if (!left) setLeft(FOCUS_MIN * 60)
    end.current = Date.now() + (left || FOCUS_MIN * 60) * 1000
    setRunning((r) => !r)
  }
  const reset = () => { setRunning(false); setLeft(FOCUS_MIN * 60) }
  const mm = String(Math.floor(left / 60)).padStart(2, '0'), ss = String(left % 60).padStart(2, '0')
  return (
    <button className={`tile small focus ${running ? 'on' : ''}`} onClick={toggle} onDoubleClick={reset} title="Click to start or pause, double click to reset">
      <div className="tile-label"><Timer size={13} />Focus</div>
      <div className="tile-big">{mm}:{ss}</div>
      <div className="tile-sub">{running ? 'Stay with it' : left === FOCUS_MIN * 60 ? 'Click to start' : 'Paused'}</div>
    </button>
  )
}

export function Git({ git }) {
  return (
    <div className="tile small">
      <div className="tile-label"><Branch size={13} />Git</div>
      {git ? (
        <>
          <div className="tile-main clip">{git.branch}</div>
          <div className="tile-sub clip" title={git.subject}>{git.changed ? `${git.changed} changed` : 'Clean'} · {git.when || 'no commits'}</div>
        </>
      ) : (
        <><div className="tile-main">Not a repo</div><div className="tile-sub">for this session</div></>
      )}
    </div>
  )
}

export function Usage({ usage }) {
  return (
    <div className="tile small">
      <div className="tile-label"><Spark size={13} />Today</div>
      <div className="tile-big">{usage ? fmtTokens(usage.output) : '·'}<span className="unit"> out</span></div>
      <div className="tile-sub">{usage ? `${fmtTokens(usage.total)} total · ${usage.messages} replies` : 'Reading usage…'}</div>
    </div>
  )
}

const Bar = ({ value }) => <div className="bar"><div style={{ transform: `scaleX(${(value || 0) / 100})` }} /></div>

export function System({ sys }) {
  return (
    <div className="tile small">
      <div className="tile-label"><Chip size={13} />System</div>
      <div className="sys-row"><span>CPU</span><Bar value={sys?.cpu} /><b>{sys ? sys.cpu + '%' : '·'}</b></div>
      <div className="sys-row"><span>RAM</span><Bar value={sys?.ram} /><b>{sys ? sys.ramUsedGb + 'G' : '·'}</b></div>
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
      <div className="setting-tip">Tip: hold <kbd>Shift</kbd> while hovering Peninsula to click whatever is behind it.</div>
    </div>
  )
}
