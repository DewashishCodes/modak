import { useEffect, useRef, useState } from 'react'
import { chime } from './chime.js'

export const FOCUS_SECONDS = 25 * 60

// Lives in App, not in the tile: the dashboard unmounts on mouse leave and the timer must keep running.
export function useFocus(sound) {
  const [left, setLeft] = useState(FOCUS_SECONDS)
  const [running, setRunning] = useState(false)
  const end = useRef(0)
  const soundRef = useRef(sound)
  soundRef.current = sound

  useEffect(() => {
    if (!running) return
    const tick = () => {
      const s = Math.max(0, Math.ceil((end.current - Date.now()) / 1000))
      setLeft(s)
      if (!s) { setRunning(false); if (soundRef.current) chime() }
    }
    tick()
    const t = setInterval(tick, 250)
    return () => clearInterval(t)
  }, [running])

  const toggle = () => {
    if (running) { setRunning(false); return }
    const from = left || FOCUS_SECONDS
    setLeft(from)
    end.current = Date.now() + from * 1000
    setRunning(true)
  }
  const reset = () => { setRunning(false); setLeft(FOCUS_SECONDS) }

  return { left, running, toggle, reset, progress: 1 - left / FOCUS_SECONDS }
}

export const mmss = (s) => `${String(Math.floor(s / 60)).padStart(2, '0')}:${String(s % 60).padStart(2, '0')}`
