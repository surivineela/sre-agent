import { describe, expect, it } from 'vitest';
import {
    formatDate,
    formatTimeStringTo12HoursFormat,
    formatTimeStringTo24HoursFormat,
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
