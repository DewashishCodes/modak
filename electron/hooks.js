const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')

const SETTINGS = path.join(os.homedir(), '.claude', 'settings.json')
const EVENTS = ['SessionStart', 'SessionEnd', 'UserPromptSubmit', 'PreToolUse', 'PostToolUse', 'PermissionRequest', 'Notification', 'Stop']
const MARK = '/modak/' // every hook URL we add contains this, so uninstall touches only ours
const LEGACY_MARK = '/peninsula/' // the app's old name; still ours so a reinstall replaces those entries

const ours = (h) => h?.type === 'http' && (String(h.url || '').includes(MARK) || String(h.url || '').includes(LEGACY_MARK))

function read(file) {
  if (!fs.existsSync(file)) return {}
  const text = fs.readFileSync(file, 'utf8')
  if (!text.trim()) return {}
  return JSON.parse(text) // throws on a broken file: we never overwrite what we cannot read
}

function write(file, settings) {
  fs.mkdirSync(path.dirname(file), { recursive: true })
  const backup = file + '.modak-backup'
  if (fs.existsSync(file) && !fs.existsSync(backup)) fs.copyFileSync(file, backup)
  const tmp = file + '.modak-tmp'
  fs.writeFileSync(tmp, JSON.stringify(settings, null, 2) + '\n')
  fs.renameSync(tmp, file)
}

// Drops our handlers, then any matcher groups and events left empty by that.
function strip(settings) {
  const hooks = settings.hooks || {}
  for (const event of Object.keys(hooks)) {
    if (!Array.isArray(hooks[event])) continue
    hooks[event] = hooks[event]
      .map((g) => (Array.isArray(g?.hooks) ? { ...g, hooks: g.hooks.filter((h) => !ours(h)) } : g))
      .filter((g) => !Array.isArray(g?.hooks) || g.hooks.length)
    if (!hooks[event].length) delete hooks[event]
  }
  if (settings.hooks && !Object.keys(settings.hooks).length) delete settings.hooks
  return settings
}

function isInstalled(file = SETTINGS) {
  try {
    const hooks = read(file).hooks || {}
    return EVENTS.every((e) => (hooks[e] || []).some((g) => g?.hooks?.some(ours)))
  } catch {
    return false
  }
}

function install(url, file = SETTINGS) {
  const settings = strip(read(file))
  settings.hooks ||= {}
  for (const event of EVENTS) {
    // A held approval may wait for the user; everything else must answer fast or Claude moves on.
    const timeout = event === 'PermissionRequest' ? 600 : 5
    ;(settings.hooks[event] ||= []).push({ hooks: [{ type: 'http', url, timeout }] })
  }
  write(file, settings)
}

function uninstall(file = SETTINGS) {
  write(file, strip(read(file)))
}

// True when our hooks are installed but some point at another URL (old name or port), so they need refreshing.
function isStale(url, file = SETTINGS) {
  try {
    const all = Object.values(read(file).hooks || {}).flat().flatMap((g) => g?.hooks || []).filter(ours)
    return all.length > 0 && all.some((h) => h.url !== url)
  } catch {
    return false
  }
}

module.exports = { install, uninstall, isInstalled, isStale, SETTINGS, EVENTS }
