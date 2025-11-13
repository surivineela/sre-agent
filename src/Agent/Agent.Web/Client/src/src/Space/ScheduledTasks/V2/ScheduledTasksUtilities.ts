import { IntlShape } from 'react-intl';
import { ScheduledTasksResources } from '../../../Strings/SREAgentResources';
import { ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';
import { normalizeCronExpression } from '../../Graph/ExtendedAgentCreationDialog/utils/schedule';

export enum TaskStatusFilterKey {
    All = 'All',
    On = 'On',
    Off = 'Off',
    Ended = 'Ended',
}

export enum TaskFrequencyKey {
    Daily = 'Daily',
    Weekly = 'Weekly',
    Monthly = 'Monthly',
    Custom = 'Custom',
}

export enum DayOfTheWeek {
    Sunday,
    Monday,
    Tuesday,
    Wednesday,
    Thursday,
    Friday,
    Saturday,
}

export enum GroupMessageKey {
    SameThread = 'SameThread',
    NewThread = 'NewThread',
}

export interface ScheduledTaskFormProps {
    name: string;
    subAgent?: string;
    details: string;
    frequency: TaskFrequencyKey;
    timeOfDay: Date;
    dayOfWeek: DayOfTheWeek;
    dayOfMonth: string;
    customCron?: string;
    startOn: Date;
    repeatUntil?: Date | null;
    groupMessages: GroupMessageKey;
    runLimit?: string;
}

export const getFilterKeyFromScheduledTaskStatus = (status: ScheduledTaskStatus) => {
    switch (status) {
        case ScheduledTaskStatus.Active:
            return TaskStatusFilterKey.On;
        case ScheduledTaskStatus.Paused:
            return TaskStatusFilterKey.Off;
        case ScheduledTaskStatus.Completed:
            return TaskStatusFilterKey.Ended;
        default:
            return TaskStatusFilterKey.All;
    }
};

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

export const getDaysOfTheWeek = (intl: IntlShape) => {
    return {
        [DayOfTheWeek.Sunday]: intl.formatMessage(ScheduledTasksResources.sunday),
        [DayOfTheWeek.Monday]: intl.formatMessage(ScheduledTasksResources.monday),
        [DayOfTheWeek.Tuesday]: intl.formatMessage(ScheduledTasksResources.tuesday),
        [DayOfTheWeek.Wednesday]: intl.formatMessage(ScheduledTasksResources.wednesday),
        [DayOfTheWeek.Thursday]: intl.formatMessage(ScheduledTasksResources.thursday),
        [DayOfTheWeek.Friday]: intl.formatMessage(ScheduledTasksResources.friday),
        [DayOfTheWeek.Saturday]: intl.formatMessage(ScheduledTasksResources.saturday),
    };
};

// TODO: Expand this to cover more cases as needed or use external library
export const getHumanReadableCronExpression = (cron: string, intl: IntlShape): string => {
    // Pattern definitions
    const patterns = {
        minuteInterval: /^\*\/([0-5]?[0-9]) \* \* \* \*$/,
        hourInterval: /^0 \*\/([0-1]?[0-9]|2[0-3]) \* \* \*$/,
        daily: /^([0-5]?[0-9]) ([0-1]?[0-9]|2[0-3]) \* \* \*$/,
        weekly: /^([0-5]?[0-9]) ([0-1]?[0-9]|2[0-3]) \* \* ([0-6])$/,
        monthly: /^([0-5]?[0-9]) ([0-1]?[0-9]|2[0-3]) ([1-9]|[12][0-9]|3[01]) \* \*$/,
    };

    // Day names mapping
    const dayNames = getDaysOfTheWeek(intl);

    // Check minute intervals (*/N * * * *)
    let match = cron.match(patterns.minuteInterval);
    if (match) {
        const minutes = parseMatch(match, 1);
        if (minutes !== null) {
            return minutes === 1
                ? intl.formatMessage(ScheduledTasksResources.everyMinute)
                : intl.formatMessage(ScheduledTasksResources.everyMinutes, { minutes: minutes });
        }
    }

    // Check hour intervals (0 */N * * *)
    if (cron === '0 * * * *') {
        return intl.formatMessage(ScheduledTasksResources.everyHour);
    }
    match = cron.match(patterns.hourInterval);
    if (match) {
        const hours = parseMatch(match, 1);
        if (hours !== null) {
            return hours === 1
                ? intl.formatMessage(ScheduledTasksResources.everyHour)
                : intl.formatMessage(ScheduledTasksResources.everyHours, { hours: hours });
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
            const dayName = dayNames[dayOfWeek as DayOfTheWeek];
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

export const getCronExpression = (params: {
    frequency: TaskFrequencyKey;
    timeOfDay: Date;
    dayOfWeek?: DayOfTheWeek;
    dayOfMonth?: string;
}): string => {
    const { frequency, timeOfDay, dayOfWeek, dayOfMonth } = params;

    switch (frequency) {
        case TaskFrequencyKey.Daily:
            return `${timeOfDay.getUTCMinutes()} ${timeOfDay.getUTCHours()} * * *`;
        case TaskFrequencyKey.Weekly:
            return `${timeOfDay.getUTCMinutes()} ${timeOfDay.getUTCHours()} * * ${dayOfWeek}`;
        case TaskFrequencyKey.Monthly:
            return `${timeOfDay.getUTCMinutes()} ${timeOfDay.getUTCHours()} ${dayOfMonth} * *`;
        case TaskFrequencyKey.Custom:
        default:
            return '';
    }
};

export const getTimeFieldValuesFromCronExpression = (cron?: string) => {
    let frequency: TaskFrequencyKey | undefined;
    let timeOfDay: Date | undefined;
    let dayOfWeek: DayOfTheWeek | undefined;
    let dayOfMonth: string | undefined;

    if (cron) {
        const normalizedCron = normalizeCronExpression(cron);
        const [cronMinute, cronHour, cronDayOfMonth, cronMonth, cronDayOfWeek] = normalizedCron.split(' ');
        // Doesn't need to be local time, as this is displayed to the user, who will select as if it's local,
        // and the value will be converted back to UTC when saving
        const cronMinuteUTC = parseInt(cronMinute);
        const cronHourUTC = parseInt(cronHour);

        if (cronMinuteUTC >= 0 && cronHourUTC >= 0) {
            timeOfDay = new Date();
            timeOfDay.setUTCMinutes(cronMinuteUTC);
            timeOfDay.setUTCHours(cronHourUTC);
        }

        dayOfWeek = parseInt(cronDayOfWeek) as DayOfTheWeek;
        dayOfMonth = cronDayOfMonth;

        if (timeOfDay) {
            if (cronDayOfMonth === '*' && cronMonth === '*' && cronDayOfWeek === '*') {
                frequency = TaskFrequencyKey.Daily;
            } else if (cronDayOfMonth === '*' && cronMonth === '*' && cronDayOfWeek !== '*') {
                frequency = TaskFrequencyKey.Weekly;
            } else if (cronDayOfMonth !== '*' && cronMonth === '*' && cronDayOfWeek === '*') {
                frequency = TaskFrequencyKey.Monthly;
            } else {
                frequency = TaskFrequencyKey.Custom;
            }
        } else {
            frequency = TaskFrequencyKey.Custom;
        }
    }
    return { frequency, timeOfDay, dayOfWeek, dayOfMonth };
};
