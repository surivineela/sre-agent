/**
 * Deduplicates an array of objects by their `id` property, keeping the first occurrence.
 *
 * @param items - Array of objects with an `id` string property
 * @returns A new array with duplicates removed
 */
export const dedupeById = <T extends { id: string }>(items: T[]): T[] => {
    const seen = new Set<string>();
    return items.filter(item => {
        if (seen.has(item.id)) return false;
        seen.add(item.id);
        return true;
    });
};
