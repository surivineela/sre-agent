import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react-swc'

console.log('Building unoptimized');
export default defineConfig({
  base: '/',
  plugins: [react()],
  build: {
    outDir: '../wwwroot/',
    emptyOutDir: true,
    minify: false,
    sourcemap: true,
  },
  publicDir: './src/assets',
  server: {
    watch: {
      usePolling: true,
    }
  }
});
