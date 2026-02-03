/**
 * Represents a Task tool execution that spawns subagents for specialized tasks.
 * The Task tool can run multiple subagents in parallel (Explore, Plan, CodeReview).
 */
export interface TaskToolExecution {
    /** Unique identifier for this execution */
    id: string;
    /** Short description of what the subagent will do (3-5 words) */
    description: string;
    /** The type of subagent being spawned */
    subagentType: SubagentType;
    /** The task prompt given to the subagent */
    prompt: string;
    /** Current execution status */
    status: TaskToolExecutionStatus;
    /** When the execution started */
    startedAt: string;
    /** When the execution completed (if finished) */
    completedAt?: string;
    /** The result/response from the subagent (if completed) */
    result?: string;
    /** Error message if the execution failed */
    error?: string;
    /** Tool invocations made by this subagent (real-time streaming only) */
    toolInvocations?: SubagentToolInvocation[];
}

/**
 * Represents a tool invocation made by a subagent during execution.
 */
export interface SubagentToolInvocation {
    /** Tool name (e.g., "ReadFile", "GrepSearch") */
    toolName: string;
    /** Brief description of the tool call */
    description?: string;
    /** Status of this tool invocation */
    status: 'Running' | 'Completed' | 'Failed';
    /** When the tool invocation started */
    startedAt: string;
    /** When the tool invocation completed */
    completedAt?: string;
    /** Truncated output from the tool (max 500 chars) */
    output?: string;
}

/**
 * Available subagent types for the Task tool.
 */
export type SubagentType = 'Explore' | 'Plan' | 'CodeReview' | 'KustoQuery' | 'Bash';

/**
 * Status of a Task tool execution.
 */
export type TaskToolExecutionStatus = 'Pending' | 'Running' | 'Completed' | 'Failed' | 'Cancelled';

/**
 * Configuration for how to display each subagent type in the UI.
 */
export interface SubagentDisplayConfig {
    /** Icon identifier for the subagent type */
    icon: 'Search' | 'Architect' | 'Code' | 'Database' | 'Terminal';
    /** Color scheme for the card */
    colorScheme: 'explore' | 'plan' | 'review' | 'kusto' | 'bash';
    /** User-friendly label for the subagent type */
    label: string;
    /** Description of what this subagent type does */
    description: string;
}

/**
 * Gets the display configuration for a subagent type.
 */
export const getSubagentDisplayConfig = (type: SubagentType): SubagentDisplayConfig => {
    switch (type) {
        case 'Explore':
            return {
                icon: 'Search',
                colorScheme: 'explore',
                label: 'Explore',
                description: 'Analyzing codebase features, tracing execution paths, and mapping architecture.',
            };
        case 'Plan':
            return {
                icon: 'Architect',
                colorScheme: 'plan',
                label: 'Plan',
                description: 'Designing feature architecture with implementation blueprints.',
            };
        case 'CodeReview':
            return {
                icon: 'Code',
                colorScheme: 'review',
                label: 'Review',
                description: 'Reviewing code for bugs, security issues, and quality problems.',
            };
        case 'KustoQuery':
            return {
                icon: 'Database',
                colorScheme: 'kusto',
                label: 'Kusto',
                description: 'Executing KQL queries and analyzing data from Azure Data Explorer.',
            };
        case 'Bash':
            return {
                icon: 'Terminal',
                colorScheme: 'bash',
                label: 'Bash',
                description: 'Running bash commands for git operations and terminal tasks.',
            };
    }
};

/**
 * Represents a group of parallel Task tool executions.
 * When multiple subagents are spawned in parallel, they're grouped together.
 */
export interface TaskToolExecutionGroup {
    /** Unique identifier for this group */
    id: string;
    /** Individual subagent executions in this group */
    executions: TaskToolExecution[];
    /** Whether all executions in the group have completed */
    isComplete: boolean;
    /** When the group started (earliest startedAt) */
    startedAt: string;
    /** When the group completed (latest completedAt) */
    completedAt?: string;
}
