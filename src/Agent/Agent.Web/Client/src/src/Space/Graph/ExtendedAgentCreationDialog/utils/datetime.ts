export const toDateTimeLocalValue = (iso?: string): string => {
    if (!iso) {
        return '';
    }

    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
        return '';
    }

    const pad = (value: number) => value.toString().padStart(2, '0');
    const local = new Date(date.getTime() - date.getTimezoneOffset() * 60000);

    return `${local.getFullYear()}-${pad(local.getMonth() + 1)}-${pad(local.getDate())}T${pad(local.getHours())}:${pad(local.getMinutes())}`;
};

export const fromDateTimeLocalValue = (value: string): string | undefined => {
    if (!value) {
        return undefined;
    }

    const [datePart, timePart] = value.split('T');
    if (!datePart || !timePart) {
        return undefined;
    }

    const [year, month, day] = datePart.split('-').map(part => Number.parseInt(part, 10));
    const [hours, minutes] = timePart.split(':').map(part => Number.parseInt(part, 10));

    if ([year, month, day, hours, minutes].some(part => Number.isNaN(part))) {
        return undefined;
    }

    const date = new Date(year, (month ?? 1) - 1, day ?? 1, hours ?? 0, minutes ?? 0, 0, 0);
    if (Number.isNaN(date.getTime())) {
        return undefined;
    }

    return date.toISOString();
};
