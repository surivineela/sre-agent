import { createHash } from 'crypto';
import fs from 'fs';
import path from 'path';
import { fileURLToPath } from 'url';
import { describe, expect, it } from 'vitest';

// Dynamically import all exported message groups from the resources file so the test
// won't need updating when new groups are added.
// eslint-disable-next-line @typescript-eslint/ban-ts-comment
// @ts-ignore - ESM interop
import * as ResourcesModule from '../Resources';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

interface MessageDescriptorInfo {
    id: string;
    defaultMessage: string;
    group: string; // export name
    key: string; // property name inside group
}

function computeExpectedId(defaultMessage: string): string {
    // Hash rule: first 6 chars of base64(SHA512(message)) with padding removed
    return createHash('sha512').update(defaultMessage).digest('base64').replace(/=+$/, '').substring(0, 6);
}

function collectAllMessageDescriptors(): MessageDescriptorInfo[] {
    const descriptors: MessageDescriptorInfo[] = [];
    for (const [exportName, value] of Object.entries(ResourcesModule)) {
        if (value && typeof value === 'object') {
            const entries = Object.entries(value as Record<string, any>);
            // Heuristic: treat as a messages group if every property value that is an object has id + defaultMessage strings
            const looksLikeMessages =
                entries.length > 0 &&
                entries.every(([, v]) => v && typeof v === 'object' && typeof v.id === 'string' && typeof v.defaultMessage === 'string');
            if (looksLikeMessages) {
                for (const [k, v] of entries) {
                    descriptors.push({ id: v.id, defaultMessage: v.defaultMessage, group: exportName, key: k });
                }
            }
        }
    }
    return descriptors;
}

function scanForRawAriaLabels(): string[] {
    // Recursively scan .tsx files for aria-label="..." literal strings (not expressions)
    const projectRoot = path.resolve(__dirname, '../../'); // points to src
    const offenders: string[] = [];
    const ignoreDirs = new Set(['node_modules', '.git', '__test__', 'Strings']);

    function walk(dir: string) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        for (const entry of entries) {
            if (ignoreDirs.has(entry.name)) continue;
            const full = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                walk(full);
            } else if (entry.isFile() && /\.tsx$/.test(entry.name)) {
                const content = fs.readFileSync(full, 'utf8');
                // Skip files that intentionally opt out
                if (content.includes('data-i18n-ignore')) continue;
                const regex = /aria-label\s*=\s*"([^"]+)"/g; // aria-label="..."
                let match: RegExpExecArray | null;
                while ((match = regex.exec(content)) !== null) {
                    const label = match[1];
                    if (/^[\s\-–—]*$/.test(label)) continue; // ignore punctuation/whitespace
                    if (/^\d+$/.test(label)) continue; // purely numeric
                    if (/[A-Za-z]/.test(label)) {
                        offenders.push(`${full}: aria-label="${label}"`);
                    }
                }
            }
        }
    }

    walk(projectRoot);
    return offenders;
}

function scanForRawTitleAttributes(): string[] {
    // Similar scan for title="..." or title='...' literal values.
    const projectRoot = path.resolve(__dirname, '../../../');
    const offenders: string[] = [];
    const ignoreDirs = new Set(['node_modules', '.git', '__test__', 'Strings']);
    const regexes = [
        /title\s*=\s*"([^"{]+)"/g, // double quoted without { expression
        /title\s*=\s*'([^'{]+)'/g, // single quoted
    ];
    function walk(dir: string) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        for (const entry of entries) {
            if (ignoreDirs.has(entry.name)) continue;
            const full = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                walk(full);
            } else if (entry.isFile() && /\.tsx$/.test(entry.name)) {
                const content = fs.readFileSync(full, 'utf8');
                if (content.includes('data-i18n-ignore')) continue;
                for (const rx of regexes) {
                    let match: RegExpExecArray | null;
                    while ((match = rx.exec(content)) !== null) {
                        const value = match[1].trim();
                        if (!value) continue;
                        if (/^[\s\-–—]*$/.test(value)) continue;
                        if (/^\d+$/.test(value)) continue;
                        // Ignore simple lowercase technical tokens to reduce false positives in object literals (e.g., title="title")
                        if (/^[a-z0-9_-]+$/.test(value)) continue;
                        if (/[A-Za-z]/.test(value)) {
                            offenders.push(`${full}: title="${value}"`);
                        }
                    }
                }
            }
        }
    }
    walk(projectRoot);
    return offenders;
}

function scanForRawJsxTextNodes(): string[] {
    // Improved heuristic: capture only plain text nodes between tags, excluding:
    //  * lines that contain TypeScript type annotations, generics, or interface declarations
    //  * JSX spread / expressions
    //  * comment blocks and inline comments
    //  * style / animation / inline CSS template blocks
    //  * placeholder shimmer markup (className shimmer-effect)
    //  * obviously code-like tokens containing ':' outside of typical time strings
    const projectRoot = path.resolve(__dirname, '../../../');
    const offenders: string[] = [];
    const ignoreDirs = new Set(['node_modules', '.git', '__test__', 'Strings']);
    // Only consider simple element pairs with direct textual content (no nested tags)
    const simpleTagPairRegex = /<([A-Za-z][A-Za-z0-9]*)[^>]*>([^<>]+)<\/\1>/g;
    function likelyFalsePositive(text: string): boolean {
        if (text.includes('\n')) return true;
        if (/^(interface|type)\s/.test(text)) return true;
        if (/^[([{]/.test(text)) return true;
        if (/^[/#]/.test(text)) return true; // paths or comments
        if (text.startsWith('= ')) return true; // assignment fragments
        if (/\bextends\b|\bimplements\b/.test(text)) return true;
        if (/\b(useState|useRef|useEffect|return|const|let|function|new Set|new Map)\b/.test(text)) return true;
        if (/=>/.test(text)) return true;
        if (/[:;{}]/.test(text) && !/[ ]/.test(text)) return true;
        if (/\b[A-Za-z0-9_]+\s*[|&]\s*[A-Za-z0-9_]+/.test(text)) return true;
        if (/\b[A-Za-z0-9_]+\s*\?\s*[A-Za-z0-9_]+\s*:\s*[A-Za-z0-9_]+/.test(text)) return true;
        if (/^[0-9\s\-–—_:.,;!?()*]+$/.test(text)) return true;
        if (/^(Promise|Record|Set|Map|string|number|boolean)$/i.test(text)) return true;
        if (text.startsWith('/sreLink/')) return true;
        if (text.length < 3) return true;
        return false;
    }
    function walk(dir: string) {
        const entries = fs.readdirSync(dir, { withFileTypes: true });
        for (const entry of entries) {
            if (ignoreDirs.has(entry.name)) continue;
            const full = path.join(dir, entry.name);
            if (entry.isDirectory()) {
                walk(full);
            } else if (entry.isFile() && /\.tsx$/.test(entry.name)) {
                const content = fs.readFileSync(full, 'utf8');
                if (content.includes('data-i18n-ignore')) continue;
                if (/Strings\//.test(full)) continue;
                let match: RegExpExecArray | null;
                while ((match = simpleTagPairRegex.exec(content)) !== null) {
                    const inner = match[2].trim();
                    if (!inner) continue;
                    if (/{.*}/.test(inner)) continue; // skip dynamic expressions
                    if (likelyFalsePositive(inner)) continue;
                    if (/FormattedMessage/.test(inner) || /formatMessage\(/.test(inner)) continue;
                    // If it has multiple words or capitalized word likely user-visible
                    if (/[A-Za-z]/.test(inner)) {
                        offenders.push(`${full}: "${inner}"`);
                    }
                }
            }
        }
    }
    walk(projectRoot);
    return offenders;
}

// MAIN TESTS

// TODO: Un-skip once a little more finalized
describe.skip('i18n enforcement', () => {
    const allDescriptors = collectAllMessageDescriptors();

    it('all message ids match hashing rule (first 6 chars of base64 sha512)', () => {
        const mismatches = allDescriptors.filter(d => computeExpectedId(d.defaultMessage) !== d.id);
        const message = mismatches
            .slice(0, 15)
            .map(m => `${m.group}.${m.key} -> id=${m.id} expected=${computeExpectedId(m.defaultMessage)} `)
            .join('\n');
        expect(mismatches, `Found ${mismatches.length} message id mismatches (showing up to 15):\n${message}`).toHaveLength(0);
    });

    it('duplicate defaultMessages use the same id (no divergent duplicates)', () => {
        const map = new Map<string, Set<string>>();
        for (const d of allDescriptors) {
            if (!map.has(d.defaultMessage)) map.set(d.defaultMessage, new Set());
            map.get(d.defaultMessage)!.add(d.id);
        }
        const bad = Array.from(map.entries()).filter(([, ids]) => ids.size > 1);
        const details = bad.map(([msg, ids]) => `${msg} => [${Array.from(ids).join(', ')}]`).join('\n');
        expect(bad, `Duplicate defaultMessages with different ids:\n${details}`).toHaveLength(0);
    });

    it('no raw aria-label string literals (must use intl.formatMessage)', () => {
        const offenders = scanForRawAriaLabels();
        expect(
            offenders,
            `Found raw aria-label literals that should be localized (wrap with intl.formatMessage or existing resource):\n${offenders.join('\n')}`
        ).toHaveLength(0);
    });

    it('no raw title attribute literals (must use intl.formatMessage)', () => {
        const offenders = scanForRawTitleAttributes();
        expect(
            offenders,
            `Found raw title attribute literals that should be localized (wrap with intl.formatMessage or existing resource):\n${offenders.join('\n')}`
        ).toHaveLength(0);
    });

    it('no raw JSX text node literals (must be localized)', () => {
        const offenders = scanForRawJsxTextNodes();
        expect(
            offenders,
            `Found raw JSX text nodes that appear user-visible and should be localized:\n${offenders.slice(0, 50).join('\n')}${offenders.length > 50 ? `\n...and ${offenders.length - 50} more` : ''}`
        ).toHaveLength(0);
    });
});
