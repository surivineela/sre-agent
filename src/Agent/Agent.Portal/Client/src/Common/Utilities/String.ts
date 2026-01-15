/**
 * Formats a string by replacing placeholders {0}, {1}, etc. with provided arguments
 * @param template The template string with placeholders like {0}, {1}, etc.
 * @param args The values to replace the placeholders with
 * @returns The formatted string
 * @example format("Hello {0}, you have {1} messages", "John", 5) => "Hello John, you have 5 messages"
 */
export function format(template: string, ...args: (string | number)[]): string {
    return template.replace(/{(\d+)}/g, (match, index) => {
        const argIndex = parseInt(index, 10);
        return args[argIndex] !== undefined ? String(args[argIndex]) : match;
    });
}

/**
 * Safely compares two values for sorting. Handles undefined, null, numbers, and strings.
 * @param a First value to compare
 * @param b Second value to compare
 * @returns Comparison result: negative if a < b, positive if a > b, 0 if equal
 */
export const safeCompare = (a: unknown, b: unknown): number => {
    const aVal = a ?? '';
    const bVal = b ?? '';

    // Handle numeric comparison
    if (typeof aVal === 'number' && typeof bVal === 'number') {
        return aVal - bVal;
    }

    // Convert to strings for comparison
    const aStr = String(aVal);
    const bStr = String(bVal);

    return aStr.localeCompare(bStr);
};
