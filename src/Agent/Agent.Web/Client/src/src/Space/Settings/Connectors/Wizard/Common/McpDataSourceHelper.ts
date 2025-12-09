/**
 * Helper module for parsing and creating MCP data source strings
 */

import { ITelemetryInfo } from '../../../../../Common/AzPortalProxy/Models/ITelemetryInfo';

/**
 * Enum for MCP local data source field types
 */
export enum McpLocalDataSourceField {
    Type = 'Type',
    Command = 'Command',
    ArgsJson = 'ArgsJson',
    EnvJson = 'EnvJson',
}

/**
 * Parsed MCP local data source
 */
export interface ParsedMcpLocalDataSource {
    type?: string;
    command?: string;
    args?: string[];
    env?: Record<string, string>;
}

/**
 * Parse an MCP local data source string into a structured object
 * Supports both JSON format (new) and semicolon-delimited format (legacy)
 * @param dataSource - The data source string (JSON or Type=stdio;Command=...;ArgsJson=...;EnvJson=...)
 * @param log - Optional logging function for error reporting
 * @returns Parsed data source object
 */
export const parseMcpLocalDataSource = (dataSource: string, log?: (info: ITelemetryInfo) => void): ParsedMcpLocalDataSource => {
    const result: ParsedMcpLocalDataSource = {};

    // Try JSON format first (new format)
    if (dataSource.trimStart().startsWith('{')) {
        try {
            const parsed = JSON.parse(dataSource);

            if (parsed.Type) {
                result.type = parsed.Type;
            }

            if (parsed.Command) {
                result.command = parsed.Command;
            }

            // Args might be already parsed or still in JSON string format
            if (parsed.ArgsJson) {
                try {
                    result.args = typeof parsed.ArgsJson === 'string' ? JSON.parse(parsed.ArgsJson) : parsed.ArgsJson;
                } catch (e) {
                    log?.({
                        action: 'parseMcpLocalDataSource',
                        actionModifier: 'ArgsJson parse failed',
                        logLevel: 'error',
                        data: {
                            message: 'Failed to parse ArgsJson from MCP data source',
                            argsJson: parsed.ArgsJson,
                            error: e instanceof Error ? e.message : String(e),
                        },
                    });
                    result.args = [];
                }
            }

            // Env might be already parsed or still in JSON string format
            if (parsed.EnvJson) {
                try {
                    result.env = typeof parsed.EnvJson === 'string' ? JSON.parse(parsed.EnvJson) : parsed.EnvJson;
                } catch (e) {
                    log?.({
                        action: 'parseMcpLocalDataSource',
                        actionModifier: 'EnvJson parse failed',
                        logLevel: 'error',
                        data: {
                            message: 'Failed to parse EnvJson from MCP data source',
                            envJson: parsed.EnvJson,
                            error: e instanceof Error ? e.message : String(e),
                        },
                    });
                    result.env = {};
                }
            }

            return result;
        } catch (e) {
            log?.({
                action: 'parseMcpLocalDataSource',
                actionModifier: 'JSON parse failed',
                logLevel: 'error',
                data: {
                    message: 'Failed to parse MCP data source as JSON',
                    dataSource,
                    error: e instanceof Error ? e.message : String(e),
                },
            });
            // Fall through to legacy parsing
        }
    }

    // Legacy semicolon-delimited format
    const parts = dataSource.split(';');

    // Create a map for O(1) lookups
    const partsMap = new Map<string, string>();
    parts.forEach(part => {
        const separatorIndex = part.indexOf('=');
        if (separatorIndex !== -1) {
            const key = part.substring(0, separatorIndex);
            const value = part.substring(separatorIndex + 1);
            partsMap.set(key, value);
        }
    });

    // Type
    const type = partsMap.get(McpLocalDataSourceField.Type);
    if (type) {
        result.type = type;
    }

    // Command
    const command = partsMap.get(McpLocalDataSourceField.Command);
    if (command) {
        result.command = command;
    }

    // Args
    const argsJson = partsMap.get(McpLocalDataSourceField.ArgsJson);
    if (argsJson) {
        try {
            result.args = JSON.parse(argsJson) as string[];
        } catch (e) {
            log?.({
                action: 'parseMcpLocalDataSource',
                actionModifier: 'ArgsJson parse failed',
                logLevel: 'error',
                data: {
                    message: 'Failed to parse ArgsJson from MCP data source',
                    argsJson,
                    error: e instanceof Error ? e.message : String(e),
                },
            });
            result.args = [];
        }
    }

    // Env
    const envJson = partsMap.get(McpLocalDataSourceField.EnvJson);
    if (envJson) {
        try {
            result.env = JSON.parse(envJson) as Record<string, string>;
        } catch (e) {
            log?.({
                action: 'parseMcpLocalDataSource',
                actionModifier: 'EnvJson parse failed',
                logLevel: 'error',
                data: {
                    message: 'Failed to parse EnvJson from MCP data source',
                    envJson,
                    error: e instanceof Error ? e.message : String(e),
                },
            });
            result.env = {};
        }
    }

    return result;
};

/**
 * Create ExtendedProperties object for MCP local (stdio) connections
 * @param command - The command to execute
 * @param args - Optional command arguments
 * @param env - Optional environment variables
 * @returns ExtendedProperties object with type, command, args (as array), and envs (as object)
 */
export const createMcpLocalExtendedProperties = (command: string, args?: string[], env?: Record<string, string>): Record<string, any> => {
    const properties: Record<string, any> = {
        type: 'stdio',
        command: command,
    };

    if (args && args.length > 0) {
        properties.args = args;
    }

    if (env && Object.keys(env).length > 0) {
        properties.envs = env;
    }

    return properties;
};

/**
 * Create ExtendedProperties object for MCP remote (HTTP) connections
 * @param endpoint - The HTTP endpoint URL
 * @param authType - Authentication type (BearerToken or CustomHeaders)
 * @param bearerToken - Bearer token (if authType is BearerToken)
 * @param customHeaders - Custom headers as key-value pairs (if authType is CustomHeaders)
 * @returns ExtendedProperties object with type, endpoint, authType, and authentication details
 */
export const createMcpRemoteExtendedProperties = (
    endpoint: string,
    authType?: string,
    bearerToken?: string,
    customHeaders?: Array<{ key: string; value: string }>
): Record<string, any> => {
    const properties: Record<string, any> = {
        type: 'http',
        endpoint: endpoint,
    };

    if (authType) {
        properties.authType = authType;

        if (authType === 'BearerToken' && bearerToken) {
            properties.bearerToken = bearerToken;
        } else if (authType === 'CustomHeaders' && customHeaders) {
            // Add each custom header as a property
            customHeaders.forEach(header => {
                if (header.key && header.value) {
                    properties[header.key] = header.value;
                }
            });
        }
    }

    return properties;
};

/**
 * Parsed MCP remote data source (SSE connection)
 */
export interface ParsedMcpRemoteDataSource {
    endpoint?: string;
    authType?: string;
    bearerToken?: string;
    customHeaders?: Array<{ key: string; value: string }>;
}

/**
 * Parse an MCP remote data source string into a structured object
 * @param dataSource - The data source string in format: Endpoint=...;AuthType=...;BearerToken=... or Endpoint=...;AuthType=CustomHeaders;Header1=Value1;...
 * @returns Parsed data source object
 */
export const parseMcpRemoteDataSource = (dataSource: string): ParsedMcpRemoteDataSource => {
    const parts = dataSource.split(';');

    // Create a map for O(1) lookups
    const partsMap = new Map<string, string>();
    parts.forEach(part => {
        const separatorIndex = part.indexOf('=');
        if (separatorIndex !== -1) {
            const key = part.substring(0, separatorIndex);
            const value = part.substring(separatorIndex + 1);
            partsMap.set(key, value);
        }
    });

    const result: ParsedMcpRemoteDataSource = {};

    // Endpoint
    const endpoint = partsMap.get('Endpoint');
    if (endpoint) {
        result.endpoint = endpoint;
    }

    // AuthType
    const authType = partsMap.get('AuthType');
    if (authType) {
        result.authType = authType;
    }

    // BearerToken (if AuthType is BearerToken)
    if (authType === 'BearerToken') {
        const bearerToken = partsMap.get('BearerToken');
        if (bearerToken) {
            result.bearerToken = bearerToken;
        }
    }

    // CustomHeaders (if AuthType is CustomHeaders)
    if (authType === 'CustomHeaders') {
        const customHeaders: Array<{ key: string; value: string }> = [];
        partsMap.forEach((value, key) => {
            if (key !== 'Endpoint' && key !== 'AuthType') {
                customHeaders.push({ key, value });
            }
        });
        result.customHeaders = customHeaders;
    }

    return result;
};
