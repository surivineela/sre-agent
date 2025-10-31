import { describe, expect, it } from 'vitest';
import { format } from '../String';

describe('String utilities', () => {
    describe('format', () => {
        it('replaces single placeholder with string', () => {
            expect(format('Hello {0}', 'World')).toBe('Hello World');
        });

        it('replaces single placeholder with number', () => {
            expect(format('You have {0} messages', 5)).toBe('You have 5 messages');
        });

        it('replaces multiple placeholders in order', () => {
            expect(format('{0} {1} {2}', 'a', 'b', 'c')).toBe('a b c');
        });

        it('handles mixed string and number arguments', () => {
            expect(format('Hello {0}, you have {1} messages', 'John', 5)).toBe('Hello John, you have 5 messages');
        });

        it('reuses same placeholder index', () => {
            expect(format('{0} and {0}', 'test')).toBe('test and test');
        });

        it('preserves placeholder when argument is undefined', () => {
            expect(format('Hello {0} {1}', 'World')).toBe('Hello World {1}');
        });

        it('handles non-sequential placeholder indices', () => {
            expect(format('{1} {0}', 'second', 'first')).toBe('first second');
        });

        it('returns unchanged string with no placeholders', () => {
            expect(format('No placeholders here', 'unused')).toBe('No placeholders here');
        });

        it('handles empty string template', () => {
            expect(format('', 'arg')).toBe('');
        });

        it('handles template with no arguments', () => {
            expect(format('Static text')).toBe('Static text');
        });

        it('converts number 0 correctly', () => {
            expect(format('Value: {0}', 0)).toBe('Value: 0');
        });

        it('handles placeholders in complex strings', () => {
            expect(format('/subscriptions/{0}/locations?api-version={1}', 'sub-123', '2020-01-01')).toBe(
                '/subscriptions/sub-123/locations?api-version=2020-01-01'
            );
        });
    });
});
