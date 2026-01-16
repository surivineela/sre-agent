/**
 * Represents an MCP (Model Context Protocol) tool execution with its parameters and status.
 */
export interface McpToolExecution {
    /** Unique identifier for this execution */
    id: string;
    /** The full tool name (e.g., "kusto-mcp_kusto_query") */
    fullToolName: string;
    /** The MCP server name (e.g., "kusto-mcp") */
    mcpServerName: string;
    /** The tool name without the MCP prefix (e.g., "kusto_query") */
    toolName: string;
    /** User-friendly display name for the tool (e.g., "Kusto Query") */
    displayName: string;
    /** Current execution status */
    status: McpToolExecutionStatus;
    /** When the execution started */
    startedAt: string;
    /** When the execution completed (if finished) */
    completedAt?: string;
    /** Structured parameters for the tool execution */
    parameters?: McpToolParameters;
    /** The result of the execution (if completed) */
    result?: string;
    /** Error message if the execution failed */
    error?: string;
}

/**
 * Status of an MCP tool execution.
 */
export type McpToolExecutionStatus = 'Running' | 'Completed' | 'Failed' | 'Cancelled';

/**
 * Parameters for an MCP tool execution, structured by tool type.
 */
export interface McpToolParameters {
    /** Raw parameters dictionary for any tool */
    raw?: Record<string, unknown>;

    // Kusto-specific parameters
    /** Kusto cluster URL (for Kusto tools) */
    clusterUrl?: string;
    /** Kusto database name (for Kusto tools) */
    database?: string;
    /** KQL query (for kusto_query tool) */
    query?: string;
    /** Table name (for kusto_sample, kusto_table_schema tools) */
    tableName?: string;
    /** Sample size (for kusto_sample tool) */
    sampleSize?: number;

    // ADO-specific parameters
    /** Repository name (for ADO tools) */
    repository?: string;
    /** Source branch (for PR creation) */
    sourceBranch?: string;
    /** Target branch (for PR creation) */
    targetBranch?: string;
    /** Pull request title */
    title?: string;
    /** Pull request description */
    description?: string;
}

/**
 * Configuration for how to display MCP tools in the UI.
 */
export interface McpToolDisplayConfig {
    /** Icon name for the tool type */
    icon: string;
    /** Color scheme for the card */
    colorScheme: 'kusto' | 'ado' | 'generic';
    /** Whether to show a code editor for query-type parameters */
    showQueryEditor: boolean;
    /** Language for syntax highlighting (e.g., 'kql', 'sql') */
    queryLanguage?: string;
}

/**
 * Gets the display configuration for an MCP tool based on its name.
 */
export const getMcpToolDisplayConfig = (mcpServerName: string, toolName: string): McpToolDisplayConfig => {
    // Kusto MCP tools
    if (mcpServerName === 'kusto-mcp') {
        return {
            icon: 'Database',
            colorScheme: 'kusto',
            showQueryEditor: toolName === 'kusto_query',
            queryLanguage: 'kql',
        };
    }

    // Azure DevOps MCP tools
    if (mcpServerName === 'ado-mcp') {
        return {
            icon: 'GitPullRequest',
            colorScheme: 'ado',
            showQueryEditor: false,
        };
    }

    // Default/generic MCP tools
    return {
        icon: 'Puzzle',
        colorScheme: 'generic',
        showQueryEditor: false,
    };
};

/**
 * Parses an MCP tool name into its components.
 * Format: "{mcp-server-name}_{tool_name}" (e.g., "kusto-mcp_kusto_query")
 */
export const parseMcpToolName = (fullToolName: string): { mcpServerName: string; toolName: string; displayName: string } => {
    const firstUnderscoreIndex = fullToolName.indexOf('_');

    if (firstUnderscoreIndex === -1) {
        return {
            mcpServerName: fullToolName,
            toolName: fullToolName,
            displayName: formatDisplayName(fullToolName),
        };
    }

    const mcpServerName = fullToolName.substring(0, firstUnderscoreIndex);
    const toolName = fullToolName.substring(firstUnderscoreIndex + 1);

    return {
        mcpServerName,
        toolName,
        displayName: formatDisplayName(toolName),
    };
};

/**
 * Formats a tool name into a user-friendly display name.
 * e.g., "kusto_query" -> "Kusto Query"
 */
const formatDisplayName = (toolName: string): string => {
    return toolName
        .replace(/_/g, ' ')
        .replace(/-/g, ' ')
        .split(' ')
        .map(word => word.charAt(0).toUpperCase() + word.slice(1).toLowerCase())
        .join(' ');
};
