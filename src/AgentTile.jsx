import Mascot from './Mascot.jsx'
import Preview from './Preview.jsx'
import { Check } from './icons.jsx'

const STATUS = {
  working: (s) => <><span className="spinner" />{s.activity || 'Working'}</>,
  asking: (s) => <><span className="q">?</span>{s.activity || 'Needs you'}</>,
  done: () => <><Check size={13} />Done</>,
  idle: () => <>Idle</>,
}

// The big tile: who is working, what they're doing right now, and a live terminal/diff box.
export default function AgentTile({ sessions, index, onCycle, mood, loveSignal }) {
  const s = sessions[index]
  if (!s) {
    return (
      <div className="tile agent">
        <div className="agent-head">
          <Mascot mood="idle" size={52} />
          <div>
            <div className="agent-name">No sessions yet</div>
            <div className="agent-status idle">Start <code>claude</code> in a terminal</div>
          </div>
        </div>
        <Preview preview={null} />
      </div>
    )
  }
  return (
    <div className="tile agent">
      <div className="agent-head">
        <Mascot mood={mood} size={52} loveSignal={loveSignal} beat={s.activity} />
        <div className="agent-meta">
          <div className="agent-name">
            <span className={`dot ${s.status}`} />
            <b>{s.folder || 'session'}</b>
            <span className="muted">{s.agent === 'codex' ? 'Codex' : 'Claude Code'}</span>
            {sessions.length > 1 && (
              <button className="counter" onClick={onCycle} title="Next session">{index + 1}/{sessions.length}</button>
            )}
          </div>
          <div className={`agent-status ${s.status}`}>{(STATUS[s.status] || STATUS.idle)(s)}</div>
        </div>
      </div>
      <Preview preview={s.preview} working={s.status === 'working'} />
    </div>
  )
}
