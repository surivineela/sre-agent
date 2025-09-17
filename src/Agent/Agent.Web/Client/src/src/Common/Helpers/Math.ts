/**
 * Calculates the percent change in an array (must be pre-sorted; uses the first and last values; values must be numbers)
 *
 * @returns Percent change as a rounded whole number (Ex: 0.275 -> 28%)
 */
export const getPercentChangeInArray = <T>(array: T[], valueKey: keyof T): number => {
    if (array.length < 2) {
        return 0;
    }

    const firstValue = array[0][valueKey];
    const lastValue = array[array.length - 1][valueKey];

    if (firstValue === undefined || typeof firstValue !== 'number' || lastValue === undefined || typeof lastValue !== 'number') {
        return 0;
    }

    const percentChange = ((lastValue - firstValue) / (firstValue === 0 ? 1 : firstValue)) * 100;
    return Math.round(percentChange);
};
