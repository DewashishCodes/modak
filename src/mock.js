// Dev only: when the UI runs in a normal browser (no Electron preload), fake the bridge with sample
// sessions so every state can be looked at. ?approval shows the approval bar, ?done the done bar, ?nophotos the empty slideshow.
if (!window.modak) {
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
  // Gradient stand-ins for real photos.
  const photo = (a, b) => 'data:image/svg+xml,' + encodeURIComponent(`<svg xmlns="http://www.w3.org/2000/svg" width="400" height="400"><defs><linearGradient id="g" x2="1" y2="1"><stop offset="0" stop-color="${a}"/><stop offset="1" stop-color="${b}"/></linearGradient></defs><rect width="400" height="400" fill="url(#g)"/><circle cx="290" cy="120" r="50" fill="#fff" opacity=".5"/></svg>`)
  const photos = q.has('nophotos') ? [] : [photo('#f97316', '#7c3aed'), photo('#06b6d4', '#1e3a8a'), photo('#f43f5e', '#fbbf24')]
  const subs = { signal: [] }
  window.modak = {
    setHover() {},
    getState: async () => ({ sessions, approvals }),
    onState: () => () => {},
    onSignal: (cb) => { subs.signal.push(cb); return () => {} },
    onDecidedByKey: () => () => {},
    decide: (id, b) => console.log('decide', id, b),
    widgets: async () => ({
      sys: { cpu: 23, ram: 61, ramUsedGb: 9.7, ramTotalGb: 16 },
      git: { branch: 'feat/checkout', changed: 6, added: 1, modified: 4, deleted: 1, ahead: 2, behind: 0, author: 'Dewashish', subject: 'wire tax into checkout total', when: '12 minutes ago',
        files: [{ kind: 'mod', path: 'src/checkout.ts' }, { kind: 'add', path: 'src/tax.ts' }, { kind: 'mod', path: 'src/cart.tsx' }, { kind: 'del', path: 'src/old-total.ts' }] },
      usage: { output: 259266, input: 630, cacheWrite: 638654, cacheRead: 61454455, total: 62353005, messages: 261, hours: [0, 0, 0, 0, 0, 0, 0, 0, 0, 2000, 9000, 14000, 30000, 52000, 41000, 22000, 36000, 48000, 5000, 0, 0, 0, 0, 0] },
    }),
    photos: async () => photos,
    openPhotos: () => console.log('open photos folder'),
    getSettings: async () => ({ connected: true, autostart: false, sound: true, photosDir: 'C:\\Users\\you\\Pictures\\Modak' }),
    setSetting: async (k, v) => ({ connected: true, autostart: false, sound: true, photosDir: 'C:\\Users\\you\\Pictures\\Modak', [k]: v }),
  }
  if (q.has('done')) setTimeout(() => subs.signal.forEach((cb) => cb({ type: 'done', sessionId: 'b' })), 1500) // after the app subscribes
}
