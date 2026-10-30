const { execFile } = require('node:child_process')

const git = (cwd, args) => new Promise((resolve) => {
  execFile('git', ['-C', cwd, ...args], { timeout: 2000, windowsHide: true }, (err, out) => resolve(err ? null : out.trimEnd())) // keep leading spaces: porcelain status codes start with one
})

// Branch, changed file count and last commit for the folder a session runs in; null if it isn't a repo.
async function gitInfo(cwd) {
  if (!cwd) return null
  const branch = await git(cwd, ['rev-parse', '--abbrev-ref', 'HEAD'])
  if (branch === null) return null
  const [status, log, counts] = await Promise.all([
    git(cwd, ['status', '--porcelain']),
    git(cwd, ['log', '-1', '--format=%s%x1f%cr%x1f%an']),
    git(cwd, ['rev-list', '--left-right', '--count', '@{upstream}...HEAD']), // null when there is no upstream
  ])
  const [subject = '', when = '', author = ''] = (log || '').split('\x1f')
  const files = status ? status.split('\n') : []
  const count = (re) => files.filter((l) => re.test(l.slice(0, 2))).length
  const kind = (xy) => (/D/.test(xy) ? 'del' : /\?\?|A/.test(xy) ? 'add' : 'mod')
  const [behind, ahead] = counts ? counts.split(/\s+/).map(Number) : [null, null]
  return {
    branch,
    changed: files.length,
    added: count(/\?\?|A/),
    modified: count(/M|R|C|U/),
    deleted: count(/D/),
    files: files.slice(0, 4).map((l) => ({ kind: kind(l.slice(0, 2)), path: l.slice(3).replace(/^"|"$/g, '') })),
    ahead,
    behind,
    subject,
    when,
    author,
  }
}

module.exports = { gitInfo }
