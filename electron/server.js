const http = require('node:http')
const path = require('node:path')
const crypto = require('node:crypto')
const { describe, detail, clip } = require('./describe')

const HOOK_PATH = '/peninsula/hook'
const MAX_BODY = 1024 * 1024

// Receives Claude Code HTTP hooks, keeps per-session state, and holds PermissionRequest
// responses open until the user decides in the pill.
// onChange(state) fires after every change; onSignal({ type, sessionId }) fires for one-off moments (done).
function createServer({ onChange = () => {}, onSignal = () => {} } = {}) {
  const sessions = new Map() // session_id -> { id, cwd, folder, status, activity, lastAnswer, updatedAt }
  const pending = new Map() // approval id -> { id, sessionId, tool, detail, createdAt, res }
  let allowedHosts = []

  const state = () => ({
    sessions: [...sessions.values()].sort((a, b) => b.updatedAt - a.updatedAt),
    approvals: [...pending.values()].map(({ res, ...a }) => a).sort((a, b) => a.createdAt - b.createdAt),
  })
  const changed = () => onChange(state())

  function reply(res, body) {
    if (res.writableEnded) return
    res.writeHead(200, { 'Content-Type': 'application/json' })
    res.end(body ? JSON.stringify(body) : '')
  }

  // Empty reply = "no decision", so Claude Code shows its normal terminal prompt.
  function release(id, body) {
    const p = pending.get(id)
    if (!p) return
    pending.delete(id)
    reply(p.res, body)
  }
  const releaseSession = (sessionId) => {
    for (const p of [...pending.values()]) if (p.sessionId === sessionId) release(p.id)
  }

  function touch(ev) {
    let s = sessions.get(ev.session_id)
    if (!s) {
      s = { id: ev.session_id, cwd: ev.cwd || '', status: 'idle', activity: '', lastAnswer: '' }
      sessions.set(s.id, s)
    }
    if (ev.cwd) { s.cwd = ev.cwd; s.folder = path.basename(ev.cwd) }
    s.updatedAt = Date.now()
    return s
  }

  // Returns true when the response is held open (PermissionRequest).
  function handle(ev, res) {
    if (!ev || typeof ev.session_id !== 'string') return false
    const name = ev.hook_event_name

    if (name === 'SessionEnd') {
      releaseSession(ev.session_id)
      sessions.delete(ev.session_id)
      changed()
      return false
    }

    const s = touch(ev)
    switch (name) {
      case 'SessionStart':
        s.status = 'idle'
        break
      case 'UserPromptSubmit':
        s.status = 'working'
        s.activity = 'Thinking'
        break
      case 'PreToolUse':
        s.status = 'working'
        s.activity = describe(ev.tool_name, ev.tool_input)
        break
      case 'PostToolUse':
        releaseSession(s.id) // a held card for this session was answered in the terminal
        s.status = 'working'
        break
      case 'PermissionRequest': {
        releaseSession(s.id) // only one open card per session
        const id = crypto.randomUUID()
        pending.set(id, { id, sessionId: s.id, folder: s.folder, tool: ev.tool_name, detail: detail(ev.tool_name, ev.tool_input), activity: describe(ev.tool_name, ev.tool_input), createdAt: Date.now(), res })
        res.on('close', () => { if (pending.delete(id)) changed() }) // Claude gave up or timed out
        s.status = 'asking'
        s.activity = `Wants to use ${ev.tool_name}`
        changed()
        return true
      }
      case 'Notification':
        if (![...pending.values()].some((p) => p.sessionId === s.id)) {
          s.status = 'asking'
          s.activity = clip(String(ev.message || 'Needs your input'))
        }
        break
      case 'Stop':
        releaseSession(s.id)
        s.status = 'done'
        s.activity = 'Done'
        s.lastAnswer = clip(String(ev.last_assistant_message || ''), 600)
        onSignal({ type: 'done', sessionId: s.id })
        break
      default:
        return false
    }
    changed()
    return false
  }

  const server = http.createServer((req, res) => {
    if (req.method !== 'POST' || req.url !== HOOK_PATH) { res.writeHead(404).end(); return }
    // Browsers cannot send these cross-site without a CORS preflight we never answer; Host check stops DNS rebinding.
    if (!allowedHosts.includes(req.headers.host)) { res.writeHead(403).end(); return }
    if (!String(req.headers['content-type'] || '').startsWith('application/json')) { res.writeHead(415).end(); return }

    let size = 0
    const chunks = []
    req.on('data', (c) => {
      size += c.length
      if (size > MAX_BODY) { res.writeHead(413).end(); req.destroy() }
      else chunks.push(c)
    })
    req.on('end', () => {
      if (res.writableEnded) return
      let ev
      try { ev = JSON.parse(Buffer.concat(chunks).toString('utf8')) } catch { return reply(res) }
      try {
        if (!handle(ev, res)) reply(res)
      } catch (err) {
        console.error('[peninsula] hook error', err) // never block Claude Code on our bug
        reply(res)
      }
    })
  })

  return {
    state,
    listen(port, host = '127.0.0.1') {
      return new Promise((resolve, reject) => {
        server.once('error', reject)
        server.listen(port, host, () => {
          const p = server.address().port
          allowedHosts = [`127.0.0.1:${p}`, `localhost:${p}`]
          resolve(p)
        })
      })
    },
    // behavior: 'allow' | 'deny' | 'terminal'
    decide(id, behavior) {
      const p = pending.get(id)
      if (!p) return
      const s = sessions.get(p.sessionId)
      if (behavior === 'terminal') release(id)
      else {
        const decision = behavior === 'allow' ? { behavior: 'allow' } : { behavior: 'deny', message: 'Denied from Peninsula' }
        release(id, { hookSpecificOutput: { hookEventName: 'PermissionRequest', decision } })
      }
      if (s) { s.status = 'working'; s.activity = behavior === 'deny' ? 'Denied' : p.activity }
      changed()
    },
    // On quit: hand every open request back to the terminal prompt.
    close() {
      for (const id of [...pending.keys()]) release(id)
      return new Promise((r) => server.close(() => r()))
    },
  }
}

module.exports = { createServer, HOOK_PATH }
