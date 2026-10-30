import { useEffect, useState } from 'react'
import { motion, AnimatePresence } from 'motion/react'

const spring = { type: 'spring', stiffness: 400, damping: 30 }

// shortcut: spike-only fps readout so the user can judge smoothness without DevTools, remove in M1
function useFps() {
  const [fps, setFps] = useState(0)
  useEffect(() => {
    let frames = 0, last = performance.now(), id
    const tick = (now) => {
      frames++
      if (now - last >= 500) { setFps(Math.round((frames * 1000) / (now - last))); frames = 0; last = now }
      id = requestAnimationFrame(tick)
    }
    id = requestAnimationFrame(tick)
    return () => cancelAnimationFrame(id)
  }, [])
  return fps
}

function Blob() {
  return (
    <div className="blob">
      <div className="eye" />
      <div className="eye" />
    </div>
  )
}

export default function App() {
  const [open, setOpen] = useState(false)
  const fps = useFps()

  const enter = () => { window.peninsula?.setHover(true); setOpen(true) }
  const leave = () => { window.peninsula?.setHover(false); setOpen(false) }

  return (
    <motion.div
      layout
      transition={spring}
      className="pill"
      style={{ width: open ? 440 : 220, borderBottomLeftRadius: 22, borderBottomRightRadius: 22 }}
      onMouseEnter={enter}
      onMouseLeave={leave}
    >
      <motion.div layout="position" transition={spring} className="row">
        <Blob />
        <span className="title">Peninsula</span>
        <span className="sub">idle</span>
      </motion.div>
      <AnimatePresence>
        {open && (
          <motion.div
            className="body"
            initial={{ opacity: 0, y: -6 }}
            animate={{ opacity: 1, y: 0 }}
            exit={{ opacity: 0, transition: { duration: 0.08 } }}
            transition={spring}
          >
            Hover spike. Move the mouse off the pill and it should shrink back with a spring.
            Clicks outside the pill go to the apps underneath.
            <div className="sub" style={{ marginTop: 8 }}>{fps} fps</div>
          </motion.div>
        )}
      </AnimatePresence>
    </motion.div>
  )
}
