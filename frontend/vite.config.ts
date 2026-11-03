import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { fileURLToPath } from 'node:url'

const legalDir = fileURLToPath(new URL('../docs/legal', import.meta.url))

export default defineConfig({
  plugins: [react(), tailwindcss()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      // юр. документы рендерятся из docs/legal (единый источник)
      '@legal': legalDir,
    },
  },
  server: {
    port: 5173,
    fs: { allow: ['..'] },
    proxy: {
      '/api': { target: process.env.API_URL ?? 'http://localhost:8080', changeOrigin: false },
    },
  },
  build: {
    // иллюстрации — отдельными файлами (ленивая загрузка), а не base64 в JS
    assetsInlineLimit: 0,
    sourcemap: false,
  },
})
