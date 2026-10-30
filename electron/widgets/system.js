const os = require('node:os')

const totals = () => os.cpus().reduce((a, c) => {
  const t = c.times
  a.idle += t.idle
  a.all += t.user + t.nice + t.sys + t.idle + t.irq
  return a
}, { idle: 0, all: 0 })

let prev = totals()

// CPU % since the previous call, plus RAM in use.
function systemStats() {
  const now = totals()
  const all = now.all - prev.all
  const cpu = all > 0 ? Math.round(100 * (1 - (now.idle - prev.idle) / all)) : 0
  prev = now
  const total = os.totalmem()
  const used = total - os.freemem()
  return { cpu, ramUsedGb: +(used / 2 ** 30).toFixed(1), ramTotalGb: Math.round(total / 2 ** 30), ram: Math.round((100 * used) / total) }
}

module.exports = { systemStats }
