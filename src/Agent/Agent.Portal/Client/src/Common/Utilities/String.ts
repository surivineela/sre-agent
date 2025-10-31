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
