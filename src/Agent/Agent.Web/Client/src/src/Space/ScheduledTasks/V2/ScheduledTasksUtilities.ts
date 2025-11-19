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

export enum GroupMessageKey {
    SameThread = 'SameThread',
    NewThread = 'NewThread',
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

const validateCronField: (field: string, min: number, max: number) => boolean = (field: string, min: number, max: number): boolean => {
    if (field === '*') return true;

    if (field.includes('/')) {
        const [range, step] = field.split('/');
        const stepNum = parseInt(step, 10);
        if (isNaN(stepNum) || stepNum <= 0) return false;

        if (range === '*') return true;
        if (range.includes('-')) {
            const [start, end] = range.split('-').map(n => parseInt(n, 10));
            return !isNaN(start) && !isNaN(end) && start >= min && end <= max && start <= end;
        }
        const rangeNum = parseInt(range, 10);
        return !isNaN(rangeNum) && rangeNum >= min && rangeNum <= max;
    }

    if (field.includes('-')) {
        const [start, end] = field.split('-').map(n => parseInt(n, 10));
        return !isNaN(start) && !isNaN(end) && start >= min && end <= max && start <= end;
    }

    if (field.includes(',')) {
        const values = field.split(',').map(v => parseInt(v.trim(), 10));
        return values.every(v => !isNaN(v) && v >= min && v <= max);
    }

    const num = parseInt(field, 10);
    return !isNaN(num) && num >= min && num <= max;
};

export const validateCronExpression = (cronExpression: string, intl: IntlShape): { isValid: boolean; error?: string } => {
    const errorMessage = intl.formatMessage(ScheduledTasksResources.invalidCronExpression);

    if (!cronExpression || cronExpression.trim() === '') {
        return { isValid: false, error: errorMessage };
    }

    const normalized = cronExpression.trim().replace(/\s+/g, ' ');
    const parts = normalized.split(' ');

    if (parts.length !== 5) {
        return { isValid: false, error: errorMessage };
    }

    if (parts.some(p => p.length === 0)) {
        return { isValid: false, error: errorMessage };
    }

    const [minute, hour, day, month, dayOfWeek] = parts;

    if (!validateCronField(minute, 0, 59)) {
        return { isValid: false, error: errorMessage };
    }

    if (!validateCronField(hour, 0, 23)) {
        return { isValid: false, error: errorMessage };
    }

    if (!validateCronField(day, 1, 31)) {
        return { isValid: false, error: errorMessage };
    }

    if (!validateCronField(month, 1, 12)) {
        return { isValid: false, error: errorMessage };
    }

    if (!validateCronField(dayOfWeek, 0, 7)) {
        return { isValid: false, error: errorMessage };
    }

    return { isValid: true };
};
