export const getRiskLevel = (command: string): 'Safe' | 'Low' | 'Medium' | 'High' => {
    const cmd = command.toLowerCase();

    // High risk operations
    if (cmd.includes('delete') || cmd.includes('remove') || cmd.includes('purge')) return 'High';

    // Medium risk operations
    if (cmd.includes('create') || cmd.includes('update') || cmd.includes('set') || cmd.includes('scale') || cmd.includes('restart'))
        return 'Medium';

    // Low risk operations
    if (cmd.includes('start') || cmd.includes('stop') || cmd.includes('enable') || cmd.includes('disable')) return 'Low';

    // Safe operations (read-only)
    if (cmd.includes('list') || cmd.includes('show') || cmd.includes('get') || cmd.includes('describe')) return 'Safe';

    return 'Medium'; // Default
};

export const getRiskColor = (risk: string) => {
    switch (risk) {
        case 'Safe':
            return '#16a34a';
        case 'Low':
            return '#3b82f6';
        case 'Medium':
            return '#f59e0b';
        case 'High':
            return '#dc2626';
        default:
            return '#6b7280';
    }
};
