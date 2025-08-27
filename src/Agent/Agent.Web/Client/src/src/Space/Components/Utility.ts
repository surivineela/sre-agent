enum ExecutionRiskLevel {
    Safe = 'Safe',
    Low = 'Low',
    Medium = 'Medium',
    High = 'High',
}

export const getRiskLevel = (command: string): ExecutionRiskLevel => {
    const cmd = command.toLowerCase();

    // High risk operations
    if (cmd.includes('delete') || cmd.includes('remove') || cmd.includes('purge')) return ExecutionRiskLevel.High;

    // Medium risk operations
    if (cmd.includes('create') || cmd.includes('update') || cmd.includes('set') || cmd.includes('scale') || cmd.includes('restart'))
        return ExecutionRiskLevel.Medium;

    // Low risk operations
    if (cmd.includes('start') || cmd.includes('stop') || cmd.includes('enable') || cmd.includes('disable')) return ExecutionRiskLevel.Low;

    // Safe operations (read-only)
    if (cmd.includes('list') || cmd.includes('show') || cmd.includes('get') || cmd.includes('describe')) return ExecutionRiskLevel.Safe;

    return ExecutionRiskLevel.Medium;
};

export const getRiskColor = (risk: ExecutionRiskLevel) => {
    switch (risk) {
        case ExecutionRiskLevel.Safe:
            return 'success';
        case ExecutionRiskLevel.Low:
            return 'brand';
        case ExecutionRiskLevel.Medium:
            return 'warning';
        case ExecutionRiskLevel.High:
            return 'danger';
        default:
            return 'informative';
    }
};

/** Ex: 7/15/25, 1:00:00 PM */
export const formatTimestampShort = (value: string | number | Date): string => {
    const dt = new Date(value);
    const locale = typeof navigator !== 'undefined' && navigator.language ? navigator.language : 'en-US';

    return new Intl.DateTimeFormat(locale, {
        year: '2-digit',
        month: 'numeric',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
    }).format(dt);
};
