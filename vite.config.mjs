import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { spawn } from 'node:child_process'

// Starts Electron once the dev server is up, so `npm run dev` is the only command needed.
const electronDev = () => ({
  name: 'electron-dev',
  configureServer(server) {
    server.httpServer?.once('listening', async () => {
      const { default: electron } = await import('electron') // only dev needs the binary
      const url = server.resolvedUrls.local[0]
      const child = spawn(electron, ['.'], { stdio: 'inherit', env: { ...process.env, VITE_DEV_URL: url } })
      child.on('close', () => process.exit())
    })
  },
})

export default defineConfig({
  base: './',
  plugins: [react(), electronDev()],
  server: { port: 5280, strictPort: true },
})
