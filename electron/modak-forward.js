// Codex hook command: `node modak-forward.js <hook url>`. Pipes the event on stdin to Modak and prints its reply.
// Must never block Codex: if Modak is not running we answer "no decision" right away.
const http = require('node:http')

// Codex wants JSON on stdout (Stop rejects plain text); {} means "no decision", like Modak's empty reply.
const done = (out) => {
  process.stdout.write(out && out.trim() ? out : '{}')
  process.exit(0)
}

let input = ''
process.stdin.setEncoding('utf8')
process.stdin.on('data', (c) => (input += c))
process.stdin.on('end', () => {
  let ev
  try { ev = JSON.parse(input) } catch { return done() }
  const req = http.request(process.argv[2], { method: 'POST', headers: { 'Content-Type': 'application/json' } }, (res) => {
    let out = ''
    res.setEncoding('utf8')
    res.on('data', (c) => (out += c))
    res.on('end', () => done(res.statusCode === 200 ? out : ''))
  })
  req.on('error', () => done())
  req.end(JSON.stringify({ ...ev, modak_agent: 'codex' }))
})
