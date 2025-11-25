import { IntlShape } from 'react-intl';
import { describe, expect, it, vi } from 'vitest';
import { ScheduledTasksResources } from '../../../Strings/SREAgentResources';
import { getHumanReadableCronExpression } from '../CronExpression';

// Mock the dependencies at the top level before any other code
vi.mock('../../../Space/ScheduledTasks/ScheduledTasksUtilities', () => ({
    DayOfTheWeek: {
        Sunday: 0,
        Monday: 1,
        Tuesday: 2,
        Wednesday: 3,
        Thursday: 4,
        Friday: 5,
        Saturday: 6,
    },
    getDaysOfTheWeek: () => ({
        0: 'Sunday',
        1: 'Monday',
        2: 'Tuesday',
        3: 'Wednesday',
        4: 'Thursday',
        5: 'Friday',
        6: 'Saturday',
    }),
}));

const mockIntl = {
    formatMessage: (descriptor: any, values?: any) => {
        const mockResources: { [key: string]: string } = {
            [ScheduledTasksResources.everyMinutes.id]: 'Every {minutes, plural, one {minute} other {# minutes}}',
            [ScheduledTasksResources.everyHours.id]: 'Every {hours, plural, one {hour} other {# hours}}',
            [ScheduledTasksResources.dailyAt.id]: 'Daily at {time}',
            [ScheduledTasksResources.weeklyOn.id]: 'Weekly on {day} at {time}',
            [ScheduledTasksResources.monthlyOn.id]: 'Monthly on {dayOfMonth} at {time}',
            [ScheduledTasksResources.am.id]: 'AM',
            [ScheduledTasksResources.pm.id]: 'PM',
        };

        let message = mockResources[descriptor.id] || descriptor.defaultMessage || '';

        if (values) {
            Object.entries(values).forEach(([key, value]) => {
                // Handle ICU plural syntax for testing
                const pluralRegex = new RegExp(`{${key}, plural, one {([^}]+)} other {([^}]+)}}`, 'g');
                const pluralMatch = pluralRegex.exec(message);
                if (pluralMatch) {
                    const [, oneForm, otherForm] = pluralMatch;
                    const selectedForm = Number(value) === 1 ? oneForm : otherForm.replace('#', String(value));
                    message = message.replace(pluralRegex, selectedForm);
                } else {
                    message = message.replace(`{${key}}`, String(value));
                }
            });
        }

        return message;
    },
} as IntlShape;

const convertUTCToLocalForTest = (utcHours: number, utcMinutes: number): { hours: number; minutes: number } => {
    const utcDate = new Date();
    utcDate.setUTCHours(utcHours, utcMinutes, 0, 0);
    return {
        hours: utcDate.getHours(),
        minutes: utcDate.getMinutes(),
    };
};

const formatTimeForTest = (utcHours: number, utcMinutes: number): string => {
    const { hours, minutes } = convertUTCToLocalForTest(utcHours, utcMinutes);
    const displayHour = hours === 0 ? 12 : hours > 12 ? hours - 12 : hours;
    const ampm = hours >= 12 ? 'PM' : 'AM';
    return `${displayHour}:${minutes.toString().padStart(2, '0')} ${ampm}`;
};

describe('getHumanReadableCronExpression', () => {
    describe('edge cases and invalid inputs', () => {
        it('returns original value for empty string', () => {
            expect(getHumanReadableCronExpression('', mockIntl)).toBe('');
        });

        it('returns original value for invalid format with less than 5 parts', () => {
            expect(getHumanReadableCronExpression('0 0', mockIntl)).toBe('0 0');
        });

        it('returns original value for invalid format with more than 5 parts', () => {
            expect(getHumanReadableCronExpression('0 0 * * * * extra', mockIntl)).toBe('0 0 * * * * extra');
        });

        it('returns original value for non-matching patterns', () => {
            expect(getHumanReadableCronExpression('invalid cron', mockIntl)).toBe('invalid cron');
        });
    });

    describe('minute interval expressions', () => {
        it('parses every minute', () => {
            expect(getHumanReadableCronExpression('*/1 * * * *', mockIntl)).toBe('Every minute');
        });

        it('parses 5 minute intervals', () => {
            expect(getHumanReadableCronExpression('*/5 * * * *', mockIntl)).toBe('Every 5 minutes');
        });

        it('parses 15 minute intervals', () => {
            expect(getHumanReadableCronExpression('*/15 * * * *', mockIntl)).toBe('Every 15 minutes');
        });

        it('parses 30 minute intervals', () => {
            expect(getHumanReadableCronExpression('*/30 * * * *', mockIntl)).toBe('Every 30 minutes');
        });

        it('handles maximum minute interval (59)', () => {
            expect(getHumanReadableCronExpression('*/59 * * * *', mockIntl)).toBe('Every 59 minutes');
        });
    });

    describe('hour interval expressions', () => {
        it('parses every hour (special case)', () => {
            expect(getHumanReadableCronExpression('0 * * * *', mockIntl)).toBe('Every hour');
        });

        it('parses every hour (using interval)', () => {
            expect(getHumanReadableCronExpression('0 */1 * * *', mockIntl)).toBe('Every hour');
        });

        it('parses 2 hour intervals', () => {
            expect(getHumanReadableCronExpression('0 */2 * * *', mockIntl)).toBe('Every 2 hours');
        });

        it('parses 6 hour intervals', () => {
            expect(getHumanReadableCronExpression('0 */6 * * *', mockIntl)).toBe('Every 6 hours');
        });

        it('parses 12 hour intervals', () => {
            expect(getHumanReadableCronExpression('0 */12 * * *', mockIntl)).toBe('Every 12 hours');
        });

        it('handles maximum hour interval (23)', () => {
            expect(getHumanReadableCronExpression('0 */23 * * *', mockIntl)).toBe('Every 23 hours');
        });
    });

    describe('daily expressions', () => {
        it('parses daily at midnight', () => {
            expect(getHumanReadableCronExpression('0 0 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(0, 0)}`);
        });

        it('parses daily at noon', () => {
            expect(getHumanReadableCronExpression('0 12 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(12, 0)}`);
        });

        it('parses daily at specific time with minutes', () => {
            expect(getHumanReadableCronExpression('30 14 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(14, 30)}`);
        });

        it('parses daily early morning', () => {
            expect(getHumanReadableCronExpression('15 6 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(6, 15)}`);
        });

        it('parses daily late evening', () => {
            expect(getHumanReadableCronExpression('45 23 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(23, 45)}`);
        });
    });

    describe('weekly expressions with numeric days', () => {
        it('parses weekly on Sunday (0)', () => {
            expect(getHumanReadableCronExpression('0 9 * * 0', mockIntl)).toBe(`Weekly on Sunday at ${formatTimeForTest(9, 0)}`);
        });

        it('parses weekly on Monday (1)', () => {
            expect(getHumanReadableCronExpression('30 10 * * 1', mockIntl)).toBe(`Weekly on Monday at ${formatTimeForTest(10, 30)}`);
        });

        it('parses weekly on Friday (5)', () => {
            expect(getHumanReadableCronExpression('0 17 * * 5', mockIntl)).toBe(`Weekly on Friday at ${formatTimeForTest(17, 0)}`);
        });

        it('parses weekly on Saturday (6)', () => {
            expect(getHumanReadableCronExpression('15 8 * * 6', mockIntl)).toBe(`Weekly on Saturday at ${formatTimeForTest(8, 15)}`);
        });

        it('handles Sunday as 7', () => {
            expect(getHumanReadableCronExpression('0 12 * * 7', mockIntl)).toBe(`Weekly on Sunday at ${formatTimeForTest(12, 0)}`);
        });
    });

    describe('monthly expressions', () => {
        it('parses monthly on 1st', () => {
            expect(getHumanReadableCronExpression('0 9 1 * *', mockIntl)).toBe(`Monthly on 1 at ${formatTimeForTest(9, 0)}`);
        });

        it('parses monthly on 15th', () => {
            expect(getHumanReadableCronExpression('30 14 15 * *', mockIntl)).toBe(`Monthly on 15 at ${formatTimeForTest(14, 30)}`);
        });

        it('parses monthly on last day (31st)', () => {
            expect(getHumanReadableCronExpression('45 23 31 * *', mockIntl)).toBe(`Monthly on 31 at ${formatTimeForTest(23, 45)}`);
        });

        it('parses monthly on mid-month', () => {
            expect(getHumanReadableCronExpression('0 12 20 * *', mockIntl)).toBe(`Monthly on 20 at ${formatTimeForTest(12, 0)}`);
        });
    });

    describe('time formatting edge cases', () => {
        it('handles single digit hours and minutes', () => {
            expect(getHumanReadableCronExpression('5 7 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(7, 5)}`);
        });

        it('handles double digit hours and minutes', () => {
            expect(getHumanReadableCronExpression('45 23 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(23, 45)}`);
        });

        it('handles midnight (00:00)', () => {
            expect(getHumanReadableCronExpression('0 0 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(0, 0)}`);
        });

        it('handles noon (12:00)', () => {
            expect(getHumanReadableCronExpression('0 12 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(12, 0)}`);
        });

        it('handles edge hours (23:59)', () => {
            expect(getHumanReadableCronExpression('59 23 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(23, 59)}`);
        });
    });

    describe('boundary value testing', () => {
        it('handles minimum minute value (0)', () => {
            expect(getHumanReadableCronExpression('0 12 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(12, 0)}`);
        });

        it('handles maximum minute value (59)', () => {
            expect(getHumanReadableCronExpression('59 12 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(12, 59)}`);
        });

        it('handles minimum hour value (0)', () => {
            expect(getHumanReadableCronExpression('30 0 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(0, 30)}`);
        });

        it('handles maximum hour value (23)', () => {
            expect(getHumanReadableCronExpression('30 23 * * *', mockIntl)).toBe(`Daily at ${formatTimeForTest(23, 30)}`);
        });

        it('handles minimum day of month (1)', () => {
            expect(getHumanReadableCronExpression('0 9 1 * *', mockIntl)).toBe(`Monthly on 1 at ${formatTimeForTest(9, 0)}`);
        });

        it('handles maximum day of month (31)', () => {
            expect(getHumanReadableCronExpression('0 9 31 * *', mockIntl)).toBe(`Monthly on 31 at ${formatTimeForTest(9, 0)}`);
        });
    });

    describe('non-matching patterns fallback', () => {
        it('returns original cron for complex patterns not supported', () => {
            const complexCron = '0,15,30,45 9-17 * * 1-5';
            expect(getHumanReadableCronExpression(complexCron, mockIntl)).toBe(complexCron);
        });

        it('returns original cron for range patterns', () => {
            const rangeCron = '0 9-17 * * *';
            expect(getHumanReadableCronExpression(rangeCron, mockIntl)).toBe(rangeCron);
        });

        it('returns original cron for step values in complex patterns', () => {
            const stepCron = '0 */2 1-15 * *';
            expect(getHumanReadableCronExpression(stepCron, mockIntl)).toBe(stepCron);
        });

        it('returns original cron for multiple day of week values', () => {
            const multiDowCron = '0 9 * * 1,3,5';
            expect(getHumanReadableCronExpression(multiDowCron, mockIntl)).toBe(multiDowCron);
        });
    });

    describe('UTC to local time conversion consistency', () => {
        it('converts UTC time correctly for different timezones', () => {
            const result = getHumanReadableCronExpression('0 12 * * *', mockIntl);
            expect(result).toMatch(/Daily at \d{1,2}:\d{2} (AM|PM)/);
        });

        it('maintains consistent time format across different expressions', () => {
            const dailyResult = getHumanReadableCronExpression('30 14 * * *', mockIntl);
            const weeklyResult = getHumanReadableCronExpression('30 14 * * 1', mockIntl);

            const timePattern = /\d{1,2}:\d{2} (AM|PM)/;
            const dailyTime = dailyResult.match(timePattern)?.[0];
            const weeklyTime = weeklyResult.match(timePattern)?.[0];

            expect(dailyTime).toBe(weeklyTime);
        });
    });
});
