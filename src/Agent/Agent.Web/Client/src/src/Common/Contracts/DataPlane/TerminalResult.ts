/**
 * Status of a terminal command execution.
 */
export type TerminalStatus = 'Running' | 'Completed' | 'Failed' | 'Background';

/**
 * Structured result from a terminal command execution for rich UI rendering.
 */
export interface TerminalExecutionResult {
    /** The command that was executed */
    command: string;
    /** Human-readable explanation of what the command does */
    explanation?: string;
    /** Whether this command runs in the background */
    isBackground: boolean;
    /** Session ID for background commands */
    sessionId?: string;
    /** Exit code (null for background/running commands) */
    exitCode?: number;
    /** Command stdout output */
    output?: string;
    /** Command stderr output */
    error?: string;
    /** Current status of the execution */
    status: TerminalStatus;
}
