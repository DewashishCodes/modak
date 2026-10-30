import { useEffect, useRef, useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'
import Mascot from './Mascot.jsx'
import { chime } from './chime.js'

const spring = { type: 'spring', stiffness: 400, damping: 30 }
const DONE_FLASH_MS = 5000
const api = window.peninsula

function usePeninsula() {
  const [state, setState] = useState({ sessions: [], approvals: [] })
  const [flash, setFlash] = useState(null) // session id that just finished
  const timer = useRef()

  useEffect(() => {
    if (!api) return
    api.getState().then(setState)
    const offState = api.onState(setState)
    const offSignal = api.onSignal((s) => {
      if (s.type !== 'done') return
      chime()
      setFlash(s.sessionId)
      clearTimeout(timer.current)
      timer.current = setTimeout(() => setFlash(null), DONE_FLASH_MS)
    })
    return () => { offState(); offSignal(); clearTimeout(timer.current) }
  }, [])

  return { ...state, flash }
}

function Approval({ approval, more }) {
  const decide = (b) => api?.decide(approval.id, b)
  return (
    <>
      <div className="card-title">
        <b>{approval.folder || 'Claude'}</b> wants to use <b>{approval.tool}</b>
        {more > 0 && <span className="more">+{more} more</span>}
      </div>
      <pre className="detail">{approval.detail}</pre>
      <div className="buttons">
        <button className="btn allow" onClick={() => decide('allow')}>Allow</button>
        <button className="btn deny" onClick={() => decide('deny')}>Deny</button>
        <button className="btn ghost" onClick={() => decide('terminal')}>Answer in terminal</button>
      </div>
    </>
  )
}

function Sessions({ sessions }) {
  if (!sessions.length) return <div className="empty">No Claude Code sessions yet. Start <code>claude</code> in a terminal.</div>
  return sessions.slice(0, 4).map((s) => (
    <div className="session" key={s.id}>
      <div className="session-head">
        <span className={`dot ${s.status}`} />
        <b>{s.folder || 'session'}</b>
        <span className="sub">{s.activity}</span>
      </div>
      {s.status === 'done' && s.lastAnswer && <div className="answer clamp2">{s.lastAnswer}</div>}
    </div>
  ))
}

export default function App() {
  const { sessions, approvals, flash } = usePeninsula()
  const [hover, setHover] = useState(false)

  const approval = approvals[0]
  const flashed = flash && sessions.find((s) => s.id === flash)
  const primary = (approval && sessions.find((s) => s.id === approval.sessionId)) || flashed || sessions[0]
  const view = approval ? 'approval' : flashed ? 'done' : hover ? 'list' : null

  const mood = approval ? 'asking' : flashed ? 'done' : primary?.status === 'done' ? 'idle' : primary?.status || 'idle'

  const enter = () => { api?.setHover(true); setHover(true) }
  const leave = () => { api?.setHover(false); setHover(false) }

  return (
    <motion.div
      layout
      transition={spring}
      className="pill"
      style={{ width: view ? 440 : 260, borderBottomLeftRadius: 22, borderBottomRightRadius: 22 }}
      onMouseEnter={enter}
      onMouseLeave={leave}
    >
      <motion.div layout="position" transition={spring} className="row">
        <Mascot mood={mood} />
        <span className="title">{primary?.folder || 'Peninsula'}</span>
        <span className="sub">{primary?.activity || 'idle'}</span>
      </motion.div>
      <AnimatePresence mode="popLayout">
        {view && (
          <motion.div
            key={view + (approval?.id || '')}
            className="body"
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, transition: { duration: 0.08 } }}
            transition={spring}
          >
            {view === 'approval' && <Approval approval={approval} more={approvals.length - 1} />}
            {view === 'done' && (
              <>
                <div className="card-title">Done in <b>{flashed.folder || 'Claude'}</b></div>
                {flashed.lastAnswer && <div className="answer clamp4">{flashed.lastAnswer}</div>}
              </>
            )}
            {view === 'list' && <Sessions sessions={sessions} />}
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  )
}
