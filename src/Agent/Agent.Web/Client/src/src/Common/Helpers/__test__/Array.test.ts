import { describe, expect, it } from 'vitest';
import { dedupeById } from '../Array';

describe('Array utils', () => {
    describe('dedupeById', () => {
        it('removes duplicate items by id', () => {
            const items = [
                { id: '1', title: 'Doc A' },
                { id: '1', title: 'Doc A duplicate' },
                { id: '2', title: 'Doc B' },
            ];
            const result = dedupeById(items);
            expect(result).toHaveLength(2);
            expect(result[0].id).toBe('1');
            expect(result[1].id).toBe('2');
        });

        it('keeps first occurrence when duplicates exist', () => {
            const items = [
                { id: '1', title: 'First' },
                { id: '1', title: 'Second' },
            ];
            const result = dedupeById(items);
            expect(result[0].title).toBe('First');
        });

        it('returns empty array for empty input', () => {
            expect(dedupeById([])).toEqual([]);
        });

        it('returns same array when no duplicates', () => {
            const items = [
                { id: '1', title: 'A' },
                { id: '2', title: 'B' },
                { id: '3', title: 'C' },
            ];
            const result = dedupeById(items);
            expect(result).toHaveLength(3);
        });
    });
});
