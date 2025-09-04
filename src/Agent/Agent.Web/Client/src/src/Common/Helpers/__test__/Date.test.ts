import { describe, expect, it } from 'vitest';
import {
    changeToLocalTimezone,
    changeToUtcTimezone,
    extractDateFromDateTime,
    formatDate,
    formatDateToYYYYMMDD,
    formatTimeStringTo12HoursFormat,
    formatTimeStringTo24HoursFormat,
    getCombineDateAndTime,
    getDateObjectFromDateAndTimeInput,
    getHourMinuteSecondFrom24HoursFormatTimeInput,
    getHourMinuteSecondFromTimeInput,
} from '../Date';

describe('Date helpers', () => {
    it('parses 12-hour input correctly', () => {
        expect(getHourMinuteSecondFromTimeInput('12:00:00 AM')).toEqual({ hour: 0, minute: 0, second: 0 });
        expect(getHourMinuteSecondFromTimeInput('12:00:00 PM')).toEqual({ hour: 12, minute: 0, second: 0 });
        expect(getHourMinuteSecondFromTimeInput('1:05:09 PM')).toEqual({ hour: 13, minute: 5, second: 9 });
        expect(getHourMinuteSecondFromTimeInput('bad')).toBeNull();
    });

    it('parses 24-hour input strictly', () => {
        expect(getHourMinuteSecondFrom24HoursFormatTimeInput('09:07:01')).toEqual({ hour: 9, minute: 7, second: 1 });
        expect(getHourMinuteSecondFrom24HoursFormatTimeInput('9:7:1')).toEqual({ hour: 9, minute: 7, second: 1 });
        expect(getHourMinuteSecondFrom24HoursFormatTimeInput('9:undefined:1')).toBeNull();
    });

    it('normalizes time formats', () => {
        expect(formatTimeStringTo12HoursFormat('00:05:05 am')).toBe('12:05:05 AM');
        expect(formatTimeStringTo12HoursFormat('14:05:05 PM')).toBe('2:05:05 PM');
        expect(formatTimeStringTo24HoursFormat('09:05:05')).toBe('9:05:05');
        expect(formatTimeStringTo24HoursFormat('bad')).toBe('');
    });

    it('composes a Date from date and time', () => {
        const d = new Date('2020-01-02T00:00:00Z');
        const local = getDateObjectFromDateAndTimeInput(true, d, '1:02:03 AM');
        expect(local.getHours()).toBe(1);
        expect(local.getMinutes()).toBe(2);
        expect(local.getSeconds()).toBe(3);

        const utc = getDateObjectFromDateAndTimeInput(false, d, '1:02:03 AM');
        expect(utc.toISOString().endsWith('01:02:03.000Z')).toBeTruthy();
    });

    it('formatDate strips millis from ISO', () => {
        expect(formatDate('2020-01-02T03:04:05.678Z')).toBe('2020-01-02T03:04:05Z');
    });
});

describe('TimerangePillFilter date utility functions', () => {
    describe('formatDateToYYYYMMDD', () => {
        it('formats a valid date to YYYY-MM-DD format', () => {
            const date = new Date(2023, 11, 25, 14, 30, 45); // December 25, 2023, 2:30:45 PM
            expect(formatDateToYYYYMMDD(date)).toBe('2023-12-25');
        });

        it('pads single digit months and days with zeros', () => {
            const date = new Date(2023, 0, 5); // January 5, 2023
            expect(formatDateToYYYYMMDD(date)).toBe('2023-01-05');
        });

        it('handles year boundaries correctly', () => {
            const date = new Date(2024, 0, 1); // January 1, 2024
            expect(formatDateToYYYYMMDD(date)).toBe('2024-01-01');
        });

        it('returns empty string for undefined date', () => {
            expect(formatDateToYYYYMMDD(undefined)).toBe('');
        });

        it('handles leap year dates', () => {
            const date = new Date(2024, 1, 29); // February 29, 2024 (leap year)
            expect(formatDateToYYYYMMDD(date)).toBe('2024-02-29');
        });
    });

    describe('extractDateFromDateTime', () => {
        it('extracts date components and sets time to midnight', () => {
            const dateTime = new Date(2023, 11, 25, 14, 30, 45, 123);
            const dateOnly = extractDateFromDateTime(dateTime);

            expect(dateOnly).toBeDefined();
            expect(dateOnly!.getFullYear()).toBe(2023);
            expect(dateOnly!.getMonth()).toBe(11); // December (0-indexed)
            expect(dateOnly!.getDate()).toBe(25);
            expect(dateOnly!.getHours()).toBe(0);
            expect(dateOnly!.getMinutes()).toBe(0);
            expect(dateOnly!.getSeconds()).toBe(0);
            expect(dateOnly!.getMilliseconds()).toBe(0);
        });

        it('preserves date components regardless of original time', () => {
            const lateNight = new Date(2023, 11, 25, 23, 59, 59, 999);
            const dateOnly = extractDateFromDateTime(lateNight);

            expect(dateOnly!.getFullYear()).toBe(2023);
            expect(dateOnly!.getMonth()).toBe(11);
            expect(dateOnly!.getDate()).toBe(25);
            expect(dateOnly!.getHours()).toBe(0);
        });

        it('returns undefined for undefined input', () => {
            expect(extractDateFromDateTime(undefined)).toBeUndefined();
        });

        it('handles year boundaries correctly', () => {
            const yearEnd = new Date(2023, 11, 31, 23, 59, 59);
            const dateOnly = extractDateFromDateTime(yearEnd);

            expect(dateOnly!.getFullYear()).toBe(2023);
            expect(dateOnly!.getMonth()).toBe(11);
            expect(dateOnly!.getDate()).toBe(31);
            expect(dateOnly!.getHours()).toBe(0);
        });

        it('handles leap year dates', () => {
            const leapDay = new Date(2024, 1, 29, 12, 0, 0); // February 29, 2024
            const dateOnly = extractDateFromDateTime(leapDay);

            expect(dateOnly!.getFullYear()).toBe(2024);
            expect(dateOnly!.getMonth()).toBe(1); // February
            expect(dateOnly!.getDate()).toBe(29);
            expect(dateOnly!.getHours()).toBe(0);
        });
    });

    describe('getCombineDateAndTime', () => {
        it('combines date and time components correctly', () => {
            const date = new Date(2023, 11, 25, 0, 0, 0, 0); // December 25, 2023 at midnight
            const time = new Date(2000, 0, 1, 14, 30, 45, 123); // Any date with 2:30:45.123 PM

            const combined = getCombineDateAndTime(date, time);

            expect(combined).toBeDefined();
            expect(combined!.getFullYear()).toBe(2023);
            expect(combined!.getMonth()).toBe(11); // December (0-indexed)
            expect(combined!.getDate()).toBe(25);
            expect(combined!.getHours()).toBe(14);
            expect(combined!.getMinutes()).toBe(30);
            expect(combined!.getSeconds()).toBe(45);
            expect(combined!.getMilliseconds()).toBe(123);
        });

        it('uses date components from first parameter only', () => {
            const date = new Date(2023, 5, 15, 10, 20, 30, 40); // June 15, 2023 with some time
            const time = new Date(2020, 0, 1, 8, 45, 15, 500); // Different date with different time

            const combined = getCombineDateAndTime(date, time);

            // Should use date from first parameter
            expect(combined!.getFullYear()).toBe(2023);
            expect(combined!.getMonth()).toBe(5); // June
            expect(combined!.getDate()).toBe(15);

            // Should use time from second parameter
            expect(combined!.getHours()).toBe(8);
            expect(combined!.getMinutes()).toBe(45);
            expect(combined!.getSeconds()).toBe(15);
            expect(combined!.getMilliseconds()).toBe(500);
        });

        it('uses time components from second parameter only', () => {
            const date = new Date(2024, 1, 29, 23, 59, 59, 999); // Leap day with late time
            const time = new Date(1990, 11, 31, 0, 0, 0, 0); // Different date with midnight

            const combined = getCombineDateAndTime(date, time);

            // Should use date from first parameter
            expect(combined!.getFullYear()).toBe(2024);
            expect(combined!.getMonth()).toBe(1); // February
            expect(combined!.getDate()).toBe(29);

            // Should use time from second parameter
            expect(combined!.getHours()).toBe(0);
            expect(combined!.getMinutes()).toBe(0);
            expect(combined!.getSeconds()).toBe(0);
            expect(combined!.getMilliseconds()).toBe(0);
        });

        it('handles midnight time correctly', () => {
            const date = new Date(2023, 6, 4); // July 4, 2023
            const time = new Date(2000, 0, 1, 0, 0, 0, 0); // Midnight

            const combined = getCombineDateAndTime(date, time);

            expect(combined!.getHours()).toBe(0);
            expect(combined!.getMinutes()).toBe(0);
            expect(combined!.getSeconds()).toBe(0);
            expect(combined!.getMilliseconds()).toBe(0);
        });

        it('handles end of day time correctly', () => {
            const date = new Date(2023, 11, 31); // December 31, 2023
            const time = new Date(2000, 0, 1, 23, 59, 59, 999); // End of day

            const combined = getCombineDateAndTime(date, time);

            expect(combined!.getFullYear()).toBe(2023);
            expect(combined!.getMonth()).toBe(11);
            expect(combined!.getDate()).toBe(31);
            expect(combined!.getHours()).toBe(23);
            expect(combined!.getMinutes()).toBe(59);
            expect(combined!.getSeconds()).toBe(59);
            expect(combined!.getMilliseconds()).toBe(999);
        });

        it('handles leap year dates correctly', () => {
            const date = new Date(2024, 1, 29); // February 29, 2024 (leap year)
            const time = new Date(2000, 0, 1, 12, 0, 0, 0); // Noon

            const combined = getCombineDateAndTime(date, time);

            expect(combined!.getFullYear()).toBe(2024);
            expect(combined!.getMonth()).toBe(1); // February
            expect(combined!.getDate()).toBe(29);
            expect(combined!.getHours()).toBe(12);
        });

        it('returns undefined when date is undefined', () => {
            const time = new Date(2000, 0, 1, 14, 30, 45, 123);

            const combined = getCombineDateAndTime(undefined, time);

            expect(combined).toBeUndefined();
        });

        it('returns undefined when time is undefined', () => {
            const date = new Date(2023, 11, 25);

            const combined = getCombineDateAndTime(date, undefined);

            expect(combined).toBeUndefined();
        });

        it('returns undefined when both date and time are undefined', () => {
            const combined = getCombineDateAndTime(undefined, undefined);

            expect(combined).toBeUndefined();
        });

        it('preserves all millisecond precision', () => {
            const date = new Date(2023, 0, 1);
            const time = new Date(2000, 0, 1, 1, 2, 3, 456);

            const combined = getCombineDateAndTime(date, time);

            expect(combined!.getMilliseconds()).toBe(456);
        });

        it('works with dates from different years', () => {
            const date = new Date(2025, 8, 15); // September 15, 2025
            const time = new Date(1985, 3, 20, 16, 45, 30, 750); // Time from 1985

            const combined = getCombineDateAndTime(date, time);

            expect(combined!.getFullYear()).toBe(2025);
            expect(combined!.getMonth()).toBe(8); // September
            expect(combined!.getDate()).toBe(15);
            expect(combined!.getHours()).toBe(16);
            expect(combined!.getMinutes()).toBe(45);
            expect(combined!.getSeconds()).toBe(30);
            expect(combined!.getMilliseconds()).toBe(750);
        });

        it('creates a new Date object (does not mutate inputs)', () => {
            const originalDate = new Date(2023, 5, 15, 10, 20, 30, 40);
            const originalTime = new Date(2020, 0, 1, 8, 45, 15, 500);
            const originalDateTime = originalDate.getTime();
            const originalTimeTime = originalTime.getTime();

            const combined = getCombineDateAndTime(originalDate, originalTime);

            // Verify inputs weren't mutated
            expect(originalDate.getTime()).toBe(originalDateTime);
            expect(originalTime.getTime()).toBe(originalTimeTime);

            // Verify result is a different object
            expect(combined).not.toBe(originalDate);
            expect(combined).not.toBe(originalTime);
        });

        it('handles dates and times with different timezone interpretations', () => {
            // Both dates will be interpreted in local time
            const date = new Date(2023, 11, 25); // Christmas 2023
            const time = new Date(2000, 0, 1, 15, 30, 0, 0); // 3:30 PM

            const combined = getCombineDateAndTime(date, time);

            // Result should be Christmas 2023 at 3:30 PM local time
            expect(combined!.getFullYear()).toBe(2023);
            expect(combined!.getMonth()).toBe(11);
            expect(combined!.getDate()).toBe(25);
            expect(combined!.getHours()).toBe(15);
            expect(combined!.getMinutes()).toBe(30);
            expect(combined!.getSeconds()).toBe(0);
            expect(combined!.getMilliseconds()).toBe(0);
        });
    });

    describe('changeToLocalTimezone', () => {
        it('converts UTC components to local time representation', () => {
            // Create a UTC date: 2023-12-25 14:30:45 UTC
            const utcDate = new Date('2023-12-25T14:30:45.123Z');
            const localDate = changeToLocalTimezone(utcDate);

            expect(localDate).toBeDefined();
            expect(localDate!.getFullYear()).toBe(2023);
            expect(localDate!.getMonth()).toBe(11); // December (0-indexed)
            expect(localDate!.getDate()).toBe(25);
            expect(localDate!.getHours()).toBe(14);
            expect(localDate!.getMinutes()).toBe(30);
            expect(localDate!.getSeconds()).toBe(45);
            expect(localDate!.getMilliseconds()).toBe(123);
        });

        it('preserves the numeric components exactly', () => {
            const utcDate = new Date('2023-01-01T00:00:00.000Z');
            const localDate = changeToLocalTimezone(utcDate);

            expect(localDate!.getFullYear()).toBe(utcDate.getUTCFullYear());
            expect(localDate!.getMonth()).toBe(utcDate.getUTCMonth());
            expect(localDate!.getDate()).toBe(utcDate.getUTCDate());
            expect(localDate!.getHours()).toBe(utcDate.getUTCHours());
            expect(localDate!.getMinutes()).toBe(utcDate.getUTCMinutes());
            expect(localDate!.getSeconds()).toBe(utcDate.getUTCSeconds());
            expect(localDate!.getMilliseconds()).toBe(utcDate.getUTCMilliseconds());
        });

        it('returns undefined for undefined input', () => {
            expect(changeToLocalTimezone(undefined)).toBeUndefined();
        });

        it('handles edge cases like year boundaries', () => {
            const utcDate = new Date('2023-12-31T23:59:59.999Z');
            const localDate = changeToLocalTimezone(utcDate);

            expect(localDate!.getFullYear()).toBe(2023);
            expect(localDate!.getMonth()).toBe(11); // December
            expect(localDate!.getDate()).toBe(31);
            expect(localDate!.getHours()).toBe(23);
            expect(localDate!.getMinutes()).toBe(59);
            expect(localDate!.getSeconds()).toBe(59);
            expect(localDate!.getMilliseconds()).toBe(999);
        });
    });

    describe('changeToUtcTimezone', () => {
        it('converts local components to UTC time representation', () => {
            // Create a local date: 2023-12-25 14:30:45 local time
            const localDate = new Date(2023, 11, 25, 14, 30, 45, 123);
            const utcDate = changeToUtcTimezone(localDate);

            expect(utcDate).toBeDefined();
            expect(utcDate!.getUTCFullYear()).toBe(2023);
            expect(utcDate!.getUTCMonth()).toBe(11); // December (0-indexed)
            expect(utcDate!.getUTCDate()).toBe(25);
            expect(utcDate!.getUTCHours()).toBe(14);
            expect(utcDate!.getUTCMinutes()).toBe(30);
            expect(utcDate!.getUTCSeconds()).toBe(45);
            expect(utcDate!.getUTCMilliseconds()).toBe(123);
        });

        it('preserves the numeric components exactly', () => {
            const localDate = new Date(2023, 0, 1, 0, 0, 0, 0);
            const utcDate = changeToUtcTimezone(localDate);

            expect(utcDate!.getUTCFullYear()).toBe(localDate.getFullYear());
            expect(utcDate!.getUTCMonth()).toBe(localDate.getMonth());
            expect(utcDate!.getUTCDate()).toBe(localDate.getDate());
            expect(utcDate!.getUTCHours()).toBe(localDate.getHours());
            expect(utcDate!.getUTCMinutes()).toBe(localDate.getMinutes());
            expect(utcDate!.getUTCSeconds()).toBe(localDate.getSeconds());
            expect(utcDate!.getUTCMilliseconds()).toBe(localDate.getMilliseconds());
        });

        it('returns undefined for undefined input', () => {
            expect(changeToUtcTimezone(undefined)).toBeUndefined();
        });

        it('handles midnight correctly', () => {
            const localDate = new Date(2023, 11, 25, 0, 0, 0, 0);
            const utcDate = changeToUtcTimezone(localDate);

            expect(utcDate!.getUTCHours()).toBe(0);
            expect(utcDate!.getUTCMinutes()).toBe(0);
            expect(utcDate!.getUTCSeconds()).toBe(0);
            expect(utcDate!.getUTCMilliseconds()).toBe(0);
        });
    });

    describe('round-trip conversions', () => {
        it('changeToUtcTimezone and changeToLocalTimezone are inverse operations', () => {
            const originalLocal = new Date(2023, 11, 25, 14, 30, 45, 123);
            const utc = changeToUtcTimezone(originalLocal);
            const backToLocal = changeToLocalTimezone(utc);

            expect(backToLocal!.getTime()).toBe(originalLocal.getTime());
        });

        it('preserves all components through round-trip conversion', () => {
            const original = new Date(2023, 0, 1, 0, 0, 0, 1);
            const utc = changeToUtcTimezone(original);
            const local = changeToLocalTimezone(utc);

            expect(local!.getFullYear()).toBe(original.getFullYear());
            expect(local!.getMonth()).toBe(original.getMonth());
            expect(local!.getDate()).toBe(original.getDate());
            expect(local!.getHours()).toBe(original.getHours());
            expect(local!.getMinutes()).toBe(original.getMinutes());
            expect(local!.getSeconds()).toBe(original.getSeconds());
            expect(local!.getMilliseconds()).toBe(original.getMilliseconds());
        });
    });
});
