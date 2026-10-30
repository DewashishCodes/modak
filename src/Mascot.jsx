import { useEffect, useRef, useState } from 'react'
import { motion, useAnimationControls } from 'motion/react'
import { MODAK, MODAK_DEFS, MODAK_CSS, LOD_PX } from './modak.generated.js'

const POKES_TO_DIZZY = 5
const POKE_WINDOW_MS = 2500
const SLEEP_AFTER_MS = 2 * 60 * 1000
// Every frame of the big transparent window costs real GPU on Windows, so Modak animates
// for a moment after something happens, then holds still and only blinks now and then.
const REST_AFTER_MS = 5000
const BLINK_EVERY_MS = 4500

// Gradients and filters are shared by every Modak on the page, so they are added to the document once.
if (typeof document !== 'undefined' && !document.getElementById('modak-defs')) {
  const host = document.createElement('div')
  host.id = 'modak-defs'
  host.innerHTML = MODAK_DEFS
  document.body.prepend(host)
  const style = document.createElement('style')
  style.textContent = MODAK_CSS
  document.head.append(style)
}

// Modak, the mascot. mood: idle | working | asking | done; it adds love, wink, dizzy and sleepy on its own.
// beat: anything that changes when the agent does something new (its activity line), wakes Modak up.
// still: start resting, so mounting it (the dashboard tile) costs no animation frames.
export default function Mascot({ mood = 'idle', size = 26, loveSignal = 0, beat, still = false }) {
  const [override, setOverride] = useState(null)
  const [sleepy, setSleepy] = useState(false)
  const [rest, setRest] = useState(still)
  const woke = useRef(!still)
  const [blink, setBlink] = useState(false)
  const pokes = useRef([])
  const timer = useRef()
  const squish = useAnimationControls()

  const flash = (face, ms) => {
    clearTimeout(timer.current)
    setOverride(face)
    timer.current = setTimeout(() => setOverride(null), ms)
  }

  // Heart eyes for a moment whenever the user approves something.
  useEffect(() => { if (loveSignal) flash('love', 1600) }, [loveSignal])

  // Falls asleep after a long idle.
  useEffect(() => {
    setSleepy(false)
    if (mood !== 'idle') return
    const t = setTimeout(() => setSleepy(true), SLEEP_AFTER_MS)
    return () => clearTimeout(t)
  }, [mood])

  useEffect(() => () => clearTimeout(timer.current), [])

  const poke = (e) => {
    e.stopPropagation()
    squish.start({ scaleX: [1, 1.18, 0.94, 1], scaleY: [1, 0.82, 1.06, 1], transition: { duration: 0.38 } })
    setSleepy(false)
    const now = Date.now()
    pokes.current = [...pokes.current.filter((t) => now - t < POKE_WINDOW_MS), now]
    if (pokes.current.length >= POKES_TO_DIZZY) {
      pokes.current = []
      flash('dizzy', 2400)
    } else if (pokes.current.length === 1) flash('wink', 700)
  }

  const face = override || (mood === 'idle' && sleepy ? 'sleepy' : mood)

  useEffect(() => {
    if (woke.current) setRest(false)
    woke.current = true
    const t = setTimeout(() => setRest(true), REST_AFTER_MS)
    const b = face === 'idle' && setInterval(() => { setBlink(true); setTimeout(() => setBlink(false), 250) }, BLINK_EVERY_MS)
    return () => { clearTimeout(t); clearInterval(b) }
  }, [face, beat])
  const art = MODAK[face] || MODAK.idle
  const html = (size < LOD_PX ? art.small : art.full).replaceAll('__S__', String(size))

  return (
    <motion.span
      className={`mascot ${rest ? 'rest' : ''} ${blink ? 'blink' : ''}`}
      style={{ width: size, height: size, originY: 1 }}
      animate={squish}
      onClick={poke}
      title="Modak"
      dangerouslySetInnerHTML={{ __html: html }}
    />
  )
}
