const { execFile } = require('node:child_process')

const git = (cwd, args) => new Promise((resolve) => {
  execFile('git', ['-C', cwd, ...args], { timeout: 2000, windowsHide: true }, (err, out) => resolve(err ? null : out.trim()))
})

// Branch, changed file count and last commit for the folder a session runs in; null if it isn't a repo.
async function gitInfo(cwd) {
  if (!cwd) return null
  const branch = await git(cwd, ['rev-parse', '--abbrev-ref', 'HEAD'])
  if (branch === null) return null
  const [status, log] = await Promise.all([git(cwd, ['status', '--porcelain']), git(cwd, ['log', '-1', '--format=%s%x1f%cr'])])
  const [subject = '', when = ''] = (log || '').split('\x1f')
  const files = status ? status.split('\n') : []
  const count = (re) => files.filter((l) => re.test(l.slice(0, 2))).length
  return { branch, changed: files.length, added: count(/\?\?|A/), modified: count(/M|R|C|U/), deleted: count(/D/), subject, when }
}

module.exports = { gitInfo }
