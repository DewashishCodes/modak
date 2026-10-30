const path = require('node:path')

const clip = (s, n = 60) => (s.length > n ? s.slice(0, n - 1) + '…' : s)
const base = (p) => (p ? path.basename(String(p)) : '')

// One short line saying what a tool call does, for the collapsed pill.
function describe(tool, input = {}) {
  switch (tool) {
    case 'Edit':
    case 'MultiEdit':
    case 'Write':
    case 'NotebookEdit':
      return `Editing ${base(input.file_path || input.notebook_path)}`
    case 'Read':
      return `Reading ${base(input.file_path)}`
    case 'Bash':
    case 'PowerShell':
      return `Running ${clip(String(input.command || '').split('\n')[0], 48)}`
    case 'Grep':
    case 'Glob':
      return `Searching ${clip(String(input.pattern || ''), 40)}`
    case 'WebFetch':
      return `Fetching ${clip(String(input.url || ''), 48)}`
    case 'WebSearch':
      return `Searching the web`
    case 'Task':
    case 'Agent':
      return `Running an agent`
    default:
      return clip(String(tool || 'Working'))
  }
}

// The detail shown on an approval card: the command, the file, or the raw input.
function detail(tool, input = {}) {
  if (input.command) return clip(String(input.command), 400)
  if (input.file_path) return String(input.file_path)
  if (input.url) return String(input.url)
  return clip(JSON.stringify(input), 400)
}

module.exports = { describe, detail, clip }
