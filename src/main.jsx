import { createRoot } from 'react-dom/client'
import './index.css'

if (import.meta.env.DEV) await import('./mock.js') // must run before App reads window.modak
const { default: App } = await import('./App.jsx')

createRoot(document.getElementById('root')).render(<App />)
