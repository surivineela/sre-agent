import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc'

export default defineConfig({
  base: '/react/',
  plugins: [react()],
  build: {
    outDir: '../wwwroot/react',
    emptyOutDir: true,
  },
  publicDir: './src/assets',
  server: {
    watch: {
      usePolling: true,
    }
  }
});