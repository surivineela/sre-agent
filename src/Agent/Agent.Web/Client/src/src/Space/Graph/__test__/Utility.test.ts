import { describe, expect, it } from 'vitest';
import { ResourceExtended } from '../../Contracts/Graph';
import { getAppGroupEffectiveType, parseCronExpression } from '../Utility';

const makeAppGroup = (overrides: Partial<ResourceExtended>): ResourceExtended => ({
    id: 'sub_rg_name',
    name: 'test-app',
    type: 'microsoft.web/sites',
    dashboardUrl: '',
    properties: {
        dashboardUrl: [''],
        resourceType: ['microsoft.web/sites'],
        resourceKind: ['app'],
        resourceName: ['test-app'],
        subscriptionId: ['sub'],
        resourceGroupName: ['rg'],
        location: ['westus'],
        runningStatus: 'Running',
        remarks: [''],
    },
    ...overrides,
});

describe('getAppGroupEffectiveType', () => {
    it('returns functionapp when kind indicates function app', () => {
        const ag = makeAppGroup({ properties: { ...makeAppGroup({}).properties, kind: ['functionapp'] } as any });
        expect(getAppGroupEffectiveType(ag)).toBe('functionapp');
    });

    it('returns functionapp when properties.resourceKind array contains functionapp', () => {
        const ag = makeAppGroup({ properties: { ...makeAppGroup({}).properties, resourceKind: ['functionapp'] } });
        expect(getAppGroupEffectiveType(ag)).toBe('functionapp');
    });

    it('returns original type when not a function app', () => {
        const ag = makeAppGroup({ type: 'microsoft.web/sites', properties: { ...makeAppGroup({}).properties, resourceKind: ['app'] } });
        expect(getAppGroupEffectiveType(ag)).toBe('microsoft.web/sites');
    });

    it('handles non-array kind string', () => {
        const ag: any = makeAppGroup({});
        ag.kind = 'functionApp';
        expect(getAppGroupEffectiveType(ag)).toBe('functionapp');
    });

    it('returns empty string safely when appGroup is undefined', () => {
        // @ts-expect-error testing undefined input
        expect(getAppGroupEffectiveType(undefined)).toBe('');
    });
});

describe('parseCronExpression', () => {
    describe('edge cases and invalid inputs', () => {
        it('returns original value for empty string', () => {
            expect(parseCronExpression('')).toBe('');
        });

        it('returns original value for N/A', () => {
            expect(parseCronExpression('N/A')).toBe('N/A');
        });

        it('returns original value for invalid format with less than 5 parts', () => {
            expect(parseCronExpression('0 0')).toBe('0 0');
        });

        it('returns original value for invalid format with more than 6 parts', () => {
            expect(parseCronExpression('0 0 * * * * extra')).toBe('0 0 * * * * extra');
        });

        it('handles whitespace properly', () => {
            expect(parseCronExpression('   0 0 * * *   ')).toBe('Daily at midnight');
        });
    });

    describe('standard 5-part cron expressions', () => {
        it('parses daily at midnight', () => {
            expect(parseCronExpression('0 0 * * *')).toBe('Daily at midnight');
        });

        it('parses every hour', () => {
            expect(parseCronExpression('0 * * * *')).toBe('Every hour');
        });

        it('parses every minute', () => {
            expect(parseCronExpression('* * * * *')).toBe('Every minute');
        });

        it('parses weekly on Sunday', () => {
            expect(parseCronExpression('30 14 * * 0')).toBe('Weekly on Sunday at 14:30');
        });

        it('parses monthly on 1st', () => {
            expect(parseCronExpression('0 9 1 * *')).toBe('Monthly on 1st at 09:00');
        });

        it('parses daily at specific time', () => {
            expect(parseCronExpression('15 8 * * *')).toBe('Daily at 08:15');
        });

        it('parses daily at specific time with single digit hour', () => {
            expect(parseCronExpression('30 6 * * *')).toBe('Daily at 06:30');
        });
    });

    describe('extended 6-part cron expressions', () => {
        it('parses 6-part daily at midnight', () => {
            expect(parseCronExpression('0 0 0 * * *')).toBe('Daily at midnight');
        });

        it('parses 6-part every hour', () => {
            expect(parseCronExpression('0 0 * * * *')).toBe('Every hour');
        });

        it('parses 6-part every minute', () => {
            expect(parseCronExpression('0 * * * * *')).toBe('Every minute');
        });

        it('parses 6-part weekly on Sunday', () => {
            expect(parseCronExpression('0 45 12 * * 0')).toBe('Weekly on Sunday at 12:45');
        });

        it('parses 6-part monthly on 1st', () => {
            expect(parseCronExpression('0 0 10 1 * *')).toBe('Monthly on 1st at 10:00');
        });

        it('parses 6-part daily at specific time', () => {
            expect(parseCronExpression('0 20 15 * * *')).toBe('Daily at 15:20');
        });
    });

    describe('interval expressions', () => {
        it('parses minute intervals', () => {
            expect(parseCronExpression('*/5 * * * *')).toBe('Every 5 minutes');
        });

        it('parses minute intervals with 6-part format', () => {
            expect(parseCronExpression('0 */10 * * * *')).toBe('Every 10 minutes');
        });

        it('parses hour intervals', () => {
            expect(parseCronExpression('0 */3 * * *')).toBe('Every 3 hours');
        });

        it('parses hour intervals with 6-part format', () => {
            expect(parseCronExpression('0 0 */2 * * *')).toBe('Every 2 hours');
        });
    });

    describe('day of week expressions', () => {
        it('parses single day of week', () => {
            expect(parseCronExpression('0 9 * * 1')).toBe('Weekly on Monday at 09:00');
        });

        it('parses multiple days of week', () => {
            expect(parseCronExpression('30 17 * * 1,3,5')).toBe('Weekly on Monday, Wednesday, Friday at 17:30');
        });

        it('parses weekend days', () => {
            expect(parseCronExpression('0 10 * * 0,6')).toBe('Weekly on Sunday, Saturday at 10:00');
        });

        it('handles invalid day numbers gracefully', () => {
            expect(parseCronExpression('0 12 * * 8')).toBe('Weekly on Day 8 at 12:00');
        });

        it('parses 6-part single day of week', () => {
            expect(parseCronExpression('0 0 14 * * 2')).toBe('Weekly on Tuesday at 14:00');
        });

        it('parses 6-part multiple days of week', () => {
            expect(parseCronExpression('0 15 8 * * 1,2,3')).toBe('Weekly on Monday, Tuesday, Wednesday at 08:15');
        });
    });

    describe('specific date expressions', () => {
        it('parses specific day and month', () => {
            expect(parseCronExpression('0 12 15 6 *')).toBe('At 12:00 on 15/6');
        });

        it('parses specific day and month with minutes', () => {
            expect(parseCronExpression('30 9 25 12 *')).toBe('At 09:30 on 25/12');
        });

        it('parses 6-part specific date', () => {
            expect(parseCronExpression('0 45 16 1 1 *')).toBe('At 16:45 on 1/1');
        });
    });

    describe('time formatting', () => {
        it('pads single digit hours and minutes', () => {
            expect(parseCronExpression('5 7 * * *')).toBe('Daily at 07:05');
        });

        it('handles double digit hours and minutes', () => {
            expect(parseCronExpression('45 23 * * *')).toBe('Daily at 23:45');
        });

        it('pads zero minutes', () => {
            expect(parseCronExpression('0 15 * * *')).toBe('Daily at 15:00');
        });

        it('pads zero hours', () => {
            expect(parseCronExpression('30 0 * * *')).toBe('Daily at 00:30');
        });
    });
});
