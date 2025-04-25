import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';

export default defineConfig({
    base: '/static',
    plugins: [react()],
    build: {
        outDir: '../wwwroot/static',
        emptyOutDir: true,
    },
    publicDir: './src/assets',
    server: {
        watch: {
            usePolling: true,
        },
    },
});
