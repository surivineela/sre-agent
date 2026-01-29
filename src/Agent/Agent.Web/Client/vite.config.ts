import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

const VERSION = process.env.SRE_UX_VERSION;

export default defineConfig(({ mode }) => {
    const isDevelopment = mode === 'development';

    if (isDevelopment) {
        console.log('Building unoptimized');
    }

    return {
        base: '/static',
        mode,
        plugins: isDevelopment ? [react(), mkcert()] : [react()],
        define: {
            'import.meta.env.SRE_UX_VERSION': JSON.stringify(VERSION || ''),
            'import.meta.env.BASE_ROUTE': JSON.stringify('/static/'),
        },
        build: {
            outDir: '../wwwroot/static',
            emptyOutDir: true,
            minify: isDevelopment ? false : true,
            sourcemap: isDevelopment ? true : false,
        },
        publicDir: './src/assets',
        server: {
            // Allow local-dev agent site UX to be hosted within: localhost (SREA Portal), Azure Portal, prod SREA Portal
            cors: isDevelopment
                ? { origin: /^https?:\/\/(?:(?:[^:]+\.)?localhost|127\.0\.0\.1|\[::1\]|(?:[^:]+\.)?portal\.azure\.net|(?:[^:]+\.)?sre\.azure\.com)(?::\d+)?$/ }
                : undefined,
            proxy: isDevelopment
                ? {
                      '/api': {
                          target: 'https://localhost:7023',
                          changeOrigin: true,
                          secure: false,
                      },
                  }
                : undefined,
            watch: {
                usePolling: true,
            },
        },
    };
});
