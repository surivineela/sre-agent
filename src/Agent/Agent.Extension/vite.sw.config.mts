/*
Copyright (c) Microsoft Corporation. All rights reserved.
*/

import { resolve } from 'path';
import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    lib: {
      entry: resolve(__dirname, 'src/background.ts'),
      fileName: 'lib/background',
      formats: ['es']
    },
    outDir: 'dist',
    emptyOutDir: false, // Must be false - runs after vite.config.mts which clears the dir
    minify: false
  }
});
