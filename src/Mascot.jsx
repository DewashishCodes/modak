import { useRef, useState } from 'react'
import { motion, useAnimationControls } from 'motion/react'

const POKES_TO_DIZZY = 5
const POKE_WINDOW_MS = 2500

// Mochi blob placeholder until the Rive mascot (M4). mood: idle | working | asking | done
export default function Mascot({ mood = 'idle' }) {
  const [dizzy, setDizzy] = useState(false)
  const pokes = useRef([])
  const squish = useAnimationControls()

  const poke = (e) => {
    e.stopPropagation()
    squish.start({ scaleX: [1, 1.25, 0.9, 1], scaleY: [1, 0.75, 1.1, 1], transition: { duration: 0.35 } })
    const now = Date.now()
    pokes.current = [...pokes.current.filter((t) => now - t < POKE_WINDOW_MS), now]
    if (pokes.current.length >= POKES_TO_DIZZY) {
      pokes.current = []
      setDizzy(true)
      setTimeout(() => setDizzy(false), 2200)
    }
  }

  return (
    <motion.div className={`blob mood-${dizzy ? 'dizzy' : mood}`} animate={squish} onClick={poke} title="Poke me">
      <div className="eye" />
      <div className="eye" />
    </motion.div>
  )
}
