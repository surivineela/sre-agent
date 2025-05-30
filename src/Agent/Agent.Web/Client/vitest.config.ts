import react from '@vitejs/plugin-react-swc';
import { defineConfig } from 'vitest/config';

export default defineConfig({
    plugins: [react()],
    test: {
        reporters: ['default', 'junit'],
        outputFile: {
            junit: './junit-report.xml',
        },
        environment: 'jsdom',
        globals: true,
    },
});
