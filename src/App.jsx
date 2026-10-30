import { useEffect, useRef, useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import Mascot from './Mascot.jsx'
import AgentTile from './AgentTile.jsx'
import Preview from './Preview.jsx'
import { Photos, Clock, Focus, Git, Usage, System, Settings, Ring } from './widgets.jsx'
import { useFocus, mmss } from './focus.js'
import { Gear, Sound, Mute, Check, Back, Terminal } from './icons.jsx'
import { chime } from './chime.js'

const spring = { type: 'spring', stiffness: 420, damping: 32 }
const DONE_FLASH_MS = 5000
const HOVER_OPEN_MS = 140 // passing the mouse over the top edge should not pop the dashboard open
const WIDGET_POLL_MS = 2000
const WIDTH = { notch: 76, notchFocus: 150, bar: 540, dashboard: 980 }
const api = window.modak

function useModak(sound) {
  const [state, setState] = useState({ sessions: [], approvals: [] })
  const [flash, setFlash] = useState(null) // session id that just finished
  const timer = useRef()
  const soundRef = useRef(sound)
  soundRef.current = sound

  useEffect(() => {
    if (!api) return
    api.getState().then(setState)
    const offState = api.onState(setState)
    const offSignal = api.onSignal((s) => {
      if (s.type !== 'done') return
      if (soundRef.current) chime()
      setFlash(s.sessionId)
      clearTimeout(timer.current)
      timer.current = setTimeout(() => setFlash(null), DONE_FLASH_MS)
    })
    return () => { offState(); offSignal(); clearTimeout(timer.current) }
  }, [])

  return { ...state, flash }
}

function useWidgets(open, cwd) {
  const [w, setW] = useState({})
  useEffect(() => {
    if (!open || !api) return
    let alive = true
    const load = () => api.widgets(cwd).then((d) => alive && setW(d))
    load()
    const t = setInterval(load, WIDGET_POLL_MS)
    return () => { alive = false; clearInterval(t) }
  }, [open, cwd])
  return w
}

function ApprovalBar({ approval, more, onDecide }) {
  const [details, setDetails] = useState(false)
  return (
    <>
      <div className="bar-row">
        <span className="dot asking" />
        <button className="ask clip" onClick={() => setDetails((d) => !d)} title="Show details">
          <span className="muted">{approval.folder}:</span> Allow <b>{approval.ask}</b>?
        </button>
        {more > 0 && <span className="more">+{more}</span>}
        <button className="pill-btn allow" onClick={() => onDecide('allow')} title="Allow (Ctrl+Alt+Y)">Allow</button>
        <button className="pill-btn deny" onClick={() => onDecide('deny')} title="Deny (Ctrl+Alt+N)">Deny</button>
        <button className="icon-btn" onClick={() => onDecide('terminal')} title="Answer in terminal" aria-label="Answer in terminal"><Terminal size={14} /></button>
      </div>
      {details && <div className="bar-detail"><Preview preview={approval.preview || { kind: 'answer', text: approval.detail }} /></div>}
    </>
  )
}

export default function App() {
  const [settings, setSettings] = useState(null)
  const { sessions, approvals, flash } = useModak(settings?.sound !== false)
  const focus = useFocus(settings?.sound !== false)
  const [hover, setHover] = useState(false)
  const [ghost, setGhost] = useState(false) // Shift held: fade out and let clicks through
  const [page, setPage] = useState('grid') // grid | settings
  const [index, setIndex] = useState(0)
  const [love, setLove] = useState(0)
  const openTimer = useRef()
  const pillRef = useRef()

  useEffect(() => { api?.getSettings().then(setSettings) }, [])
  useEffect(() => api?.onDecidedByKey?.((b) => b === 'allow' && setLove((n) => n + 1)), [])

  const approval = approvals[0]
  const flashed = flash && sessions.find((s) => s.id === flash)
  const view = ghost ? 'notch' : approval ? 'approval' : hover ? 'dashboard' : flashed ? 'done' : 'notch'

  const i = Math.min(index, Math.max(0, sessions.length - 1))
  const primary = (approval && sessions.find((s) => s.id === approval.sessionId)) || flashed || sessions[i]
  const status = approval ? 'asking' : flashed ? 'done' : primary?.status === 'done' ? 'idle' : primary?.status || 'idle'
  const widgets = useWidgets(view === 'dashboard', sessions[i]?.cwd)

  const enter = (e) => {
    if (e.shiftKey) return
    api?.setHover(true)
    clearTimeout(openTimer.current)
    openTimer.current = setTimeout(() => setHover(true), HOVER_OPEN_MS)
  }
  const leave = () => {
    clearTimeout(openTimer.current)
    api?.setHover(false)
    setHover(false)
    setPage('grid')
  }

  // Mouse moves still arrive while click-through is on, so Shift can be watched without focus.
  useEffect(() => {
    const move = (e) => {
      const over = pillRef.current?.contains(e.target)
      if (e.shiftKey && over && !ghost) { setGhost(true); leave() }
      else if (ghost && (!e.shiftKey || !over)) { setGhost(false); if (over && !e.shiftKey) enter(e) }
    }
    window.addEventListener('mousemove', move)
    return () => window.removeEventListener('mousemove', move)
  }, [ghost])

  const decide = (b) => { if (b === 'allow') setLove((n) => n + 1); api?.decide(approval.id, b) }
  const changeSetting = (k, v) => api?.setSetting(k, v).then(setSettings)

  return (
    <motion.div
      ref={pillRef}
      layout
      transition={spring}
      className={`pill glow-${status} view-${view} ${ghost ? 'ghost' : ''}`}
      style={{ width: WIDTH[view === 'approval' || view === 'done' ? 'bar' : view === 'notch' && focus.running ? 'notchFocus' : view], borderBottomLeftRadius: view === 'notch' ? 16 : 24, borderBottomRightRadius: view === 'notch' ? 16 : 24 }}
      onMouseEnter={enter}
      onMouseLeave={leave}
    >
      <AnimatePresence mode="popLayout" initial={false}>
        <motion.div
          key={view === 'approval' ? 'approval' + approval.id : view}
          className="content"
          initial={{ opacity: 0 }}
          animate={{ opacity: 1 }}
          exit={{ opacity: 0, transition: { duration: 0.1 } }}
          transition={{ duration: 0.22 }}
        >
          {view === 'notch' && (
            <div className="notch">
              <Mascot mood={status} size={30} loveSignal={love} beat={primary?.activity} />
              {focus.running && (
                <span className="notch-focus">
                  <Ring value={focus.progress} size={16} stroke={2.5} color="#ff9f43" />
                  {mmss(focus.left)}
                </span>
              )}
            </div>
          )}

          {view === 'approval' && <ApprovalBar approval={approval} more={approvals.length - 1} onDecide={decide} />}

          {view === 'done' && (
            <div className="bar-row">
              <span className="done-badge"><Check size={13} /></span>
              <span className="ask clip"><b>Done</b> <span className="muted">in {flashed.folder}</span> {flashed.lastAnswer && <span className="muted">· {flashed.lastAnswer}</span>}</span>
              <Mascot mood="done" size={30} />
            </div>
          )}

          {view === 'dashboard' && (
            <div className="dash">
              <div className="toolbar">
                {page === 'settings' ? (
                  <button className="icon-btn" onClick={() => setPage('grid')} aria-label="Back"><Back size={14} /></button>
                ) : (
                  <span className="brand">Modak</span>
                )}
                <span className="spacer" />
                <button className="icon-btn" onClick={() => changeSetting('sound', !settings?.sound)} aria-label={settings?.sound ? 'Mute' : 'Unmute'}>
                  {settings?.sound ? <Sound size={14} /> : <Mute size={14} />}
                </button>
                <button className={`icon-btn ${page === 'settings' ? 'active' : ''}`} onClick={() => setPage((p) => (p === 'settings' ? 'grid' : 'settings'))} aria-label="Settings"><Gear size={14} /></button>
              </div>
              {page === 'settings' ? (
                <Settings settings={settings} onChange={changeSetting} />
              ) : (
                <div className="grid">
                  <AgentTile sessions={sessions} index={i} onCycle={() => setIndex((i + 1) % sessions.length)} mood={status} loveSignal={love} />
                  <Photos />
                  <Clock />
                  <Focus focus={focus} />
                  <Git git={widgets.git} />
                  <Usage usage={widgets.usage} />
                  <System sys={widgets.sys} />
                </div>
              )}
            </div>
          )}
        </motion.div>
      </AnimatePresence>
    </motion.div>
  )
}
