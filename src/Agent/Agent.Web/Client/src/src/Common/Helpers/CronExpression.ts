import { IntlShape } from 'react-intl';
import { DayOfTheWeek, getDaysOfTheWeek } from '../../Space/ScheduledTasks/ScheduledTasksUtilities';
import { ScheduledTasksResources } from '../../Strings/SREAgentResources';

const convertUTCToLocal = (utcHours: number, utcMinutes: number): { hours: number; minutes: number } => {
    const utcDate = new Date();
    utcDate.setUTCHours(utcHours, utcMinutes, 0, 0);
    return {
        hours: utcDate.getHours(),
        minutes: utcDate.getMinutes(),
    };
};

const formatTime = (utcHours: number, utcMinutes: number, intl: IntlShape): string => {
    const { hours, minutes } = convertUTCToLocal(utcHours, utcMinutes);
    const displayHour = hours === 0 ? 12 : hours > 12 ? hours - 12 : hours;
    const ampm = hours >= 12 ? intl.formatMessage(ScheduledTasksResources.pm) : intl.formatMessage(ScheduledTasksResources.am);
    const paddedMinutes = minutes.toString().padStart(2, '0');
    return `${displayHour}:${paddedMinutes} ${ampm}`;
};

const parseMatch = (match: RegExpMatchArray | null, index: number): number | null => {
    return match ? parseInt(match[index], 10) : null;
};

// TODO: Expand this to cover more cases as needed or use external library
export const getHumanReadableCronExpression = (cron: string, intl: IntlShape): string => {
    // Pattern definitions
    const patterns = {
        minuteInterval: /^\*\/([0-5]?[0-9]) \* \* \* \*$/,
        hourInterval: /^0 \*\/([0-1]?[0-9]|2[0-3]) \* \* \*$/,
        daily: /^([0-5]?[0-9]) ([0-1]?[0-9]|2[0-3]) \* \* \*$/,
        weekly: /^([0-5]?[0-9]) ([0-1]?[0-9]|2[0-3]) \* \* ([0-7])$/,
        monthly: /^([0-5]?[0-9]) ([0-1]?[0-9]|2[0-3]) ([1-9]|[12][0-9]|3[01]) \* \*$/,
    };

    // Day names mapping
    const dayNames = getDaysOfTheWeek(intl);

    // Check minute intervals (*/N * * * *)
    let match = cron.match(patterns.minuteInterval);
    if (match) {
        const minutes = parseMatch(match, 1);
        if (minutes !== null) {
            return intl.formatMessage(ScheduledTasksResources.everyMinutes, { minutes });
        }
    }

    // Check hour intervals (0 */N * * *)
    if (cron === '0 * * * *') {
        return intl.formatMessage(ScheduledTasksResources.everyHours, { hours: 1 });
    }
    match = cron.match(patterns.hourInterval);
    if (match) {
        const hours = parseMatch(match, 1);
        if (hours !== null) {
            return intl.formatMessage(ScheduledTasksResources.everyHours, { hours });
        }
    }

    match = cron.match(patterns.daily);
    if (match) {
        const minutes = parseMatch(match, 1);
        const hours = parseMatch(match, 2);
        if (hours !== null && minutes !== null) {
            return intl.formatMessage(ScheduledTasksResources.dailyAt, { time: formatTime(hours, minutes, intl) });
        }
    }

    // Check weekly (M H * * D)
    match = cron.match(patterns.weekly);
    if (match) {
        const minutes = parseMatch(match, 1);
        const hours = parseMatch(match, 2);
        const dayOfWeek = parseMatch(match, 3);
        if (hours !== null && minutes !== null && dayOfWeek !== null) {
            const normalizedDay = dayOfWeek === 7 ? DayOfTheWeek.Sunday : (dayOfWeek as DayOfTheWeek);
            const dayName = dayNames[normalizedDay];

            return intl.formatMessage(ScheduledTasksResources.weeklyOn, { day: dayName, time: formatTime(hours, minutes, intl) });
        }
    }

    // Check monthly (M H D * *)
    match = cron.match(patterns.monthly);
    if (match) {
        const minutes = parseMatch(match, 1);
        const hours = parseMatch(match, 2);
        const dayOfMonth = parseMatch(match, 3);
        if (hours !== null && minutes !== null && dayOfMonth !== null) {
            return intl.formatMessage(ScheduledTasksResources.monthlyOn, {
                dayOfMonth: dayOfMonth,
                time: formatTime(hours, minutes, intl),
            });
        }
    }

    return cron;
};
