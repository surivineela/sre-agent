import { ToolParameter } from '../../Contracts/ExtendedAgentGraph';

/**
 * Configuration for an auth scope with its environment variable name
 */
export interface AuthScopeConfig {
    scope: string;
    variableName: string;
}

export interface PythonToolFormProps {
    name: string;
    description: string;
    functionCode: string;
    timeoutSeconds: number;
    parameters: ToolParameter[];
    authEnabled: boolean;
    authScopes: AuthScopeConfig[];
}

export enum PythonToolDialogMode {
    Create,
    Edit,
}

export interface TestResult {
    success: boolean;
    result?: unknown;
    errorMessage?: string;
    stderr?: string;
    stdout?: string;
    executionTimeMs?: number;
    errorType?: string;
}

export type ToolTestStatus = 'idle' | 'running' | 'success' | 'error';
export type RightPanelTabValue = 'code' | 'test' | 'auth';

/**
 * Predefined scope presets for quick selection
 */
export const AUTH_SCOPE_PRESETS = [
    {
        id: 'arm',
        label: 'ARM',
        scope: 'https://management.azure.com/.default',
        description: 'Azure Resource Manager',
        defaultVariableName: 'AZURE_ARM_TOKEN',
    },
    {
        id: 'keyvault',
        label: 'Key Vault',
        scope: 'https://vault.azure.net/.default',
        description: 'Azure Key Vault',
        defaultVariableName: 'AZURE_KEYVAULT_TOKEN',
    },
    {
        id: 'storage',
        label: 'Storage',
        scope: 'https://storage.azure.com/.default',
        description: 'Azure Storage',
        defaultVariableName: 'AZURE_STORAGE_TOKEN',
    },
] as const;

/**
 * Default scope when auth is enabled
 */
export const DEFAULT_AUTH_SCOPE: AuthScopeConfig = {
    scope: 'https://management.azure.com/.default',
    variableName: 'AZURE_ARM_TOKEN',
};

/**
 * Generates a default variable name for a scope URL
 */
export function getDefaultVariableName(scope: string): string {
    const preset = AUTH_SCOPE_PRESETS.find(p => p.scope === scope);
    if (preset) {
        return preset.defaultVariableName;
    }

    // Generate name from scope URL: https://graph.microsoft.com/.default -> AZURE_GRAPH_TOKEN
    try {
        const hostname = new URL(scope).hostname;
        const serviceName = hostname
            .split('.')[0]
            .replace(/[^a-zA-Z0-9]/g, '_')
            .toUpperCase();
        return `AZURE_${serviceName}_TOKEN`;
    } catch {
        return 'AZURE_CUSTOM_TOKEN';
    }
}

/**
 * Creates an AuthScopeConfig from a scope URL with default variable name
 */
export function createAuthScopeConfig(scope: string, variableName?: string): AuthScopeConfig {
    return {
        scope,
        variableName: variableName ?? getDefaultVariableName(scope),
    };
}

/**
 * Converts an array of scope strings to AuthScopeConfig array (for loading from API)
 */
export function scopeStringsToConfigs(scopes: string[] | undefined): AuthScopeConfig[] {
    if (!scopes?.length) return [];
    return scopes.map(scope => createAuthScopeConfig(scope));
}

/**
 * Converts AuthScopeConfig array to scope strings (for saving to API)
 */
export function scopeConfigsToStrings(configs: AuthScopeConfig[]): string[] {
    return configs.map(c => c.scope);
}

/**
 * Gets the preset info for a scope URL, if it matches a known preset
 */
export function getScopePresetInfo(scope: string): { label: string; description: string } | null {
    const preset = AUTH_SCOPE_PRESETS.find(p => p.scope === scope);
    if (!preset) return null;
    return { label: preset.label, description: preset.description };
}

/**
 * Generates the auth snippet code for the Python tool showing all configured scopes
 */
export function getAuthSnippetCode(scopes: AuthScopeConfig[]): string {
    if (scopes.length === 0) return '';

    const tokenLines = scopes.map(s => `${s.variableName} = credential.get_token("${s.scope}").token`).join('\n');

    return `# Azure Identity Authentication
from azure.identity import DefaultAzureCredential

credential = DefaultAzureCredential()
${tokenLines}
`;
}

/**
 * Checks if the code already contains the auth snippet
 */
export function hasAuthSnippet(code: string): boolean {
    return code.includes('from azure.identity import DefaultAzureCredential');
}

/**
 * Inserts the auth snippet at the beginning of the code if not already present
 */
export function insertAuthSnippet(code: string, scopes: AuthScopeConfig[]): string {
    if (hasAuthSnippet(code) || scopes.length === 0) return code;
    return getAuthSnippetCode(scopes) + code;
}

/**
 * Default Python code template for new tools
 */
export const getDefaultCode = (): string => `def main() -> dict:
    """
    Your function description here.
    """
    # Your code here
    return {"result": "Hello, World!"}
`;

/**
 * Generates a fingerprint for the current tool state to track changes
 * Used to determine if re-testing is needed after edits
 */
export const getPythonToolFingerprint = (
    functionCode: string | undefined,
    parameters: ToolParameter[] | undefined,
    timeoutSeconds: number | undefined
): string => {
    const paramString = (parameters || [])
        .map(p => `${p.name}:${p.type}:${p.required}`)
        .sort()
        .join('|');
    return `${functionCode || ''}::${paramString}::${timeoutSeconds || 120}`;
};

/**
 * Validates that function code contains required main function
 */
export const hasValidMainFunction = (functionCode: string | undefined): boolean => {
    return !!functionCode?.includes('def main');
};

/**
 * Splits parameter string by comma, respecting nested brackets.
 * Handles complex types like Dict[str, int] or List[Tuple[int, str]].
 */
const splitParameters = (paramString: string): string[] => {
    const parts: string[] = [];
    let current = '';
    let bracketDepth = 0;

    for (const ch of paramString) {
        if (ch === '[' || ch === '{' || ch === '(') {
            bracketDepth++;
            current += ch;
        } else if (ch === ']' || ch === '}' || ch === ')') {
            bracketDepth--;
            current += ch;
        } else if (ch === ',' && bracketDepth === 0) {
            parts.push(current);
            current = '';
        } else {
            current += ch;
        }
    }

    if (current.length > 0) {
        parts.push(current);
    }

    return parts;
};

/**
 * Maps Python type hints to parameter types.
 */
const mapPythonType = (typeHint: string): string => {
    if (!typeHint?.trim()) {
        return 'string'; // Default
    }

    // Normalize type hint (remove Optional, whitespace)
    const normalized = typeHint
        .replace(/Optional\[/gi, '')
        .replace(/]/g, '')
        .replace(/\s/g, '')
        .toLowerCase();

    if (normalized.includes('int')) {
        return 'int';
    }
    if (normalized.includes('float') || normalized.includes('double')) {
        return 'double';
    }
    if (normalized.includes('bool')) {
        return 'bool';
    }
    if (normalized.includes('str') || normalized.includes('string')) {
        return 'string';
    }
    if (normalized.includes('dict') || normalized.includes('object')) {
        return 'object';
    }
    if (normalized.includes('list') || normalized.includes('array')) {
        return 'array';
    }

    return 'string';
};

/**
 * Converts snake_case to readable words.
 * Example: "max_retries" -> "Max retries"
 */
const convertSnakeCaseToWords = (snakeCase: string): string => {
    if (!snakeCase?.trim()) {
        return snakeCase;
    }

    const words = snakeCase.split('_');
    const capitalized = words.map((w, i) => (i === 0 && w.length > 0 ? w.charAt(0).toUpperCase() + w.slice(1) : w));

    return capitalized.join(' ');
};

/**
 * Generates a human-readable description from parameter information.
 */
const generateDescription = (name: string, typeHint: string, defaultValue: string | null): string => {
    const parts: string[] = [];
    const readableName = convertSnakeCaseToWords(name);

    if (typeHint?.trim()) {
        const cleanType = typeHint
            .replace(/Optional\[/gi, '')
            .replace(/]/g, '')
            .trim();
        parts.push(`${readableName} (${cleanType})`);
    } else {
        parts.push(readableName);
    }

    if (defaultValue && defaultValue !== 'None') {
        parts.push(`default: ${defaultValue}`);
    }

    return parts.join(' - ');
};

/**
 * Parses a single parameter definition.
 * Examples:
 * - "url: str"
 * - "timeout: int = 30"
 * - "enabled: bool = True"
 * - "data: Optional[Dict[str, Any]] = None"
 */
const parseParameter = (paramDef: string): ToolParameter | null => {
    if (!paramDef?.trim()) {
        return null;
    }

    // Pattern: name: type = default
    const match = paramDef.match(/^\s*(\w+)\s*(?::\s*(.+?))?\s*(?:=\s*(.+))?\s*$/);

    if (!match) {
        return null;
    }

    const name = match[1]?.trim() ?? '';
    const typeHint = match[2]?.trim() ?? '';
    const defaultValue = match[3]?.trim() ?? null;

    if (!name) {
        return null;
    }

    const required = !defaultValue;
    const type = mapPythonType(typeHint);
    const description = generateDescription(name, typeHint, defaultValue);

    return {
        name,
        type,
        description,
        required,
    };
};

/**
 * Extracts parameters from a Python function's 'def main(...)' signature.
 * Ported from Agent.Web/Utils/PythonSignatureParser.cs
 *
 * @param functionCode The Python function code
 * @returns Array of parameters with name, type, required status, and description
 */
export const extractParametersFromCode = (functionCode: string | undefined): ToolParameter[] => {
    if (!functionCode?.trim()) {
        return [];
    }

    // Match: def main(param1: str, param2: int = 10, param3: Optional[str] = None)
    // Handles multi-line signatures with 's' flag (dotall)
    const mainFunctionMatch = functionCode.match(/def\s+main\s*\((.*?)\)\s*(?:->.*?)?:/is);

    if (!mainFunctionMatch) {
        return [];
    }

    const paramString = mainFunctionMatch[1]?.trim() ?? '';
    if (!paramString) {
        return []; // No parameters
    }

    const paramParts = splitParameters(paramString);
    const parameters: ToolParameter[] = [];

    for (const part of paramParts) {
        const param = parseParameter(part.trim());
        if (param) {
            parameters.push(param);
        }
    }

    return parameters;
};
