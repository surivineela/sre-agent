import { Badge, makeStyles, mergeClasses, Spinner, Text, tokens } from '@fluentui/react-components';
import {
    BranchFork24Regular,
    CheckmarkCircle16Regular,
    ChevronDown16Regular,
    ChevronRight16Regular,
    DismissCircle16Filled,
    Search24Regular,
} from '@fluentui/react-icons';
import { memo, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SubagentToolInvocation, TaskToolExecution, TaskToolExecutionGroup } from '../../Common/Contracts/DataPlane/TaskToolExecution';
import { SreAgentResources } from '../../Strings/SREAgentResources';

type TaskToolExecutionMessageProps = {
    execution?: TaskToolExecution;
    executionGroup?: TaskToolExecutionGroup;
    onCancelExecution?: (executionId: string) => void;
};

const useStyles = makeStyles({
    // Outer card container
    card: {
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '16px',
        transitionProperty: 'background-color, border-color',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
    },

    // Header row
    headerRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
    },

    // Icon container
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '40px',
        height: '40px',
        borderRadius: '8px',
        backgroundColor: 'rgba(100, 181, 246, 0.15)',
        color: '#64b5f6',
        flexShrink: 0,
    },

    // Content section
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        flex: 1,
        minWidth: 0,
    },

    primaryText: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },

    secondaryText: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
    },

    statusBadge: {
        marginLeft: 'auto',
        flexShrink: 0,
    },

    // Tree visualization container
    treeContainer: {
        marginTop: '20px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
    },

    // Parent node (hub)
    parentNode: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '20px',
        padding: '8px 16px',
        fontSize: '13px',
        fontWeight: 600,
        color: tokens.colorNeutralForeground1,
    },

    parentNodeIcon: {
        color: tokens.colorBrandForeground1,
    },

    // Connection lines container
    connectionsContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        width: '100%',
    },

    // Vertical line from parent
    verticalLine: {
        width: '1.5px',
        height: '16px',
        backgroundColor: tokens.colorNeutralStroke1,
    },

    // Horizontal line spanning all cards
    horizontalLineContainer: {
        display: 'flex',
        alignItems: 'flex-start',
        justifyContent: 'center',
        width: '100%',
    },

    horizontalLine: {
        height: '1.5px',
        backgroundColor: tokens.colorNeutralStroke1,
    },

    // Branch lines going down to each card
    branchesContainer: {
        display: 'flex',
        justifyContent: 'center',
        gap: '24px',
    },

    branchLine: {
        width: '1.5px',
        height: '16px',
        backgroundColor: tokens.colorNeutralStroke1,
    },

    branchWrapper: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        width: '280px',
    },

    // Individual agent card
    agentCard: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: '8px',
        width: '100%',
        overflow: 'hidden',
        cursor: 'pointer',
    },

    agentCardHeader: {
        padding: '12px',
    },

    agentCardRunning: {
        border: `1px solid ${tokens.colorBrandStroke1}`,
    },

    agentCardComplete: {
        border: `1px solid ${tokens.colorPaletteGreenBorder1}`,
    },

    agentCardFailed: {
        border: `1px solid ${tokens.colorPaletteRedBorder1}`,
    },

    agentHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },

    agentIcon: {
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
        fontSize: '18px',
    },

    agentTitle: {
        fontSize: '13px',
        fontWeight: 600,
        color: tokens.colorNeutralForeground1,
    },

    agentStatusIcon: {
        marginLeft: 'auto',
        flexShrink: 0,
    },

    statusComplete: {
        color: tokens.colorPaletteGreenForeground1,
    },

    statusFailed: {
        color: tokens.colorPaletteRedForeground1,
    },

    chevronIcon: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        marginLeft: '4px',
    },

    agentDescription: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground3,
        marginTop: '4px',
    },

    agentStats: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground4,
        marginTop: '4px',
    },

    // Tool invocations section
    toolsSection: {
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        paddingTop: '8px',
        marginTop: '8px',
    },

    toolItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        fontSize: '11px',
        color: tokens.colorNeutralForeground3,
        padding: '3px 0',
    },

    toolBullet: {
        width: '6px',
        height: '6px',
        borderRadius: '50%',
        flexShrink: 0,
    },

    bulletRunning: {
        backgroundColor: tokens.colorPaletteBlueBorderActive,
    },

    bulletComplete: {
        backgroundColor: tokens.colorPaletteGreenBorder1,
    },

    bulletFailed: {
        backgroundColor: tokens.colorPaletteRedBorder1,
    },

    toolText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: 1,
    },

    moreTools: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground4,
        marginBottom: '4px',
    },

    // Expanded result section
    resultSection: {
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground2,
    },

    resultLabel: {
        fontSize: '11px',
        fontWeight: 600,
        color: tokens.colorNeutralForeground3,
        marginBottom: '6px',
        textTransform: 'uppercase',
        letterSpacing: '0.5px',
    },

    resultContent: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground1,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '200px',
        overflowY: 'auto',
        fontFamily: 'Consolas, Monaco, monospace',
        lineHeight: '1.5',
    },

    noResult: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground4,
        fontStyle: 'italic',
    },

    // Tool output items in expanded view
    toolOutputItem: {
        marginBottom: '10px',
        paddingBottom: '10px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
        ':last-child': {
            borderBottom: 'none',
            marginBottom: 0,
            paddingBottom: 0,
        },
    },

    toolOutputHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        marginBottom: '4px',
    },

    toolOutputName: {
        fontSize: '12px',
        fontWeight: 600,
        color: tokens.colorNeutralForeground2,
    },

    toolOutputContent: {
        fontSize: '11px',
        color: tokens.colorNeutralForeground3,
        fontFamily: 'Consolas, Monaco, monospace',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        maxHeight: '100px',
        overflowY: 'auto',
        marginLeft: '12px',
        padding: '6px 8px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '4px',
        lineHeight: '1.4',
    },
});

const truncate = (str: string, maxLen: number): string => {
    if (str.length <= maxLen) return str;
    return str.slice(0, maxLen - 1) + '…';
};

/**
 * Formats a tool invocation for display with a friendly preamble.
 * Extracts just the filename from paths and adds action verbs.
 */
const formatToolDisplay = (toolName: string, description?: string): string => {
    // Extract the key parameter from description (usually a file path or pattern)
    const param = description || '';

    // Get just the filename/last part if it's a path
    const getFileName = (path: string): string => {
        if (!path) return '';
        // Handle both forward and back slashes
        const parts = path.split(/[/\\]/);
        return parts[parts.length - 1] || path;
    };

    // Map tool names to friendly preambles
    const toolLower = toolName.toLowerCase();

    if (toolLower.includes('read') || toolLower === 'readfile') {
        return `Reading ${getFileName(param)}`;
    }
    if (toolLower.includes('grep') || toolLower.includes('search')) {
        // For search, show the pattern as-is
        return `Searching ${param}`;
    }
    if (toolLower.includes('glob') || toolLower.includes('find')) {
        return `Finding ${param}`;
    }
    if (toolLower.includes('write') || toolLower === 'writefile') {
        return `Writing ${getFileName(param)}`;
    }
    if (toolLower.includes('edit')) {
        return `Editing ${getFileName(param)}`;
    }
    if (toolLower.includes('bash') || toolLower.includes('terminal') || toolLower.includes('exec')) {
        return `Running ${truncate(param, 25)}`;
    }
    if (toolLower.includes('list') || toolLower === 'ls') {
        return `Listing ${getFileName(param) || 'directory'}`;
    }

    // Default: use description if available, otherwise just tool name
    if (param) {
        return `${toolName}: ${getFileName(param)}`;
    }
    return toolName;
};

// Individual agent execution card with expandable result
const AgentExecutionCard = memo(
    ({
        execution,
        classes,
        intl,
    }: {
        execution: TaskToolExecution;
        classes: ReturnType<typeof useStyles>;
        intl: ReturnType<typeof useIntl>;
    }) => {
        const [isExpanded, setIsExpanded] = useState(false);
        const isRunning = execution.status === 'Running';
        const isComplete = execution.status === 'Completed';
        const isFailed = execution.status === 'Failed';
        const hasResult = execution.result || execution.error;

        const toolInvocations = execution.toolInvocations || [];
        // Show last 3 tool calls
        const visibleTools = toolInvocations.slice(-3);
        const hiddenCount = toolInvocations.length > 3 ? toolInvocations.length - 3 : 0;

        const handleClick = useCallback(() => {
            if (hasResult || toolInvocations.length > 0) {
                setIsExpanded(prev => !prev);
            }
        }, [hasResult, toolInvocations.length]);

        const cardClass = mergeClasses(
            classes.agentCard,
            isRunning && classes.agentCardRunning,
            isComplete && classes.agentCardComplete,
            isFailed && classes.agentCardFailed
        );

        const canExpand = hasResult || toolInvocations.length > 0;

        return (
            <div className={cardClass} onClick={handleClick} style={{ cursor: canExpand ? 'pointer' : 'default' }}>
                <div className={classes.agentCardHeader}>
                    <div className={classes.agentHeader}>
                        <Search24Regular className={classes.agentIcon} />
                        <span className={classes.agentTitle}>{execution.subagentType}</span>
                        {isRunning && <Spinner size="extra-tiny" className={classes.agentStatusIcon} />}
                        {isComplete && (
                            <CheckmarkCircle16Regular className={mergeClasses(classes.agentStatusIcon, classes.statusComplete)} />
                        )}
                        {isFailed && <DismissCircle16Filled className={mergeClasses(classes.agentStatusIcon, classes.statusFailed)} />}
                        {canExpand &&
                            (isExpanded ? (
                                <ChevronDown16Regular className={classes.chevronIcon} />
                            ) : (
                                <ChevronRight16Regular className={classes.chevronIcon} />
                            ))}
                    </div>

                    <div className={classes.agentDescription}>{truncate(execution.description || '', 40)}</div>

                    {toolInvocations.length > 0 && (
                        <div className={classes.agentStats}>
                            {intl.formatMessage(SreAgentResources.taskToolToolCallsCount, { count: toolInvocations.length })}
                        </div>
                    )}

                    {/* Tool invocations preview (only when not expanded and has tools) */}
                    {!isExpanded && visibleTools.length > 0 && (
                        <div className={classes.toolsSection}>
                            {hiddenCount > 0 && (
                                <div className={classes.moreTools}>
                                    {intl.formatMessage(SreAgentResources.taskToolEarlierCount, { count: hiddenCount })}
                                </div>
                            )}
                            {visibleTools.map((tool: SubagentToolInvocation, idx: number) => (
                                <div key={idx} className={classes.toolItem}>
                                    <span
                                        className={mergeClasses(
                                            classes.toolBullet,
                                            tool.status === 'Running'
                                                ? classes.bulletRunning
                                                : tool.status === 'Completed'
                                                  ? classes.bulletComplete
                                                  : classes.bulletFailed
                                        )}
                                    />
                                    <span className={classes.toolText}>
                                        {truncate(formatToolDisplay(tool.toolName, tool.description), 35)}
                                    </span>
                                </div>
                            ))}
                        </div>
                    )}
                </div>

                {/* Expanded section with tool outputs */}
                {isExpanded && (
                    <div className={classes.resultSection}>
                        {/* Show all tool invocations with their outputs */}
                        {toolInvocations.length > 0 && (
                            <>
                                <div className={classes.resultLabel}>{intl.formatMessage(SreAgentResources.taskToolToolCalls)}</div>
                                {toolInvocations.map((tool: SubagentToolInvocation, idx: number) => (
                                    <div key={idx} className={classes.toolOutputItem}>
                                        <div className={classes.toolOutputHeader}>
                                            <span
                                                className={mergeClasses(
                                                    classes.toolBullet,
                                                    tool.status === 'Running'
                                                        ? classes.bulletRunning
                                                        : tool.status === 'Completed'
                                                          ? classes.bulletComplete
                                                          : classes.bulletFailed
                                                )}
                                            />
                                            <span className={classes.toolOutputName}>
                                                {formatToolDisplay(tool.toolName, tool.description)}
                                            </span>
                                        </div>
                                        {tool.output && <div className={classes.toolOutputContent}>{tool.output}</div>}
                                    </div>
                                ))}
                            </>
                        )}

                        {/* Show final result if available */}
                        {(execution.result || execution.error) && (
                            <>
                                <div className={classes.resultLabel} style={{ marginTop: toolInvocations.length > 0 ? '12px' : 0 }}>
                                    {intl.formatMessage(SreAgentResources.taskToolFinalResult)}
                                </div>
                                {execution.error ? (
                                    <div className={classes.resultContent} style={{ color: tokens.colorPaletteRedForeground1 }}>
                                        {execution.error}
                                    </div>
                                ) : (
                                    <div className={classes.resultContent}>{execution.result}</div>
                                )}
                            </>
                        )}

                        {/* Show message if no data available */}
                        {toolInvocations.length === 0 && !execution.result && !execution.error && (
                            <div className={classes.noResult}>{intl.formatMessage(SreAgentResources.taskToolNoOutputsAvailable)}</div>
                        )}
                    </div>
                )}
            </div>
        );
    }
);
AgentExecutionCard.displayName = 'AgentExecutionCard';

/**
 * Tree-style visualization for parallel Task tool executions
 */
const TaskToolExecutionMessage = ({ execution, executionGroup }: TaskToolExecutionMessageProps) => {
    const classes = useStyles();
    const intl = useIntl();

    // Handle single execution (wrap in group-like structure)
    const executions = useMemo(() => {
        if (executionGroup?.executions) return executionGroup.executions;
        if (execution) return [execution];
        return [];
    }, [execution, executionGroup]);

    if (executions.length === 0) return null;

    const runningCount = executions.filter(e => e.status === 'Running').length;
    const completedCount = executions.filter(e => e.status === 'Completed').length;
    const failedCount = executions.filter(e => e.status === 'Failed').length;
    const totalCount = executions.length;
    const isAllComplete = runningCount === 0;

    // Status badge for header
    const statusBadge = !isAllComplete ? (
        <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />} className={classes.statusBadge}>
            {intl.formatMessage(SreAgentResources.taskToolRunning, { count: runningCount })}
        </Badge>
    ) : failedCount > 0 ? (
        <Badge color="danger" icon={<DismissCircle16Filled />} className={classes.statusBadge}>
            {intl.formatMessage(SreAgentResources.taskToolAgentsFailed, { failed: failedCount })}
        </Badge>
    ) : (
        <Badge color="success" icon={<CheckmarkCircle16Regular />} className={classes.statusBadge}>
            {intl.formatMessage(SreAgentResources.taskToolAllComplete)}
        </Badge>
    );

    // Calculate horizontal line width based on number of cards
    const cardWidth = 280;
    const gap = 24;
    const horizontalLineWidth = totalCount > 1 ? (totalCount - 1) * (cardWidth + gap) : 0;

    return (
        <div className={classes.card}>
            {/* Header */}
            <div className={classes.headerRow}>
                <div className={classes.iconContainer}>
                    <BranchFork24Regular />
                </div>
                <div className={classes.content}>
                    <Text className={classes.primaryText}>{intl.formatMessage(SreAgentResources.taskToolParallelExploration)}</Text>
                    <Text className={classes.secondaryText}>
                        {intl.formatMessage(SreAgentResources.taskToolAgentsCompleted, { total: totalCount, completed: completedCount })}
                        {failedCount > 0 && ` · ${intl.formatMessage(SreAgentResources.taskToolAgentsFailed, { failed: failedCount })}`}
                    </Text>
                </div>
                {statusBadge}
            </div>

            {/* Tree visualization */}
            <div className={classes.treeContainer}>
                {/* Parent node */}
                <div className={classes.parentNode}>
                    <BranchFork24Regular className={classes.parentNodeIcon} />
                    <span>{intl.formatMessage(SreAgentResources.taskToolExploreAgents, { count: totalCount })}</span>
                    {!isAllComplete && <Spinner size="extra-tiny" />}
                </div>

                {/* Connection lines */}
                <div className={classes.connectionsContainer}>
                    {/* Vertical line from parent */}
                    <div className={classes.verticalLine} />

                    {/* Horizontal line spanning cards (only if more than 1 card) */}
                    {totalCount > 1 && (
                        <div className={classes.horizontalLineContainer}>
                            <div className={classes.horizontalLine} style={{ width: `${horizontalLineWidth}px` }} />
                        </div>
                    )}

                    {/* Branch lines going down to each card */}
                    <div className={classes.branchesContainer}>
                        {executions.map(exec => (
                            <div key={exec.id} className={classes.branchWrapper}>
                                <div className={classes.branchLine} />
                                <AgentExecutionCard execution={exec} classes={classes} intl={intl} />
                            </div>
                        ))}
                    </div>
                </div>
            </div>
        </div>
    );
};

export default memo(TaskToolExecutionMessage);
