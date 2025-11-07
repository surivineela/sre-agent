import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { describe, expect, it } from 'vitest';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

function scanForGeneralLodashImports(): string[] {
    // Scan all TypeScript/JavaScript files for imports from 'lodash' instead of specific paths like 'lodash/debounce'
    const projectRoot = path.resolve(__dirname, '../../'); // points to src
    const offenders: string[] = [];
    const ignoreDirs = new Set(['node_modules', '.git', '__test__', 'dist', 'build', 'out']);

    // Match: import ... from 'lodash' or import ... from "lodash"
    // But NOT: import ... from 'lodash/something'
    const generalLodashImportRegex = /import\s+.*\s+from\s+['"]lodash['"]/g;
    const specificLodashImportRegex = /import\s+.*\s+from\s+['"]lodash\//;

    function walk(dir: string) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        for (const entry of entries) {
            if (ignoreDirs.has(entry.name)) continue;
            const full = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                walk(full);
            } else if (entry.isFile() && /\.(ts|tsx|js|jsx)$/.test(entry.name)) {
                const content = fs.readFileSync(full, 'utf8');
                const lines = content.split('\n');

                for (let i = 0; i < lines.length; i++) {
                    const line = lines[i];
                    const match = generalLodashImportRegex.exec(line);
                    if (match) {
                        // Make sure it's not a specific import
                        if (!specificLodashImportRegex.test(line)) {
                            offenders.push(`${full}:${i + 1}: ${line.trim()}`);
                        }
                    }
                    // Reset regex lastIndex for next iteration
                    generalLodashImportRegex.lastIndex = 0;
                }
            }
        }
    }

    walk(projectRoot);
    return offenders;
}

describe('Lodash import enforcement', () => {
    it('all lodash imports must be specific (e.g., lodash/debounce, not just lodash)', () => {
        const offenders = scanForGeneralLodashImports();
        expect(
            offenders,
            `Found general lodash imports that should be specific to reduce bundle size.\nUse "import debounce from 'lodash/debounce'" instead of "import { debounce } from 'lodash'":\n${offenders.join('\n')}`
        ).toHaveLength(0);
    });
});
