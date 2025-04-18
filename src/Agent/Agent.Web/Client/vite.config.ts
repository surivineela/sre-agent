import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc'

export default defineConfig({
  base: '/',
  plugins: [react()],
  build: {
    outDir: '../wwwroot/',
    emptyOutDir: true,
  },
  publicDir: './src/assets',
  server: {
    watch: {
      usePolling: true,
    }
  }
});
