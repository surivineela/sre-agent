import react from '@vitejs/plugin-react-swc';
import * as fs from 'fs';
import * as path from 'path';
import { defineConfig } from 'vite';
import mkcert from 'vite-plugin-mkcert';

const VERSION = process.env.SRE_AGENT_PORTAL_VERSION || '';

export default defineConfig(({ mode }) => {
    const isProduction = mode === 'production' || mode === undefined;

    if (!isProduction) {
        console.log('Building Agent Portal client in development mode (unoptimized)');
    }

    // Use / as base if no version is specified, otherwise /<version>/
    const baseRoute = VERSION ? `/${VERSION}/` : '/';

    return {
        base: baseRoute,
        plugins: [
            react(),
            // Add mkcert for local HTTPS in development
            ...(!isProduction ? [mkcert()] : []),
            {
                name: 'move-index-to-root',
                closeBundle() {
                    // Only move index.html if we're using a versioned path
                    if (!VERSION) {
                        return;
                    }

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
            'import.meta.env.SRE_AGENT_PORTAL_VERSION': JSON.stringify(VERSION || undefined),
            'import.meta.env.BASE_ROUTE': JSON.stringify(baseRoute),
        },
        build: {
            outDir: VERSION ? `dist/${VERSION}` : 'dist',
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
                ? (() => {
                      // Shared proxy configuration for backend endpoints
                      const proxyConfig = {
                          target: 'https://localhost:7108',
                          changeOrigin: true,
                          secure: false,
                          configure: (proxy: any, _options: any) => {
                              proxy.on('proxyReq', (proxyReq: any, req: any, _res: any) => {
                                  // Add forwarded headers so backend knows the original host
                                  proxyReq.setHeader('X-Forwarded-Host', 'localhost:5173');
                                  proxyReq.setHeader('X-Forwarded-Proto', 'https');
                                  proxyReq.setHeader('X-Forwarded-For', req.socket.remoteAddress || '');
                              });
                          },
                      };

                      // Apply same config to all backend routes
                      return {
                          '/api': proxyConfig,
                          '/signin-oidc': proxyConfig,
                          '/signout-callback-oidc': proxyConfig,
                      };
                  })()
                : undefined,
            watch: {
                usePolling: true,
            },
        },
    };
});
