import { describe, expect, it } from 'vitest';
import { getPercentChangeInArray } from '../Math';

describe('Math utils', () => {
    describe('getPercentChangeInArray', () => {
        it('should return 0 for arrays with less than 2 elements', () => {
            expect(getPercentChangeInArray([], 'value')).toBe(0);
            expect(getPercentChangeInArray([{ value: 10 }], 'value')).toBe(0);
        });

        it('should return 0 if the first or last value is undefined or not a number', () => {
            expect(getPercentChangeInArray([{ value: 10 }, { value: undefined }], 'value')).toBe(0);
            expect(getPercentChangeInArray([{ value: 'a' }, { value: 20 }], 'value')).toBe(0);
        });

        it('should calculate percent change correctly', () => {
            expect(getPercentChangeInArray([{ value: 10 }, { value: 20 }], 'value')).toBe(100);
            expect(getPercentChangeInArray([{ value: 20 }, { value: 10 }], 'value')).toBe(-50);
            expect(getPercentChangeInArray([{ value: 0 }, { value: 10 }], 'value')).toBe(1000); // Edge case: first value is 0
            expect(getPercentChangeInArray([{ value: 10 }, { value: 10 }], 'value')).toBe(0);
            expect(getPercentChangeInArray([{ value: 0 }, { value: 1 }], 'value')).toBe(100);
            expect(getPercentChangeInArray([{ value: 50 }, { value: 63 }], 'value')).toBe(26);
        });
    });
});
