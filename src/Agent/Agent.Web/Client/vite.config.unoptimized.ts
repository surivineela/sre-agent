import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

console.log('Building unoptimized');
export default defineConfig({
    // Don't need base path logic here - prod config produces same thing if local dev
    base: '/static',
    mode: 'development',
    plugins: [react(), mkcert()],
    define: {
        'import.meta.env.BASE_ROUTE': JSON.stringify('/static/'),
    },
    build: {
        outDir: '../wwwroot/static',
        emptyOutDir: true,
        minify: false,
        sourcemap: true,
    },
    publicDir: './src/assets',
    // https://vite.dev/config/server-options.html
    server: {
        cors: { origin: /^https?:\/\/(?:(?:[^:]+\.)?localhost|127\.0\.0\.1|\[::1\]|(?:[^:]+\.)?portal\.azure\.net)(?::\d+)?$/ },
        proxy: {
            '/api': {
                target: 'https://localhost:7023', // or your ASP.NET Core HTTPS port
                changeOrigin: true,
                secure: false, // <== THIS disables certificate verification
            },
        },
        watch: {
            usePolling: true,
        },
    },
});
