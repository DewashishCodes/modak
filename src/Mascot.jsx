import { useEffect, useRef, useState } from 'react'
import { motion, useAnimationControls } from 'motion/react'

const POKES_TO_DIZZY = 5
const POKE_WINDOW_MS = 2500
const INK = '#2a1d24'

// Eyes (and mouth) per expression, drawn in a 40x32 box. Left eye centered at x=14, right at x=26, y=17.
const faces = {
  idle: () => (
    <g className="blink">
      <rect x="12.6" y="14.5" width="2.8" height="5" rx="1.4" fill={INK} />
      <rect x="24.6" y="14.5" width="2.8" height="5" rx="1.4" fill={INK} />
    </g>
  ),
  working: () => (
    <g className="scan">
      <rect x="12" y="16" width="4" height="2.6" rx="1.3" fill={INK} />
      <rect x="24" y="16" width="4" height="2.6" rx="1.3" fill={INK} />
      <path d="M11.5 13.6l4.5 1M28.5 13.6l-4.5 1" stroke={INK} strokeWidth="1.2" strokeLinecap="round" />
    </g>
  ),
  asking: () => (
    <g>
      <circle cx="14" cy="17" r="3.3" fill={INK} />
      <circle cx="26" cy="17" r="3.3" fill={INK} />
      <circle cx="15.1" cy="15.8" r="1.1" fill="#fff" />
      <circle cx="27.1" cy="15.8" r="1.1" fill="#fff" />
      <circle cx="20" cy="23.4" r="1.2" fill={INK} />
    </g>
  ),
  done: () => (
    <g>
      <path d="M11 18.5q3-4 6 0M23 18.5q3-4 6 0" stroke={INK} strokeWidth="1.8" fill="none" strokeLinecap="round" />
      <path d="M17.6 22.2q2.4 2.4 4.8 0" stroke={INK} strokeWidth="1.5" fill="none" strokeLinecap="round" />
    </g>
  ),
  love: () => (
    <g fill="#e0457b">
      <path d="M14 20.6l-3-3a1.8 1.8 0 013-2 1.8 1.8 0 013 2z" />
      <path d="M26 20.6l-3-3a1.8 1.8 0 013-2 1.8 1.8 0 013 2z" />
    </g>
  ),
  wink: () => (
    <g>
      <path d="M11.4 17.4h5.2" stroke={INK} strokeWidth="1.8" strokeLinecap="round" />
      <rect x="24.6" y="14.5" width="2.8" height="5" rx="1.4" fill={INK} />
      <path d="M17.6 22q2.4 2 4.8 0" stroke={INK} strokeWidth="1.4" fill="none" strokeLinecap="round" />
    </g>
  ),
  dizzy: () => (
    <g className="spin-eyes" stroke={INK} strokeWidth="1.3" fill="none" strokeLinecap="round">
      <path className="spin" d="M14 17m-2.8 0a2.8 2.8 0 105.6 0 1.8 1.8 0 10-3.6 0 .8 .8 0 101.6 0" />
      <path className="spin" d="M26 17m-2.8 0a2.8 2.8 0 105.6 0 1.8 1.8 0 10-3.6 0 .8 .8 0 101.6 0" />
      <path d="M17.5 23.5q1.2-1.2 2.5 0t2.5 0" />
    </g>
  ),
  sleepy: () => (
    <g>
      <path d="M11.5 18q2.5 1.6 5 0M23.5 18q2.5 1.6 5 0" stroke={INK} strokeWidth="1.5" fill="none" strokeLinecap="round" />
      <text x="31" y="9" fontSize="6" fontWeight="700" fill="#b9a7b0" className="zzz">z</text>
    </g>
  ),
}

const BLUSH = new Set(['done', 'love', 'wink'])
const SLEEP_AFTER_MS = 2 * 60 * 1000

// Mochi blob. mood: idle | working | asking | done; it adds love, wink, dizzy and sleepy on its own.
export default function Mascot({ mood = 'idle', size = 26, loveSignal = 0 }) {
  const [override, setOverride] = useState(null)
  const [sleepy, setSleepy] = useState(false)
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
    squish.start({ scaleX: [1, 1.22, 0.92, 1], scaleY: [1, 0.78, 1.08, 1], transition: { duration: 0.38 } })
    setSleepy(false)
    const now = Date.now()
    pokes.current = [...pokes.current.filter((t) => now - t < POKE_WINDOW_MS), now]
    if (pokes.current.length >= POKES_TO_DIZZY) {
      pokes.current = []
      flash('dizzy', 2400)
    } else if (pokes.current.length === 1) flash('wink', 700)
  }

  const face = override || (mood === 'idle' && sleepy ? 'sleepy' : mood)
  const Face = faces[face] || faces.idle

  return (
    <motion.svg
      className={`mascot face-${face}`}
      width={size}
      height={size * 0.8}
      viewBox="0 0 40 32"
      animate={squish}
      onClick={poke}
      style={{ originY: 1 }}
    >
      <defs>
        <radialGradient id="mochi" cx="40%" cy="30%" r="75%">
          <stop offset="0" stopColor="#fff2f7" />
          <stop offset="0.6" stopColor="#f6d6e3" />
          <stop offset="1" stopColor="#e6b6ca" />
        </radialGradient>
      </defs>
      <path d="M3 22.5C3 10.5 11 3 20 3s17 7.5 17 19.5C37 28.5 31 30 20 30S3 28.5 3 22.5z" fill="url(#mochi)" />
      <ellipse cx="14" cy="8.5" rx="4" ry="1.6" fill="#fff" opacity="0.55" />
      {BLUSH.has(face) && (
        <g fill="#f08aa9" opacity="0.6">
          <ellipse cx="9.5" cy="22" rx="2.6" ry="1.5" />
          <ellipse cx="30.5" cy="22" rx="2.6" ry="1.5" />
        </g>
      )}
      <Face />
    </motion.svg>
  )
}
