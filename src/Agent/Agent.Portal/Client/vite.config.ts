import react from '@vitejs/plugin-react-swc';
import * as fs from 'fs';
import * as path from 'path';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

const VERSION = process.env.SRE_AGENT_PORTAL_VERSION || 'latest';

export default defineConfig(({ mode }) => {
    const isProduction = mode === 'production' || mode === undefined;

    if (!isProduction) {
        console.log('Building Agent Portal client in development mode (unoptimized)');
    }

    return {
        base: `/${VERSION}/`,
        plugins: [
            react(),
            // Add mkcert for local HTTPS in development
            ...(!isProduction ? [mkcert()] : []),
            {
                name: 'move-index-to-root',
                closeBundle() {
                    // Move index.html from dist/VERSION/ to dist/
                    const versionIndexPath = path.resolve(__dirname, 'dist', VERSION, 'index.html');
                    const rootIndexPath = path.resolve(__dirname, 'dist', 'index.html');

                    if (fs.existsSync(versionIndexPath)) {
                        fs.copyFileSync(versionIndexPath, rootIndexPath);
                        console.log(`Copied index.html to dist/ (references ${VERSION}/ assets)`);
                    }
                },
            },
        ],
        define: {
            'import.meta.env.SRE_AGENT_PORTAL_VERSION': JSON.stringify(VERSION),
            'import.meta.env.BASE_ROUTE': JSON.stringify(`/${VERSION}/`),
        },
        build: {
            outDir: `dist/${VERSION}`,
            // Disable minification and enable sourcemaps in development for easier debugging
            minify: isProduction,
            sourcemap: !isProduction,
        },
        publicDir: './src/assets',
        server: {
            cors: !isProduction
                ? { origin: /^https?:\/\/(?:(?:[^:]+\.)?localhost|127\.0\.0\.1|\[::1\]|(?:[^:]+\.)?portal\.azure\.net)(?::\d+)?$/ }
                : undefined,
            proxy: !isProduction
                ? {
                      '/api': {
                          target: 'https://localhost:7108',
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
