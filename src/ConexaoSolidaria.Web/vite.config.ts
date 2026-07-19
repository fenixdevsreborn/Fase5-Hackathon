import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const createProxy = (target: string, prefix: string) => ({
  target,
  changeOrigin: true,
  rewrite: (path: string) => path.replace(new RegExp(`^${prefix}`), ''),
})

export default defineConfig({
  plugins: [react()],
  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: {
      '/identity-api': createProxy('http://localhost:30081', '/identity-api'),
      '/campaigns-api': createProxy('http://localhost:30082', '/campaigns-api'),
    },
  },
})
