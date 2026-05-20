import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'

export default defineConfig({
  plugins: [vue(), vueDevTools()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5098',
        changeOrigin: true,
      },
    },
  },
})
