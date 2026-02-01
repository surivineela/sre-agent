import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    makeStyles,
    mergeClasses,
    Spinner,
    Text,
    tokens,
} from '@fluentui/react-components';
import {
    BranchFork24Regular,
    CheckmarkCircle16Regular,
    Code24Regular,
    DataArea24Regular,
    DismissCircle16Filled,
    Search24Regular,
    ShapeSubtract24Regular,
} from '@fluentui/react-icons';
import { memo, ReactElement } from 'react';
import { useIntl } from 'react-intl';
import { SubagentToolInvocation, TaskToolExecution, TaskToolExecutionGroup } from '../../Common/Contracts/DataPlane/TaskToolExecution';
import { SreAgentResources } from '../../Strings/SREAgentResources';

type TaskToolExecutionMessageProps = {
    execution?: TaskToolExecution;
    executionGroup?: TaskToolExecutionGroup;
    onCancelExecution?: (executionId: string) => void;
};

const useStyles = makeStyles({
    // Card container - matches ExecutionMessage pattern
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
    // Icon container - matches SpecialMessageCard
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '40px',
        height: '40px',
        borderRadius: '8px',
        flexShrink: 0,
    },
    iconExplore: {
        backgroundColor: 'rgba(100, 181, 246, 0.15)',
        color: '#64b5f6',
    },
    iconPlan: {
        backgroundColor: 'rgba(186, 104, 200, 0.15)',
        color: '#ba68c8',
    },
    iconReview: {
        backgroundColor: 'rgba(129, 199, 132, 0.15)',
        color: '#81c784',
    },
    iconKusto: {
        backgroundColor: 'rgba(255, 183, 77, 0.15)',
        color: '#ffb74d',
    },
    iconBash: {
        backgroundColor: 'rgba(144, 164, 174, 0.15)',
        color: '#90a4ae',
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
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    // Status badge
    statusBadge: {
        marginLeft: 'auto',
        flexShrink: 0,
    },
    // Tool progress section
    toolProgressSection: {
        marginTop: '12px',
        paddingTop: '12px',
        borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    toolProgressHeader: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        marginBottom: '8px',
    },
    toolItem: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '4px 0',
        fontSize: tokens.fontSizeBase200,
    },
    toolBullet: {
        width: '8px',
        height: '8px',
        borderRadius: '50%',
        flexShrink: 0,
    },
    bulletRunning: {
        backgroundColor: tokens.colorPaletteYellowBackground2,
    },
    bulletComplete: {
        backgroundColor: tokens.colorPaletteGreenBackground2,
    },
    bulletFailed: {
        backgroundColor: tokens.colorPaletteRedBackground2,
    },
    toolName: {
        color: tokens.colorNeutralForeground2,
        fontFamily: 'Consolas, Monaco, monospace',
    },
    toolDesc: {
        color: tokens.colorNeutralForeground4,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: 1,
    },
    moreIndicator: {
        color: tokens.colorNeutralForeground4,
        fontStyle: 'italic',
        fontSize: tokens.fontSizeBase200,
        padding: '4px 0',
    },
    // Group container
    groupContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
    },
    groupHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        padding: '8px 0',
    },
    groupIcon: {
        color: tokens.colorBrandForeground1,
    },
    // Result/Error section
    resultSection: {
        marginTop: '8px',
        padding: '8px 12px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: '8px',
        fontSize: tokens.fontSizeBase200,
    },
    resultPreview: {
        color: tokens.colorNeutralForeground2,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    errorText: {
        color: tokens.colorPaletteRedForeground1,
    },
    // Accordion styling
    accordionPanel: {
        padding: '8px 0 0 0',
    },
    resultFull: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        color: tokens.colorNeutralForeground2,
        maxHeight: '300px',
        overflow: 'auto',
    },
});

const getIcon = (type: string): ReactElement => {
    switch (type) {
        case 'Explore':
            return <Search24Regular />;
        case 'Plan':
            return <ShapeSubtract24Regular />;
        case 'CodeReview':
            return <Code24Regular />;
        case 'KustoQuery':
            return <DataArea24Regular />;
        case 'Bash':
            return <Code24Regular />;
        default:
            return <Search24Regular />;
    }
};

const getIconClass = (type: string, classes: ReturnType<typeof useStyles>) => {
    switch (type) {
        case 'Explore':
            return classes.iconExplore;
        case 'Plan':
            return classes.iconPlan;
        case 'CodeReview':
            return classes.iconReview;
        case 'KustoQuery':
            return classes.iconKusto;
        case 'Bash':
            return classes.iconBash;
        default:
            return classes.iconExplore;
    }
};

const truncate = (str: string, maxLen: number): string => {
    if (str.length <= maxLen) return str;
    return str.slice(0, maxLen - 1) + '…';
};

const getResultPreview = (result?: string): string | null => {
    if (!result) return null;
    const lines = result.split('\n').filter(l => l.trim() && !l.startsWith('#') && !l.startsWith('**'));
    if (lines.length === 0) return null;
    return truncate(lines[0].trim(), 80);
};

/**
 * Tool progress display with rolling window
 */
const ToolProgress = memo(({ invocations, classes }: { invocations: SubagentToolInvocation[]; classes: ReturnType<typeof useStyles> }) => {
    const intl = useIntl();
    const hiddenCount = invocations.length > 3 ? invocations.length - 3 : 0;
    const visibleInvocations = invocations.slice(-3);

    return (
        <div className={classes.toolProgressSection}>
            <Text className={classes.toolProgressHeader}>{intl.formatMessage(SreAgentResources.toolCalls)}</Text>
            {hiddenCount > 0 && (
                <div className={classes.moreIndicator}>{intl.formatMessage(SreAgentResources.otherToolCalls, { count: hiddenCount })}</div>
            )}
            {visibleInvocations.map((tool, idx) => (
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
                    <span className={classes.toolName}>{tool.toolName}</span>
                    {tool.description && <span className={classes.toolDesc}>{truncate(tool.description, 40)}</span>}
                </div>
            ))}
        </div>
    );
});

ToolProgress.displayName = 'ToolProgress';

/**
 * Single execution card
 */
const ExecutionCard = memo(({ execution, classes }: { execution: TaskToolExecution; classes: ReturnType<typeof useStyles> }) => {
    const isRunning = execution.status === 'Running';
    const isComplete = execution.status === 'Completed';
    const isError = execution.status === 'Failed';
    const resultPreview = getResultPreview(execution.result);

    const statusBadge = isRunning ? (
        <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />} className={classes.statusBadge}>
            Running
        </Badge>
    ) : isComplete ? (
        <Badge color="success" icon={<CheckmarkCircle16Regular />} className={classes.statusBadge}>
            Done
        </Badge>
    ) : isError ? (
        <Badge color="danger" icon={<DismissCircle16Filled />} className={classes.statusBadge}>
            Failed
        </Badge>
    ) : null;

    return (
        <div className={classes.card}>
            <div className={classes.headerRow}>
                <div className={mergeClasses(classes.iconContainer, getIconClass(execution.subagentType, classes))}>
                    {getIcon(execution.subagentType)}
                </div>
                <div className={classes.content}>
                    <Text className={classes.primaryText}>{execution.subagentType}</Text>
                    <Text className={classes.secondaryText}>{execution.description}</Text>
                </div>
                {statusBadge}
            </div>

            {isRunning && execution.toolInvocations && execution.toolInvocations.length > 0 && (
                <ToolProgress invocations={execution.toolInvocations} classes={classes} />
            )}

            {isComplete && execution.result && (
                <Accordion collapsible>
                    <AccordionItem value="result">
                        <AccordionHeader size="small">
                            <Text className={classes.resultPreview}>{resultPreview || 'View result'}</Text>
                        </AccordionHeader>
                        <AccordionPanel className={classes.accordionPanel}>
                            <div className={classes.resultFull}>{execution.result}</div>
                        </AccordionPanel>
                    </AccordionItem>
                </Accordion>
            )}

            {isError && execution.error && (
                <div className={classes.resultSection}>
                    <Text className={classes.errorText}>{truncate(execution.error, 100)}</Text>
                </div>
            )}
        </div>
    );
});

ExecutionCard.displayName = 'ExecutionCard';

/**
 * Main component - card-based display matching other tool cards
 */
const TaskToolExecutionMessage = ({ execution, executionGroup }: TaskToolExecutionMessageProps) => {
    const classes = useStyles();

    // Single execution
    if (execution && !executionGroup) {
        return <ExecutionCard execution={execution} classes={classes} />;
    }

    // Parallel executions group
    if (executionGroup && executionGroup.executions.length > 0) {
        // Single task in group - render directly
        if (executionGroup.executions.length === 1) {
            return <ExecutionCard execution={executionGroup.executions[0]} classes={classes} />;
        }

        const runningCount = executionGroup.executions.filter(e => e.status === 'Running').length;
        const totalCount = executionGroup.executions.length;

        return (
            <div className={classes.groupContainer}>
                {/* Group header */}
                <div className={classes.groupHeader}>
                    <BranchFork24Regular className={classes.groupIcon} />
                    <Text weight="semibold">{totalCount} parallel tasks</Text>
                    {runningCount > 0 ? (
                        <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />}>
                            {runningCount} running
                        </Badge>
                    ) : (
                        <Badge color="success" icon={<CheckmarkCircle16Regular />}>
                            All complete
                        </Badge>
                    )}
                </div>

                {/* Individual execution cards */}
                {executionGroup.executions.map(exec => (
                    <ExecutionCard key={exec.id} execution={exec} classes={classes} />
                ))}
            </div>
        );
    }

    return null;
};

export default memo(TaskToolExecutionMessage);
