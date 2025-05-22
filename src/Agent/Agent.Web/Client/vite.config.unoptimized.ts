import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

console.log('Building unoptimized');
export default defineConfig({
    base: '/static',
    plugins: [react(), mkcert()],
    build: {
        outDir: '../wwwroot/static',
        emptyOutDir: true,
        minify: false,
        sourcemap: true,
    },
    publicDir: './src/assets',
    server: {
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
