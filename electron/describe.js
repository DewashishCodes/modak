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

const PREVIEW_LINES = 8
const lines = (s) => String(s ?? '').replace(/\r/g, '').split('\n')
const tail = (arr, n) => arr.slice(Math.max(0, arr.length - n))

// What the agent tile's terminal box shows for a tool call, or null to keep the previous preview.
function preview(tool, input = {}) {
  switch (tool) {
    case 'Edit': {
      const out = [...lines(input.old_string).map((s) => ['-', s]), ...lines(input.new_string).map((s) => ['+', s])]
      return { kind: 'diff', file: base(input.file_path), lines: out.slice(0, PREVIEW_LINES) }
    }
    case 'MultiEdit': {
      const e = input.edits?.[0] || {}
      return preview('Edit', { file_path: input.file_path, ...e })
    }
    case 'Write':
      return { kind: 'diff', file: base(input.file_path), lines: lines(input.content).slice(0, PREVIEW_LINES).map((s) => ['+', s]) }
    case 'Bash':
    case 'PowerShell':
      return { kind: 'term', command: clip(String(input.command || '').split('\n')[0], 120), output: [] }
    default:
      return null
  }
}

// Last lines of a Bash result; tool_response is an object with stdout/stderr or a plain string.
function outputTail(response) {
  const text = typeof response === 'string' ? response : [response?.stdout, response?.stderr].filter(Boolean).join('\n')
  return tail(lines(text).filter((l) => l.trim()), 6).map((l) => clip(l, 160))
}

// Short question for the slim approval bar: "Allow: git push origin main?"
function askLine(tool, input = {}) {
  if (input.command) return clip(String(input.command).split('\n')[0], 56)
  if (input.file_path) return `${tool === 'Read' ? 'read' : 'edit'} ${base(input.file_path)}`
  if (input.url) return `fetch ${clip(String(input.url), 48)}`
  return `use ${tool}`
}

module.exports = { describe, detail, clip, preview, outputTail, askLine }
