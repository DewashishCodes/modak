// Dev only: when the UI runs in a normal browser (no Electron preload), fake the bridge with sample
// sessions so every state can be looked at. ?approval shows the approval bar, ?done the done bar.
if (!window.peninsula) {
  const q = new URLSearchParams(location.search)
  const sessions = [
    { id: 'a', folder: 'webshop', cwd: '.', status: 'working', activity: 'Editing checkout.ts', updatedAt: 2,
      preview: { kind: 'diff', file: 'checkout.ts', lines: [['-', 'const total = 0'], ['+', 'const total = sum(items)'], ['+', 'const tax = total * TAX_RATE']] } },
    { id: 'b', folder: 'api-server', cwd: '.', status: 'working', activity: 'Running npm test', updatedAt: 1,
      preview: { kind: 'term', command: 'npm test', output: ['> api-server@1.0.0 test', '✓ auth (12 ms)', '✓ orders (40 ms)', 'Tests: 46 passed, 46 total'] } },
  ]
  const approvals = q.has('approval')
    ? [{ id: 'p1', sessionId: 'a', folder: 'webshop', tool: 'Bash', ask: 'git push origin main', detail: 'git push origin main', preview: { kind: 'term', command: 'git push origin main', output: [] } }]
    : []
  const subs = { signal: [] }
  window.peninsula = {
    setHover() {},
    getState: async () => ({ sessions, approvals }),
    onState: () => () => {},
    onSignal: (cb) => { subs.signal.push(cb); return () => {} },
    decide: (id, b) => console.log('decide', id, b),
    widgets: async () => ({ sys: { cpu: 23, ram: 61, ramUsedGb: 9.7, ramTotalGb: 16 }, git: { branch: 'feat/checkout', changed: 4, added: 1, modified: 2, deleted: 1, subject: 'wire tax', when: '12 minutes ago' }, usage: { output: 259266, total: 62353005, messages: 261, hours: [0,0,0,0,0,0,0,0,0,2000,9000,14000,30000,52000,41000,22000,36000,48000,5000,0,0,0,0,0] } }),
    onMedia: (cb) => { setTimeout(() => cb({ playing: true, title: 'Blinding Lights', artist: 'The Weeknd', app: 'Spotify.exe' }), 50); return () => {} },
    media: (a) => console.log('media', a),
    getSettings: async () => ({ connected: true, autostart: false, sound: true }),
    setSetting: async (k, v) => ({ connected: true, autostart: false, sound: true, [k]: v }),
  }
  if (q.has('done')) setTimeout(() => subs.signal.forEach((cb) => cb({ type: 'done', sessionId: 'b' })), 1500) // after the app subscribes
}
