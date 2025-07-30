import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';

const VERSION = process.env.SRE_UX_VERSION;

export default defineConfig({
    base: '/static',
    plugins: [react()],
    define: {
        'import.meta.env.SRE_UX_VERSION': JSON.stringify(VERSION || ''),
        'import.meta.env.BASE_ROUTE': JSON.stringify('/static/'),
    },
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
