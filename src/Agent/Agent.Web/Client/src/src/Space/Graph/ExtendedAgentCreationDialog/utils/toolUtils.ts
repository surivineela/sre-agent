import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';

const safeString = (value?: string | null): string => (value ?? '').trim();

export const getKustoTestFingerprint = (tool?: Partial<ExtendedTool> | null): string | null => {
    if (!tool) {
        return null;
    }

    const connector = safeString(tool.connector);
    const database = safeString(tool.database);
    const query = safeString(tool.query);
    const parameters = (tool.parameters ?? [])
        .map((parameter: any) => ({
            name: safeString(parameter.name),
            type: safeString(parameter.type),
            required: parameter.required !== false,
        }))
        .sort((left: any, right: any) => left.name.localeCompare(right.name));

    const fingerprint = JSON.stringify({ connector, database, query, parameters });
    return fingerprint === JSON.stringify({ connector: '', database: '', query: '', parameters: [] }) ? null : fingerprint;
};
