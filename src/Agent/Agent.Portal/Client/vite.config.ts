import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';

const VERSION = process.env.SRE_AGENT_PORTAL_VERSION;

export default defineConfig({
    base: '/',
    plugins: [react()],
    define: {
        'import.meta.env.SRE_AGENT_PORTAL_VERSION': JSON.stringify(VERSION || ''),
        'import.meta.env.BASE_ROUTE': JSON.stringify('/'),
    },
    publicDir: './src/assets',
    server: {
        watch: {
            usePolling: true,
        },
    },
});
