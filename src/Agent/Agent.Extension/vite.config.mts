/*
Copyright (c) Microsoft Corporation. All rights reserved.
*/

import { resolve } from 'path';
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { viteStaticCopy } from 'vite-plugin-static-copy';

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [
    react(),
    viteStaticCopy({
      targets: [
        {
          src: '../../icons/*',
          dest: 'icons'
        },
        {
          src: '../../manifest.json',
          dest: '.'
        }
      ]
    })
  ],
  root: resolve(__dirname, 'src/ui'),
  build: {
    outDir: resolve(__dirname, 'dist/'),
    emptyOutDir: true,
    minify: false,
    rollupOptions: {
      input: ['src/ui/connect.html', 'src/ui/status.html'],
      output: {
        manualChunks: undefined,
        entryFileNames: 'lib/ui/[name].js',
        chunkFileNames: 'lib/ui/[name].js',
        assetFileNames: 'lib/ui/[name].[ext]'
      }
    }
  }
});
