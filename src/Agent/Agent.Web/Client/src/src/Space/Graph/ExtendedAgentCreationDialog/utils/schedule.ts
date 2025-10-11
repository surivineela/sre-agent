import { SchedulePresetKey } from '../types';

export const SCHEDULE_PRESETS: Record<Exclude<SchedulePresetKey, 'custom'>, { label: string; cron: string }> = {
    hourly: { label: 'Every hour', cron: '0 * * * *' },
    every15m: { label: 'Every 15 minutes', cron: '*/15 * * * *' },
    daily: { label: 'Daily at 9AM', cron: '0 9 * * *' },
    weekly: { label: 'Weekly on Monday at 9AM', cron: '0 9 * * 1' },
    monthly: { label: 'Monthly on 1st at 9AM', cron: '0 9 1 * *' },
    workdays: { label: 'Weekdays at 9AM', cron: '0 9 * * 1-5' },
};

export const DEFAULT_SCHEDULE_PRESET: Exclude<SchedulePresetKey, 'custom'> = 'daily';

export const getPresetFromCron = (cron: string): Exclude<SchedulePresetKey, 'custom'> | undefined => {
    const normalized = normalizeCronExpression(cron);
    const match = (Object.entries(SCHEDULE_PRESETS) as Array<[Exclude<SchedulePresetKey, 'custom'>, { label: string; cron: string }]>).find(
        ([, presetValue]) => presetValue.cron === normalized
    );
    return match?.[0];
};

export const normalizeCronExpression = (value: string): string => value.trim().replace(/\s+/g, ' ');

export const isCronExpressionLikelyValid = (value: string): boolean => {
    const parts = normalizeCronExpression(value).split(' ');
    return parts.length === 5 && parts.every(part => part.length > 0);
};

export const getScheduleDescription = (cronExpression: string): string => {
    const normalized = normalizeCronExpression(cronExpression);
    const preset = Object.entries(SCHEDULE_PRESETS).find(([, presetValue]) => presetValue.cron === normalized);
    if (preset) {
        return preset[1].label;
    }
    return isCronExpressionLikelyValid(normalized) ? 'Custom schedule' : '—';
};

const toLocaleStringInline = (date: Date): string =>
    date.toLocaleString(undefined, {
        hour: 'numeric',
        minute: '2-digit',
        weekday: 'short',
        month: 'short',
        day: 'numeric',
    });

export const getNextRunPreview = (cronExpression: string, count = 3): string[] => {
    const expression = normalizeCronExpression(cronExpression);
    const now = new Date();
    const results: string[] = [];

    const pushCopy = (date: Date) => results.push(toLocaleStringInline(new Date(date)));

    if (expression === SCHEDULE_PRESETS.hourly.cron) {
        const next = new Date(now);
        next.setMinutes(0, 0, 0);
        while (next <= now) next.setHours(next.getHours() + 1);
        for (let i = 0; i < count; i++) {
            pushCopy(next);
            next.setHours(next.getHours() + 1);
        }
        return results;
    }

    if (expression === SCHEDULE_PRESETS.every15m.cron) {
        const next = new Date(now);
        next.setSeconds(0, 0);
        const quarter = Math.floor(next.getMinutes() / 15) * 15;
        next.setMinutes(quarter, 0, 0);
        while (next <= now) next.setMinutes(next.getMinutes() + 15);
        for (let i = 0; i < count; i++) {
            pushCopy(next);
            next.setMinutes(next.getMinutes() + 15);
        }
        return results;
    }

    if (expression === SCHEDULE_PRESETS.daily.cron) {
        const next = new Date(now);
        next.setHours(9, 0, 0, 0);
        if (next <= now) {
            next.setDate(next.getDate() + 1);
        }
        for (let i = 0; i < count; i++) {
            pushCopy(next);
            next.setDate(next.getDate() + 1);
        }
        return results;
    }

    if (expression === SCHEDULE_PRESETS.weekly.cron) {
        const next = new Date(now);
        next.setHours(9, 0, 0, 0);
        const currentDay = next.getDay();
        const distanceToMonday = (8 - currentDay) % 7;
        if (distanceToMonday === 0 && next <= now) {
            next.setDate(next.getDate() + 7);
        } else {
            next.setDate(next.getDate() + distanceToMonday);
        }
        for (let i = 0; i < count; i++) {
            pushCopy(next);
            next.setDate(next.getDate() + 7);
        }
        return results;
    }

    if (expression === SCHEDULE_PRESETS.monthly.cron) {
        const next = new Date(now);
        next.setHours(9, 0, 0, 0);
        next.setDate(1);
        if (next <= now) {
            next.setMonth(next.getMonth() + 1);
            next.setDate(1);
        }
        for (let i = 0; i < count; i++) {
            pushCopy(next);
            next.setMonth(next.getMonth() + 1);
            next.setDate(1);
        }
        return results;
    }

    if (expression === SCHEDULE_PRESETS.workdays.cron) {
        const next = new Date(now);
        next.setHours(9, 0, 0, 0);
        next.setSeconds(0, 0);
        const advanceToNextWorkday = (date: Date) => {
            while (date.getDay() === 0 || date.getDay() === 6 || date <= now) {
                date.setDate(date.getDate() + 1);
                date.setHours(9, 0, 0, 0);
            }
        };
        advanceToNextWorkday(next);
        for (let i = 0; i < count; i++) {
            pushCopy(next);
            next.setDate(next.getDate() + 1);
            advanceToNextWorkday(next);
        }
        return results;
    }

    return results;
};

const SIMPLE_MAPPINGS: Array<{ pattern: RegExp; cron: string }> = [
    { pattern: /^every\s+day\s+at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$/i, cron: 'DAILY' },
    { pattern: /^every\s+weekday\s+at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$/i, cron: 'WORKDAYS' },
    {
        pattern:
            /^every\s+week\s+on\s+(monday|tuesday|wednesday|thursday|friday|saturday|sunday)\s+at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$/i,
        cron: 'WEEKLY',
    },
    { pattern: /^every\s+hour$/i, cron: SCHEDULE_PRESETS.hourly.cron },
    { pattern: /^every\s+15\s*(minutes|min)$/i, cron: SCHEDULE_PRESETS.every15m.cron },
];

const dayMap: Record<string, number> = {
    sunday: 0,
    monday: 1,
    tuesday: 2,
    wednesday: 3,
    thursday: 4,
    friday: 5,
    saturday: 6,
};

const toCronTime = (hoursRaw?: string, minutesRaw?: string, meridianRaw?: string): { hours: number; minutes: number } | null => {
    if (!hoursRaw) {
        return null;
    }
    let hours = Number.parseInt(hoursRaw, 10);
    if (Number.isNaN(hours)) {
        return null;
    }
    let minutes = minutesRaw ? Number.parseInt(minutesRaw, 10) : 0;
    if (Number.isNaN(minutes)) {
        minutes = 0;
    }
    const meridian = meridianRaw?.toLowerCase();
    if (meridian === 'pm' && hours < 12) {
        hours += 12;
    }
    if (meridian === 'am' && hours === 12) {
        hours = 0;
    }
    if (hours >= 24 || minutes >= 60) {
        return null;
    }
    return { hours, minutes };
};

export const tryParseNaturalLanguageToCron = (value: string): { cron?: string; preset?: Exclude<SchedulePresetKey, 'custom'> } | null => {
    const text = value.trim();
    if (!text) {
        return null;
    }

    for (const mapping of SIMPLE_MAPPINGS) {
        const match = text.match(mapping.pattern);
        if (!match) {
            continue;
        }
        if (mapping.cron === 'DAILY') {
            const time = toCronTime(match[1], match[2], match[3]);
            if (!time) {
                return null;
            }
            return { cron: `${time.minutes} ${time.hours} * * *`, preset: 'daily' };
        }
        if (mapping.cron === 'WORKDAYS') {
            const time = toCronTime(match[1], match[2], match[3]);
            if (!time) {
                return null;
            }
            return { cron: `${time.minutes} ${time.hours} * * 1-5`, preset: 'workdays' };
        }
        if (mapping.cron === 'WEEKLY') {
            const day = match[1]?.toLowerCase();
            const time = toCronTime(match[2], match[3], match[4]);
            if (!day || !(day in dayMap) || !time) {
                return null;
            }
            return { cron: `${time.minutes} ${time.hours} * * ${dayMap[day]}` };
        }
        return { cron: mapping.cron as string, preset: mapping.cron === SCHEDULE_PRESETS.hourly.cron ? 'hourly' : 'every15m' };
    }

    const monthlyMatch = text.match(/^every\s+month\s+on\s+(\d{1,2})(st|nd|rd|th)?\s+at\s+(\d{1,2})(?::(\d{2}))?\s*(am|pm)?$/i);
    if (monthlyMatch) {
        const day = Number.parseInt(monthlyMatch[1], 10);
        const time = toCronTime(monthlyMatch[3], monthlyMatch[4], monthlyMatch[5]);
        if (!Number.isNaN(day) && day >= 1 && day <= 31 && time) {
            return { cron: `${time.minutes} ${time.hours} ${day} * *`, preset: 'monthly' };
        }
    }

    return null;
};
