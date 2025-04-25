import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';

console.log('Building unoptimized');
export default defineConfig({
    base: '/static',
    plugins: [react()],
    build: {
        outDir: '../wwwroot/static',
        emptyOutDir: true,
        minify: false,
        sourcemap: true,
    },
    publicDir: './src/assets',
    server: {
        watch: {
            usePolling: true,
        },
    },
});
