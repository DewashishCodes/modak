let ctx

// Soft two-note chime made with Web Audio, so there is no sound file to ship.
export function chime() {
  ctx ||= new AudioContext()
  const t = ctx.currentTime
  ;[[784, 0], [1175, 0.12]].forEach(([freq, at]) => {
    const osc = ctx.createOscillator()
    const gain = ctx.createGain()
    osc.type = 'sine'
    osc.frequency.value = freq
    gain.gain.setValueAtTime(0, t + at)
    gain.gain.linearRampToValueAtTime(0.18, t + at + 0.02)
    gain.gain.exponentialRampToValueAtTime(0.001, t + at + 0.6)
    osc.connect(gain).connect(ctx.destination)
    osc.start(t + at)
    osc.stop(t + at + 0.65)
  })
}
