/**
 * API client for MCP connections
 */

import { getAgentHeaders } from '../../../../Common/Helpers/headers';

export interface McpTool {
    name: string;
    description?: string;
    inputSchema?: Record<string, unknown>;
}

export interface McpConnection {
    connectionId: string;
    name: string;
    type: string;
    endpoint: string;
    status: string;
    authenticationType?: string;
    toolCount?: number;
    tools?: McpTool[];
    description?: string;
    serviceType?: string;
    maxToolsPerConnection?: number; // Maximum tools that can be selected from this connection
}

/**
 * Fetches all MCP connections and their tools
 */
export async function listMcpConnections(baseUrl: string): Promise<McpConnection[]> {
    const url = `${baseUrl}/api/v1/mcp/connections/list`;

    const response = await fetch(url, {
        method: 'GET',
        headers: getAgentHeaders(),
    });

    if (!response.ok) {
        const errorText = await response.text();
        throw new Error(`Failed to fetch MCP connections: ${response.status} ${errorText}`);
    }

    const connections: McpConnection[] = await response.json();
    return connections;
}

/**
 * Gets all tool names from MCP connections
 */
export async function getMcpToolNames(baseUrl: string): Promise<string[]> {
    const connections = await listMcpConnections(baseUrl);

    const toolNames: string[] = [];
    connections.forEach(connection => {
        if (connection.tools && Array.isArray(connection.tools)) {
            connection.tools.forEach(tool => {
                if (tool.name) {
                    toolNames.push(tool.name);
                }
            });
        }
    });

    return toolNames;
}

/**
 * Gets MCP tools grouped by connection
 */
export interface McpToolsByConnection {
    connectionName: string;
    connectionId: string;
    serviceType?: string;
    maxToolsPerConnection?: number;
    tools: McpTool[];
}

export async function getMcpToolsByConnection(baseUrl: string): Promise<McpToolsByConnection[]> {
    const connections = await listMcpConnections(baseUrl);

    return connections.map(connection => ({
        connectionName: connection.name,
        connectionId: connection.connectionId,
        serviceType: connection.serviceType,
        maxToolsPerConnection: connection.maxToolsPerConnection,
        tools: connection.tools || [],
    }));
}
