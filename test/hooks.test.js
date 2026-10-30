const { test } = require('node:test')
const assert = require('node:assert')
const fs = require('node:fs')
const os = require('node:os')
const path = require('node:path')
const { install, uninstall, isInstalled, EVENTS } = require('../electron/hooks')

const URL = 'http://127.0.0.1:47821/modak/hook'
const tmpFile = (content) => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'modak-'))
  const file = path.join(dir, 'settings.json')
  if (content !== undefined) fs.writeFileSync(file, typeof content === 'string' ? content : JSON.stringify(content))
  return file
}
const load = (f) => JSON.parse(fs.readFileSync(f, 'utf8'))

const userSettings = {
  model: 'opus',
  hooks: { Stop: [{ hooks: [{ type: 'command', command: 'echo mine' }] }], PreToolUse: [{ matcher: 'Bash', hooks: [{ type: 'command', command: 'lint' }] }] },
}

test('install adds every event, keeps user settings and hooks, and makes a backup', () => {
  const f = tmpFile(userSettings)
  install(URL, f)
  const s = load(f)
  assert.equal(s.model, 'opus')
  assert.equal(s.hooks.Stop[0].hooks[0].command, 'echo mine')
  assert.equal(s.hooks.PreToolUse[0].matcher, 'Bash')
  assert.ok(isInstalled(f))
  for (const e of EVENTS) assert.ok(s.hooks[e].some((g) => g.hooks[0].url === URL), e)
  assert.equal(s.hooks.PermissionRequest.at(-1).hooks[0].timeout, 600)
  assert.deepEqual(load(f + '.modak-backup'), userSettings)
})

test('hooks from the old Peninsula name are detected as stale and replaced on install', () => {
  const { isStale } = require('../electron/hooks')
  const f = tmpFile(userSettings)
  install('http://127.0.0.1:47821/peninsula/hook', f)
  assert.ok(isStale(URL, f))
  install(URL, f)
  assert.ok(!isStale(URL, f))
  const urls = Object.values(load(f).hooks).flat().flatMap((g) => g.hooks).map((h) => h.url).filter(Boolean)
  assert.ok(urls.every((u) => u === URL))
  assert.equal(load(f).hooks.Stop.length, 2) // user's + ours, the old entry is gone
})

test('install twice does not duplicate', () => {
  const f = tmpFile(userSettings)
  install(URL, f)
  install(URL, f)
  assert.equal(load(f).hooks.Stop.length, 2) // user's + ours
})

test('uninstall restores the original settings exactly', () => {
  const f = tmpFile(userSettings)
  install(URL, f)
  uninstall(f)
  assert.deepEqual(load(f), userSettings)
  assert.ok(!isInstalled(f))
})

test('works when settings.json does not exist yet', () => {
  const f = tmpFile()
  install(URL, f)
  assert.ok(isInstalled(f))
  uninstall(f)
  assert.deepEqual(load(f), {})
})

test('refuses to overwrite a settings file it cannot parse', () => {
  const f = tmpFile('{ broken')
  assert.throws(() => install(URL, f))
  assert.equal(fs.readFileSync(f, 'utf8'), '{ broken')
})
