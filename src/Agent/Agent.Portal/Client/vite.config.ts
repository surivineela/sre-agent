import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

const VERSION = process.env.SRE_AGENT_PORTAL_VERSION;

export default defineConfig(({ mode }) => {
    const isProduction = mode === 'production' || mode === undefined;

    if (!isProduction) {
        console.log('Building Agent Portal client in development mode (unoptimized)');
    }

    return {
        base: '/',
        plugins: [
            react(),
            // Add mkcert for local HTTPS in development
            ...(!isProduction ? [mkcert()] : []),
        ],
        define: {
            'import.meta.env.SRE_AGENT_PORTAL_VERSION': JSON.stringify(VERSION || ''),
            'import.meta.env.BASE_ROUTE': JSON.stringify('/'),
        },
        build: {
            // Disable minification and enable sourcemaps in development for easier debugging
            minify: isProduction,
            sourcemap: !isProduction,
        },
        publicDir: './src/assets',
        server: {
            watch: {
                usePolling: true,
            },
        },
    };
});
