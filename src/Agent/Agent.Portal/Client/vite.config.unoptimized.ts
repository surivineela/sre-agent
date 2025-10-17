import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

console.log('Building Agent Portal client in unoptimized mode');

export default defineConfig({
    base: '/',
    mode: 'development',
    plugins: [react(), mkcert()],
    define: {
        'import.meta.env.BASE_ROUTE': JSON.stringify('/'),
    },
    build: {
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
