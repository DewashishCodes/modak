const { test, beforeEach, afterEach } = require('node:test')
const assert = require('node:assert')
const { createServer, HOOK_PATH } = require('../electron/server')

let srv, port, last, signals

beforeEach(async () => {
  signals = []
  srv = createServer({ onChange: (s) => (last = s), onSignal: (s) => signals.push(s) })
  port = await srv.listen(0)
})
afterEach(() => srv.close())

const post = (body, headers = {}) =>
  fetch(`http://127.0.0.1:${port}${HOOK_PATH}`, {
    method: 'POST',
    headers: { 'content-type': 'application/json', ...headers },
    body: typeof body === 'string' ? body : JSON.stringify(body),
  })

const ev = (hook_event_name, extra = {}) => ({ session_id: 's1', cwd: 'C:\\code\\demo', hook_event_name, ...extra })
const waitFor = async (fn) => { for (let i = 0; i < 100 && !fn(); i++) await new Promise((r) => setTimeout(r, 10)) }

test('tool events update session activity and return empty 200', async () => {
  const r = await post(ev('PreToolUse', { tool_name: 'Bash', tool_input: { command: 'npm test' } }))
  assert.equal(r.status, 200)
  assert.equal(await r.text(), '')
  assert.equal(last.sessions[0].folder, 'demo')
  assert.equal(last.sessions[0].activity, 'Running npm test')
  assert.equal(last.sessions[0].status, 'working')
})

test('PermissionRequest is held until allow, then returns the decision JSON', async () => {
  const p = post(ev('PermissionRequest', { tool_name: 'Bash', tool_input: { command: 'rm -rf build' } }))
  await waitFor(() => last?.approvals.length === 1)
  assert.equal(last.approvals[0].detail, 'rm -rf build')
  assert.equal(last.sessions[0].status, 'asking')
  srv.decide(last.approvals[0].id, 'allow')
  const body = await (await p).json()
  assert.deepEqual(body, { hookSpecificOutput: { hookEventName: 'PermissionRequest', decision: { behavior: 'allow' } } })
  assert.equal(last.approvals.length, 0)
  assert.equal(last.sessions[0].activity, 'Running rm -rf build')
})

test('deny returns deny with a message', async () => {
  const p = post(ev('PermissionRequest', { tool_name: 'Write', tool_input: { file_path: 'a.txt' } }))
  await waitFor(() => last?.approvals.length === 1)
  srv.decide(last.approvals[0].id, 'deny')
  const body = await (await p).json()
  assert.equal(body.hookSpecificOutput.decision.behavior, 'deny')
  assert.ok(body.hookSpecificOutput.decision.message)
})

test('answer in terminal returns an empty body', async () => {
  const p = post(ev('PermissionRequest', { tool_name: 'Bash', tool_input: { command: 'ls' } }))
  await waitFor(() => last?.approvals.length === 1)
  srv.decide(last.approvals[0].id, 'terminal')
  assert.equal(await (await p).text(), '')
})

test('PostToolUse from the same session releases a card answered in the terminal', async () => {
  const p = post(ev('PermissionRequest', { tool_name: 'Bash', tool_input: { command: 'ls' } }))
  await waitFor(() => last?.approvals.length === 1)
  await post(ev('PostToolUse', { tool_name: 'Bash' }))
  assert.equal(await (await p).text(), '')
  assert.equal(last.approvals.length, 0)
})

test('close hands open requests back to the terminal', async () => {
  const p = post(ev('PermissionRequest', { tool_name: 'Bash', tool_input: { command: 'ls' } }))
  await waitFor(() => last?.approvals.length === 1)
  await srv.close()
  assert.equal(await (await p).text(), '')
  srv = createServer() // afterEach closes something
  await srv.listen(0)
})

test('Stop marks done, keeps the last answer and signals', async () => {
  await post(ev('Stop', { last_assistant_message: 'All tests pass.' }))
  assert.equal(last.sessions[0].status, 'done')
  assert.equal(last.sessions[0].lastAnswer, 'All tests pass.')
  assert.deepEqual(signals, [{ type: 'done', sessionId: 's1' }])
})

test('SessionEnd removes the session', async () => {
  await post(ev('SessionStart'))
  await post(ev('SessionEnd'))
  assert.equal(last.sessions.length, 0)
})

test('bad JSON gets an empty 200, never an error', async () => {
  const r = await post('{nope')
  assert.equal(r.status, 200)
  assert.equal(await r.text(), '')
})

test('rejects wrong content type, wrong host and wrong path', async () => {
  assert.equal((await post(ev('Stop'), { 'content-type': 'text/plain' })).status, 415)
  // fetch silently drops a custom Host header, so use http.request for the DNS-rebinding case
  const status = await new Promise((resolve, reject) => {
    const req = require('node:http').request(
      { port, path: HOOK_PATH, method: 'POST', headers: { host: 'evil.example', 'content-type': 'application/json' } },
      (res) => { res.resume(); resolve(res.statusCode) })
    req.on('error', reject)
    req.end('{}')
  })
  assert.equal(status, 403)
  const r = await fetch(`http://127.0.0.1:${port}/other`, { method: 'POST' })
  assert.equal(r.status, 404)
})
