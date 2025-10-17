import { describe, expect, it } from 'vitest';
import { addPathToHostname } from '../Url';

describe('Url utilities', () => {
    describe('addPathToHostname', () => {
        it('combines origin and relative path', () => {
            const origin = 'https://contoso.com/';
            const result = addPathToHostname(origin, '/agents/123');
            expect(result).toBe('https://contoso.com/agents/123');
        });

        it('handles origin without trailing slash', () => {
            const origin = 'https://contoso.com';
            const result = addPathToHostname(origin, 'agents/123');
            expect(result).toBe('https://contoso.com/agents/123');
        });

        it('preserves query string and hash in path', () => {
            const origin = 'https://contoso.com/';
            const result = addPathToHostname(origin, '/agents/123?foo=bar#section');
            expect(result).toBe('https://contoso.com/agents/123?foo=bar#section');
        });
    });
});
