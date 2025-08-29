import { describe, expect, it } from 'vitest';
import { AntUxStringComparison, equals } from '../Strings';

describe('Strings', () => {
    describe('equals', () => {
        it('returns true only for exact match by default', () => {
            expect(equals('abc', 'abc')).toBe(true);
            expect(equals('abc', 'Abc')).toBe(false);
        });

        it('supports case-insensitive comparison', () => {
            expect(equals('TeSt', 'test', AntUxStringComparison.IgnoreCase)).toBe(true);
            expect(equals('Ä', 'ä', AntUxStringComparison.IgnoreCase)).toBe(true);
        });

        it('returns false for non-string inputs', () => {
            expect(equals('a', null as any)).toBe(false);
            expect(equals(undefined as any, 'a')).toBe(false);
            expect(equals(1 as any, 1 as any)).toBe(false);
        });
    });
});
