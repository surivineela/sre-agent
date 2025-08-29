import { describe, expect, it } from 'vitest';
import { Guid } from '../Guid';

describe('Guid', () => {
    it('newGuid returns a valid v4 UUID', () => {
        const g = Guid.newGuid();
        const v4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/i;
        expect(v4.test(g)).toBe(true);
        expect(Guid.isValid(g)).toBe(true);
    });

    it('newShortGuid returns 8-4 hex pattern', () => {
        const g = Guid.newShortGuid();
        const pat = /^[0-9a-f]{8}-[0-9a-f]{4}$/i;
        expect(pat.test(g)).toBe(true);
        expect(Guid.isValid(g)).toBe(false); // not a full UUID
    });

    it('newTinyGuid returns a 4-hex variant nibble', () => {
        const g = Guid.newTinyGuid();
        const pat = /^[89ab][0-9a-f]{3}$/i;
        expect(pat.test(g)).toBe(true);
    });

    it('newCustomGuid respects length; 0 => empty string', () => {
        expect(Guid.newCustomGuid(0)).toBe('');
        const g10 = Guid.newCustomGuid(10);
        expect(g10).toMatch(/^[0-9a-f]{10}$/i);
    });

    it('isValid detects correct UUID strings', () => {
        expect(Guid.isValid('not-a-guid')).toBe(false);
        expect(Guid.isValid('00000000-0000-4000-8000-000000000000')).toBe(true);
    });
});
