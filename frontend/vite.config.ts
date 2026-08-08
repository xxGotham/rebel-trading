import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    port: 5173,
    // The frontend never talks to a broker or a data vendor directly — every
    // call goes through the backend, which owns validation and risk controls.
    proxy: {
      '/api': { target: 'http://localhost:5199', changeOrigin: true },
      '/health': { target: 'http://localhost:5199', changeOrigin: true },
    },
  },
})
