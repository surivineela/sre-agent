export const ConnectorTypeOptions = [
    { id: 'Kusto', label: 'Kusto' },
    { id: 'ICM', label: 'ICM' },
] as const;

export type ConnectorType = (typeof ConnectorTypeOptions)[number]['id'];

export const KustoDataSourceExample = 'https://cluster-url/database-name';

/**
 * Validates a Kusto data source URL format
 * Expected format: https://cluster-url/database-name
 */
export const isValidKustoDataSource = (value: string): boolean => {
    if (!value) return false;

    try {
        const url = new URL(value);
        return url.protocol === 'https:' && !!url.host.trim() && !!url.pathname && url.pathname.trim() !== '/';
    } catch {
        return false;
    }
};
