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
  
      const hourMinuteSecond = timeIn12Hours ? getHourMinuteSecondFromTimeInput(time) : getHourMinuteSecondFrom24HoursFormatTimeInput(time);
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
  