import { makeStyles } from '@fluentui/react-components';
import { Document16Regular, Search16Regular, WindowConsole20Regular } from '@fluentui/react-icons';
import { memo, useMemo, useState } from 'react';
import { GrepSearchResult } from '../../../Common/Contracts/DataPlane/GrepResult';
import { ReadFileResult } from '../../../Common/Contracts/DataPlane/ReadFileResult';
import { TerminalExecutionResult } from '../../../Common/Contracts/DataPlane/TerminalResult';
import GrepToolContent from './GrepToolContent';
import ReadFileToolContent from './ReadFileToolContent';
import SummaryLine from './SummaryLine';
import TerminalToolContent from './TerminalToolContent';
import { ToolCallCardProps, ToolSummary } from './types';
import { useToolCallStyles } from './useToolCallStyles';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
});

/**
 * Extracts just the filename from a path.
 */
const getFileName = (filePath: string): string => {
    const parts = filePath.split(/[/\\]/);
    return parts[parts.length - 1] || filePath;
};

/**
 * Truncates a string to a maximum length with ellipsis.
 */
const truncate = (str: string, maxLen: number): string => {
    if (str.length <= maxLen) return str;
    return str.slice(0, maxLen - 1) + '…';
};

/**
 * Generates summary information for grep results.
 */
const getGrepSummary = (result: GrepSearchResult): ToolSummary => {
    const hasResults = result.totalMatches > 0;
    return {
        icon: <Search16Regular />,
        actionText: 'Searched for',
        keyParam: `"${truncate(result.query, 40)}"`,
        resultInfo: hasResults ? `${result.totalMatches} ${result.totalMatches === 1 ? 'match' : 'matches'}` : 'no matches',
        isError: false,
    };
};

/**
 * Generates summary information for file read results.
 */
const getReadFileSummary = (result: ReadFileResult): ToolSummary => {
    const hasError = !!result.error;
    const fileName = getFileName(result.filePath);

    if (hasError) {
        return {
            icon: <Document16Regular />,
            actionText: 'Read',
            keyParam: fileName,
            resultInfo: 'error',
            isError: true,
        };
    }

    const lineInfo =
        result.startLine === 1 && result.endLine >= result.totalLines
            ? `${result.totalLines} lines`
            : `lines ${result.startLine}-${result.endLine}`;

    return {
        icon: <Document16Regular />,
        actionText: 'Read',
        keyParam: fileName,
        resultInfo: lineInfo,
        isError: false,
    };
};

/**
 * Generates summary information for terminal execution results.
 */
const getTerminalSummary = (result: TerminalExecutionResult): ToolSummary => {
    const cmdDisplay = truncate(result.command, 40);

    if (result.isBackground) {
        return {
            icon: <WindowConsole20Regular />,
            actionText: 'Started',
            keyParam: cmdDisplay,
            resultInfo: 'background',
            isError: false,
        };
    }

    if (result.status === 'Failed' || (result.exitCode !== undefined && result.exitCode !== 0)) {
        return {
            icon: <WindowConsole20Regular />,
            actionText: 'Ran',
            keyParam: cmdDisplay,
            resultInfo: result.exitCode !== undefined ? `exit ${result.exitCode}` : 'failed',
            isError: true,
        };
    }

    if (result.status === 'Running') {
        return {
            icon: <WindowConsole20Regular />,
            actionText: 'Running',
            keyParam: cmdDisplay,
            resultInfo: '...',
            isError: false,
        };
    }

    return {
        icon: <WindowConsole20Regular />,
        actionText: 'Ran',
        keyParam: cmdDisplay,
        resultInfo: `exit ${result.exitCode ?? 0}`,
        isError: false,
    };
};

/**
 * Unified tool call card component.
 *
 * Displays tool execution results in a consistent, collapsible format:
 * - Collapsed: Single summary line with icon, action, key param, and result
 * - Expanded: Full content with tool-specific rendering
 *
 * Supports grep search, file read, and terminal execution results.
 */
const ToolCallCard = ({
    toolType,
    grepResult,
    readFileResult,
    terminalResult,
    isLoading = false,
    isExpanded: controlledExpanded,
    onExpandChange,
}: ToolCallCardProps) => {
    const classes = useStyles();
    const sharedClasses = useToolCallStyles();

    // Internal expansion state (used when not controlled)
    const [internalExpanded, setInternalExpanded] = useState(false);

    // Use controlled state if provided, otherwise use internal state
    const isExpanded = controlledExpanded !== undefined ? controlledExpanded : internalExpanded;

    const handleExpandChange = (expanded: boolean) => {
        if (onExpandChange) {
            onExpandChange(expanded);
        } else {
            setInternalExpanded(expanded);
        }
    };

    // Generate summary based on tool type
    const summary = useMemo((): ToolSummary => {
        switch (toolType) {
            case 'grep':
                return getGrepSummary(grepResult!);
            case 'readfile':
                return getReadFileSummary(readFileResult!);
            case 'terminal':
                return getTerminalSummary(terminalResult!);
        }
    }, [toolType, grepResult, readFileResult, terminalResult]);

    // Determine if there's content to expand
    const hasContent = useMemo((): boolean => {
        switch (toolType) {
            case 'grep':
                return (grepResult?.files?.length ?? 0) > 0;
            case 'readfile':
                return !!readFileResult?.content || !!readFileResult?.error;
            case 'terminal':
                return !!terminalResult?.output || !!terminalResult?.error || terminalResult?.isBackground === true;
        }
    }, [toolType, grepResult, readFileResult, terminalResult]);

    // Render tool-specific content
    const renderContent = () => {
        switch (toolType) {
            case 'grep':
                return <GrepToolContent result={grepResult!} />;
            case 'readfile':
                return <ReadFileToolContent result={readFileResult!} />;
            case 'terminal':
                return <TerminalToolContent result={terminalResult!} />;
        }
    };

    return (
        <div className={classes.root}>
            <SummaryLine
                summary={summary}
                isExpanded={isExpanded}
                isLoading={isLoading}
                hasContent={hasContent}
                onClick={() => handleExpandChange(!isExpanded)}
            />

            {isExpanded && hasContent && <div className={sharedClasses.expandedContainer}>{renderContent()}</div>}
        </div>
    );
};

export default memo(ToolCallCard);
