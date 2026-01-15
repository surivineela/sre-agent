import { GrepSearchResult } from '../../../Common/Contracts/DataPlane/GrepResult';
import { ReadFileResult } from '../../../Common/Contracts/DataPlane/ReadFileResult';
import { TerminalExecutionResult } from '../../../Common/Contracts/DataPlane/TerminalResult';

/**
 * Supported tool types for the unified ToolCallCard component.
 */
export type ToolType = 'grep' | 'readfile' | 'terminal';

/**
 * Summary information displayed in the collapsed state.
 */
export interface ToolSummary {
    /** Icon component to display */
    icon: React.ReactNode;
    /** Primary action text (e.g., "Searched for", "Read", "Ran") */
    actionText: string;
    /** Key parameter (e.g., query, filename, command) */
    keyParam: string;
    /** Result summary (e.g., "5 matches", "lines 1-50", "exit 0") */
    resultInfo: string;
    /** Whether this represents an error state */
    isError?: boolean;
}

/**
 * Props for the unified ToolCallCard component.
 */
export interface ToolCallCardProps {
    /** Type of tool being displayed */
    toolType: ToolType;

    /** Grep search result (when toolType === 'grep') */
    grepResult?: GrepSearchResult;

    /** File read result (when toolType === 'readfile') */
    readFileResult?: ReadFileResult;

    /** Terminal execution result (when toolType === 'terminal') */
    terminalResult?: TerminalExecutionResult;

    /** Whether the tool is currently executing */
    isLoading?: boolean;

    /** Controlled expansion state */
    isExpanded?: boolean;

    /** Callback when expansion state changes */
    onExpandChange?: (expanded: boolean) => void;
}

/**
 * Props for the reusable SummaryLine component.
 */
export interface SummaryLineProps {
    /** Summary information to display */
    summary: ToolSummary;

    /** Whether the card is expanded */
    isExpanded: boolean;

    /** Whether the tool is currently loading/executing */
    isLoading?: boolean;

    /** Whether there are results to expand (disables click if false) */
    hasContent: boolean;

    /** Click handler for expand/collapse */
    onClick: () => void;
}

/**
 * Props for tool-specific content renderers.
 */
export interface GrepToolContentProps {
    result: GrepSearchResult;
}

export interface ReadFileToolContentProps {
    result: ReadFileResult;
}

export interface TerminalToolContentProps {
    result: TerminalExecutionResult;
}
