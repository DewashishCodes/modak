const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')

const { StringDecoder } = require('node:string_decoder')

const ROOT = path.join(os.homedir(), '.claude', 'projects')
const CHUNK = 1024 * 1024

// Today's token use summed from Claude Code transcripts. Files are read incrementally (only new bytes)
// because the active transcript grows on every turn.
function createUsage(root = ROOT) {
  let day, seen, offsets, totals, partial

  const reset = (d) => {
    day = d
    seen = new Set() // one assistant message is logged once per content block, all with the same usage
    offsets = new Map()
    partial = new Map()
    totals = { input: 0, output: 0, cacheRead: 0, cacheWrite: 0, messages: 0, hours: new Array(24).fill(0) }
  }

  const files = (dir) => {
    let out = []
    let entries
    try { entries = fs.readdirSync(dir, { withFileTypes: true }) } catch { return out }
    for (const e of entries) {
      const p = path.join(dir, e.name)
      if (e.isDirectory()) out = out.concat(files(p))
      else if (e.name.endsWith('.jsonl')) out.push(p)
    }
    return out
  }

  function addLine(line, start) {
    if (!line.includes('"usage"')) return
    let j
    try { j = JSON.parse(line) } catch { return }
    const u = j.message?.usage
    if (j.type !== 'assistant' || !u || !j.timestamp || new Date(j.timestamp) < start) return
    const key = j.message.id || j.requestId || j.uuid
    if (seen.has(key)) return
    seen.add(key)
    totals.input += u.input_tokens || 0
    totals.output += u.output_tokens || 0
    totals.cacheRead += u.cache_read_input_tokens || 0
    totals.cacheWrite += u.cache_creation_input_tokens || 0
    totals.messages++
    totals.hours[new Date(j.timestamp).getHours()] += u.output_tokens || 0
  }

  return function usageToday(now = new Date()) {
    const start = new Date(now.getFullYear(), now.getMonth(), now.getDate())
    if (day !== start.getTime()) reset(start.getTime())

    for (const f of files(root)) {
      let st
      try { st = fs.statSync(f) } catch { continue }
      if (st.mtime < start) continue
      const from = offsets.get(f) || 0
      if (st.size <= from) continue
      // Chunked so a day of big transcripts never sits in memory at once; the decoder keeps characters split across chunks whole.
      const fd = fs.openSync(f, 'r')
      const buf = Buffer.alloc(Math.min(CHUNK, st.size - from))
      const dec = new StringDecoder('utf8')
      let pos = from
      try {
        while (pos < st.size) {
          const n = fs.readSync(fd, buf, 0, Math.min(buf.length, st.size - pos), pos)
          if (!n) break
          pos += n
          const lines = ((partial.get(f) || '') + dec.write(buf.subarray(0, n))).split('\n')
          partial.set(f, lines.pop()) // last line may still be being written
          for (const l of lines) addLine(l, start)
        }
      } finally { fs.closeSync(fd) }
      offsets.set(f, pos)
    }
    return { ...totals, hours: [...totals.hours], total: totals.input + totals.output + totals.cacheRead + totals.cacheWrite }
  }
}

module.exports = { createUsage }
