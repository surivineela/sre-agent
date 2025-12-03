import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';

const safeString = (value?: string | null): string => (value ?? '').trim();

export const getKustoTestFingerprint = (tool?: Partial<ExtendedTool> | null): string | null => {
    if (!tool) {
        return null;
    }

    const toolType = safeString(tool.type);

    // Python tool fingerprint
    if (toolType === 'PythonFunctionTool') {
        const functionCode = safeString(tool.functionCode);
        const parameters = (tool.parameters ?? [])
            .map((parameter: any) => ({
                name: safeString(parameter.name),
                type: safeString(parameter.type),
                required: parameter.required !== false,
            }))
            .sort((left: any, right: any) => left.name.localeCompare(right.name));

        const fingerprint = JSON.stringify({ type: 'python', functionCode, parameters });
        return fingerprint === JSON.stringify({ type: 'python', functionCode: '', parameters: [] }) ? null : fingerprint;
    }

    // Kusto tool fingerprint
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

    const fingerprint = JSON.stringify({ type: 'kusto', connector, database, query, parameters });
    return fingerprint === JSON.stringify({ type: 'kusto', connector: '', database: '', query: '', parameters: [] }) ? null : fingerprint;
};
