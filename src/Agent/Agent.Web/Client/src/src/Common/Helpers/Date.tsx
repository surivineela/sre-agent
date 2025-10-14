import { TimeRangeValue, TimespanKeys } from '../Components/PillFilter/Contracts';

export function getSafeDateTime(dateTime: Date | string): Date {
    let stringFormat: string;

    if (dateTime instanceof Date && dateTime.toISOString && dateTime.toISOString()) {
        stringFormat = dateTime.toISOString();
    } else {
        stringFormat = dateTime.toString();
    }

    return new Date(stringFormat);
}

export const compareDates = (lhs: Date | undefined, rhs: Date | undefined) => {
    if (!lhs || !rhs) {
        return 0;
    }
    return lhs < rhs ? 1 : -1;
};

export const getFormattedValue = (value: number): string => {
    return value.toString().padStart(2, '0');
};

export const getTimePriorToday = (numOfHoursPriorToday: number): Date => {
    const today = new Date();
    return new Date(today.getTime() - 1000 * 60 * 60 * numOfHoursPriorToday);
};

export const getUTCTimeStringPriorToday = (numOfDaysPriorToday: number): string => {
    const startingDate = getTimePriorToday(numOfDaysPriorToday * 24);
    const year = startingDate.getUTCFullYear();
    const month = startingDate.getUTCMonth() + 1;
    const date = startingDate.getUTCDate();
    const hours = startingDate.getUTCHours();
    const minutes = startingDate.getUTCMinutes();
    const seconds = startingDate.getUTCSeconds();
    // used at getBackupSnapshots ARM
    return `${year}-${getFormattedValue(month)}-${getFormattedValue(date)}T${getFormattedValue(hours)}:${getFormattedValue(
        minutes
    )}:${getFormattedValue(seconds)}`;
};

export const getTimeString = (hour: number, minute: number, second: number): string => {
    return `${hour}:${getFormattedValue(minute)}:${getFormattedValue(second)}`;
};

export const getTimeStringIn12HoursFormat = (hour: number, minute: number, second: number, sign: string) => {
    return `${getTimeString(hour, minute, second)} ${sign}`;
};

/**
 * Get local or UTC time string in 12 hours format from a Date object. e.g. "2:00:59 AM"
 * @param date
 * @param isLocal
 * @returns
 */
export const getTimeIn12HoursFormatFromDate = (date?: Date, isLocal?: boolean): string => {
    if (!date) {
        return '';
    }

    let hour = isLocal ? date.getHours() : date.getUTCHours();
    const minute = isLocal ? date.getMinutes() : date.getUTCMinutes();
    const second = isLocal ? date.getSeconds() : date.getUTCSeconds();
    const sign = hour >= 12 ? 'PM' : 'AM';
    hour = hour % 12 === 0 ? 12 : hour % 12;
    return getTimeStringIn12HoursFormat(hour, minute, second, sign);
};

/**
 * Get hour, minute and second value from a 12 hour formatted time string
 * @param time
 * @returns
 */
export const getHourMinuteSecondFromTimeInput = (time: string) => {
    const timeSplit = time.split(' ');
    if (timeSplit.length === 2) {
        const isPM = timeSplit[1] === 'PM';
        const hourMinuteAndSecond = timeSplit[0].split(':');
        if (hourMinuteAndSecond.length === 3) {
            let hour = parseInt(hourMinuteAndSecond[0]);
            const minute = parseInt(hourMinuteAndSecond[1]);
            const second = parseInt(hourMinuteAndSecond[2]);
            if (!isNaN(hour) && !isNaN(minute) && !isNaN(second)) {
                if (hour === 12) {
                    hour = isPM ? hour : 0;
                } else if (isPM) {
                    hour += 12;
                }
                return {
                    hour: hour,
                    minute: minute,
                    second: second,
                };
            }
        }
    }

    return null;
};

export const getHourMinuteSecondFrom24HoursFormatTimeInput = (time: string) => {
    const match = time.match(/^[0-9]+[:][0-9]+[:][0-9]+$/);
    const hourMinuteAndSecond = time.trim().split(':');
    if (match && hourMinuteAndSecond.length === 3) {
        const hour = parseInt(hourMinuteAndSecond[0]);
        const minute = parseInt(hourMinuteAndSecond[1]);
        const second = parseInt(hourMinuteAndSecond[2]);
        if (!isNaN(hour) && !isNaN(minute) && !isNaN(second)) {
            return {
                hour: hour,
                minute: minute,
                second: second,
            };
        }
    }

    return null;
};

/**
 * Format the 12 hour formatted time input
 * @param time user input e,g, 10:08:08 AM
 * @returns formatted time string in 12 hour format, empty string if the input is invalid
 */
export const formatTimeStringTo12HoursFormat = (time: string) => {
    const timeArray = time.trim().split(' ');
    if (timeArray.length === 2) {
        let sign = timeArray[1].toLocaleUpperCase();
        if (sign === 'AM' || sign == 'PM') {
            const hourMinuteAndSecond = timeArray[0].split(':');
            if (hourMinuteAndSecond.length === 3) {
                let hour = parseInt(hourMinuteAndSecond[0]);
                const minute = parseInt(hourMinuteAndSecond[1]);
                const second = parseInt(hourMinuteAndSecond[2]);
                if (!isNaN(hour) && !isNaN(minute) && !isNaN(second)) {
                    if (hour >= 0 && hour < 24 && minute >= 0 && minute <= 59 && second >= 0 && second <= 59) {
                        if (hour === 0) {
                            hour = 12;
                        } else if (hour > 12) {
                            hour = hour % 12;
                            sign = 'PM';
                        }

                        return getTimeStringIn12HoursFormat(hour, minute, second, sign);
                    }
                }
            }
        }
    }

    return '';
};

export const formatTimeStringTo24HoursFormat = (time: string): string => {
    const formattedString = getHourMinuteSecondFrom24HoursFormatTimeInput(time);
    if (formattedString) {
        const { hour, minute, second } = formattedString;
        if (hour >= 0 && hour < 24 && minute >= 0 && minute <= 59 && second >= 0 && second <= 59) {
            return getTimeString(hour, minute, second);
        }
    }

    return '';
};

/**
 * Returns local date string with 12-hour time.
 * i.e. english: 'Saturday, November 2, 8:30 AM'
 */
export const getFullDateStringFromDate = (date: Date, locale?: string) => {
    const dateString = date.toLocaleDateString(locale, {
        weekday: 'long',
        day: 'numeric',
        month: 'long',
    });
    const time = getTimeIn12HoursFormatFromDate(date, true);
    const fullDateString = `${dateString}, ${time}`;
    return fullDateString;
};

export const getDateStringInUTC = (date: Date) => {
    const utcDate = shiftLocalDateToDateInUTCTime(date);
    return getFullDateStringFromDate(utcDate!);
};

export const getDateObjectFromDateAndTimeInput = (isLocal: boolean, date?: Date, time?: string, timeIn12Hours = true): Date => {
    if (!!date && !!time) {
        const year = date.getFullYear();
        const month = date.getMonth();
        const day = date.getDate();

        const hourMinuteSecond = timeIn12Hours
            ? getHourMinuteSecondFromTimeInput(time)
            : getHourMinuteSecondFrom24HoursFormatTimeInput(time);
        if (hourMinuteSecond) {
            const { hour, minute, second } = hourMinuteSecond;
            if (isLocal) {
                return new Date(year, month, day, hour, minute, second);
            } else {
                return new Date(Date.UTC(year, month, day, hour, minute, second));
            }
        }
    }

    return new Date();
};

export const shiftLocalDateToDateInUTCTime = (date?: Date) => {
    if (date) {
        return new Date(
            date.getUTCFullYear(),
            date.getUTCMonth(),
            date.getUTCDate(),
            date.getUTCHours(),
            date.getUTCMinutes(),
            date.getUTCSeconds()
        );
    }
    return undefined;
};

export const getDateAndTimeControlInput = (date = new Date(), isLocal = true) => {
    const dateValue = isLocal ? new Date(date) : date;

    const hour = isLocal ? date.getHours() : date.getUTCHours();
    const minute = isLocal ? date.getMinutes() : date.getUTCMinutes();
    const second = isLocal ? date.getSeconds() : date.getUTCSeconds();
    const timeValue = getTimeString(hour, minute, second);

    return {
        date: dateValue,
        time: timeValue,
    };
};

export function getDateAfterXSeconds(seconds: number) {
    const date = new Date();
    date.setSeconds(date.getSeconds() + seconds);
    return date;
}

export function formatDate(date: Date | string): string {
    if (!date) {
        return '';
    }

    const dateString = typeof date === 'string' ? date : date.toISOString();
    return dateString.replace(/(.+)\.\d+Z$/, '$1Z');
}

export function getFormattedDateTimeString(): string {
    const dateTime = new Date();
    const year = dateTime.getFullYear();
    const month = getFormattedValue(dateTime.getMonth() + 1);
    const day = getFormattedValue(dateTime.getDate());
    const hour = getFormattedValue(dateTime.getHours());
    const minute = getFormattedValue(dateTime.getMinutes());
    const second = getFormattedValue(dateTime.getSeconds());
    return `${year}${month}${day}${hour}${minute}${second}`;
}

export const formatDateTimeWithShortYear = (dateTime: Date): string => {
    const options: Intl.DateTimeFormatOptions = {
        month: 'numeric',
        day: 'numeric',
        year: '2-digit',
        hour: 'numeric',
        minute: '2-digit',
        hour12: true,
    };
    return dateTime.toLocaleDateString(undefined, options);
};

/**
 * Calculates the duration in milliseconds for predefined timespan options.
 * @param timeRangeKey - The timespan key (excluding 'Custom')
 * @returns The number of milliseconds for the specified timespan
 */
export const getTimespanInMilliseconds = (timeRangeKey: Omit<TimespanKeys, 'Custom'>): number => {
    const millisecondsInHour = 60 * 60 * 1000;
    const millisecondsInDay = 24 * millisecondsInHour;

    switch (timeRangeKey) {
        case TimespanKeys.OneHour:
            return millisecondsInHour;
        case TimespanKeys.SixHours:
            return 6 * millisecondsInHour;
        case TimespanKeys.TwelveHours:
            return 12 * millisecondsInHour;
        case TimespanKeys.TwentyFourHours:
            return millisecondsInDay;
        case TimespanKeys.ThreeDays:
            return 3 * millisecondsInDay;
        case TimespanKeys.SevenDays:
        default:
            return 7 * millisecondsInDay;
    }
};

/** Converts a `TimeRangeValue` to a Kusto timespan string */
export const getKustoTimespan = (timeRange: TimeRangeValue) => {
    if (timeRange.key === TimespanKeys.Custom) {
        const defaultedStartTime = timeRange.start ?? new Date();
        const defaultedEndTime = timeRange.end ?? new Date();
        return `between (datetime(${defaultedStartTime.toISOString()}) .. datetime(${defaultedEndTime.toISOString()}))`;
    } else {
        const test = getTimespanInMilliseconds(timeRange.key);
        return `> ago(${test}ms)`;
    }
};

/**
 * Formats a Date object to YYYY-MM-DD string format using local time components.
 * @param date - The date to format (optional)
 * @returns A string in YYYY-MM-DD format based on local time, or empty string if date is undefined
 */
export const formatDateToYYYYMMDD = (date?: Date): string => {
    if (!date) return '';
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
};

/**
 * Extracts only the date portion (year, month, day) from a DateTime object using local time.
 * Time components are set to midnight in the local timezone.
 * @param date - The datetime to extract date from (optional)
 * @returns A new Date object with only local date components at local midnight, or undefined
 */
export const extractDateFromDateTime = (date: Date | undefined): Date | undefined => {
    if (!date) return undefined;
    return new Date(date.getFullYear(), date.getMonth(), date.getDate());
};

/**
 * Combines separate date and time objects into a single DateTime object using local time components.
 * Takes the local date components from the first parameter and local time components from the second.
 * @param date - The date object containing local year, month, day (optional)
 * @param time - The time object containing local hours, minutes, seconds, milliseconds (optional)
 * @returns A new Date object combining both local date and time components, or undefined if either is missing
 */
export const getCombineDateAndTime = (date: Date | undefined, time: Date | undefined): Date | undefined => {
    if (!date || !time) return undefined;
    return new Date(
        date.getFullYear(),
        date.getMonth(),
        date.getDate(),
        time.getHours(),
        time.getMinutes(),
        time.getSeconds(),
        time.getMilliseconds()
    );
};

/**
 * Converts a UTC date to local timezone by interpreting UTC components as local time.
 * This effectively shifts the displayed time without changing the actual moment in time.
 * Input is treated as UTC, output represents the same numeric values as local time.
 * @param date - The UTC date to convert (optional)
 * @returns A new Date object with UTC components interpreted as local time, or undefined
 */
export const changeToLocalTimezone = (date: Date | undefined): Date | undefined => {
    if (!date) return undefined;
    return new Date(
        date.getUTCFullYear(),
        date.getUTCMonth(),
        date.getUTCDate(),
        date.getUTCHours(),
        date.getUTCMinutes(),
        date.getUTCSeconds(),
        date.getUTCMilliseconds()
    );
};

/**
 * Converts a local date to UTC timezone by interpreting local components as UTC time.
 * This effectively shifts the time to UTC without changing the displayed components.
 * Input is treated as local time, output represents the same numeric values as UTC.
 * @param date - The local date to convert (optional)
 * @returns A new Date object with local components interpreted as UTC time, or undefined
 */
export const changeToUtcTimezone = (date: Date | undefined): Date | undefined => {
    if (!date) return undefined;
    return new Date(
        Date.UTC(
            date.getFullYear(),
            date.getMonth(),
            date.getDate(),
            date.getHours(),
            date.getMinutes(),
            date.getSeconds(),
            date.getMilliseconds()
        )
    );
};

/**
 * @returns A locale formatted short date string or empty string when undefined
 *
 * Example (en-US): 8/4/25  |  Example (de-DE): 4.8.25
 */
export const formatShortDate = (date?: Date): string => {
    if (!date) return '';
    return date.toLocaleDateString(undefined, { month: 'numeric', day: 'numeric', year: '2-digit' });
};

/**
 * @param date A Date object
 * @returns A string formatted in local date and 2-digit hour:minute format
 */
export const getLocaleDateTimeHHMM = (date: Date) => {
    return `${date.toLocaleDateString()} ${date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}`;
};
