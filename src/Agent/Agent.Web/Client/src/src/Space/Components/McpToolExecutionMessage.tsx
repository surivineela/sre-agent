import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    Caption1,
    Spinner,
    Text,
    makeStyles,
    mergeClasses,
    tokens,
} from '@fluentui/react-components';
import {
    AppsAddIn20Regular,
    BranchFork20Regular,
    CheckmarkCircle16Regular,
    ChevronDown16Regular,
    ChevronRight16Regular,
    Database20Regular,
    DismissCircle16Filled,
} from '@fluentui/react-icons';
import { useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { CopyButton } from '../../Common/Components/CopyButton';
import { McpToolExecution, getMcpToolDisplayConfig } from '../../Common/Contracts/DataPlane/McpToolExecution';
import { SreAgentResources } from '../../Strings/SREAgentResources';

/**
 * Parses Kusto MCP result JSON and extracts table data.
 * Expected format: {"status":200,"message":"Success","results":{"items":[{"col1":"type1",...},[val1,...],...]}}
 */
interface KustoResultData {
    status: number;
    message: string;
    duration?: number;
    columns: { name: string; type: string }[];
    rows: unknown[][];
    rowCount: number;
}

const parseKustoResult = (resultString: string): KustoResultData | null => {
    try {
        const result = JSON.parse(resultString);
        if (result.status !== 200 || !result.results?.items) {
            return null;
        }

        const items = result.results.items;
        if (!Array.isArray(items) || items.length < 1) {
            return null;
        }

        // First item is column schema (object with column name -> type)
        const schemaItem = items[0];
        if (typeof schemaItem !== 'object' || Array.isArray(schemaItem)) {
            return null;
        }

        const columns = Object.entries(schemaItem as Record<string, string>).map(([name, type]) => ({
            name,
            type: String(type),
        }));

        // Remaining items are data rows (arrays)
        const rows = items.slice(1).filter(Array.isArray) as unknown[][];

        return {
            status: result.status,
            message: result.message,
            duration: result.duration,
            columns,
            rows,
            rowCount: rows.length,
        };
    } catch {
        return null;
    }
};

type McpToolExecutionMessageProps = {
    execution: McpToolExecution;
};

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
    },

    // Summary line - minimal, VS Code style
    summaryLine: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        padding: '2px 0',
        cursor: 'pointer',
        color: tokens.colorNeutralForeground3,
        fontSize: '13px',
        userSelect: 'none',
        ':hover': {
            color: tokens.colorNeutralForeground2,
        },
    },
    chevron: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        fontSize: '14px',
    },
    summaryIcon: {
        flexShrink: 0,
        display: 'flex',
        alignItems: 'center',
    },
    summaryToolName: {
        fontFamily: 'Consolas, Monaco, monospace',
        color: tokens.colorNeutralForeground2,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flexShrink: 1,
        minWidth: 0,
        fontSize: '12px',
    },
    summaryServerName: {
        color: tokens.colorNeutralForeground4,
        fontSize: '12px',
        flexShrink: 0,
        whiteSpace: 'nowrap',
    },
    separator: {
        flexShrink: 0,
        color: tokens.colorNeutralForeground4,
    },
    summaryResultInfo: {
        color: tokens.colorNeutralForeground4,
        flexShrink: 0,
        whiteSpace: 'nowrap',
        fontSize: '12px',
    },
    summaryResultInfoSuccess: {
        color: tokens.colorPaletteGreenForeground1,
    },
    summaryResultInfoError: {
        color: tokens.colorPaletteRedForeground1,
    },
    spinner: {
        marginLeft: '-2px',
    },

    // Expanded container - minimal left border
    expandedContainer: {
        borderLeft: `1px solid ${tokens.colorNeutralStroke3}`,
        marginLeft: '7px',
        marginTop: '2px',
        overflow: 'hidden',
    },

    // Content header - minimal
    contentHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        padding: '4px 8px 4px 12px',
        gap: '8px',
    },
    contentHeaderLeft: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        flex: 1,
        minWidth: 0,
        flexWrap: 'wrap',
    },
    toolNameBadge: {
        color: tokens.colorNeutralForeground4,
        fontSize: '11px',
    },
    statusBadge: {
        minWidth: '18px',
        height: '18px',
        fontSize: '11px',
    },
    parameterSection: {
        padding: '8px 12px',
    },
    parameterLabel: {
        color: tokens.colorNeutralForeground4,
        fontSize: '11px',
        marginBottom: '4px',
    },
    parameterValue: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    codeBlock: {
        position: 'relative',
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        overflow: 'auto',
        maxHeight: '280px',
        padding: '8px 0',
    },
    kqlBlock: {
        backgroundColor: 'rgba(30, 30, 30, 0.5)',
        color: '#d4d4d4',
        padding: '8px 12px',
        borderRadius: '4px',
    },
    copyButtonContainer: {
        position: 'absolute',
        top: '4px',
        right: '4px',
        opacity: 0.6,
        ':hover': {
            opacity: 1,
        },
    },
    parameterGrid: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        gap: '2px 12px',
        marginBottom: '8px',
        fontSize: '12px',
    },
    parameterName: {
        color: tokens.colorNeutralForeground4,
    },
    kustoIcon: {
        color: '#0078d4',
    },
    adoIcon: {
        color: '#0078d4',
    },
    mcpIcon: {
        color: tokens.colorBrandForeground1,
    },
    outputPre: {
        margin: 0,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
    },
    outputPreCollapsed: {
        whiteSpace: 'nowrap',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        display: 'block',
    },
    resultTable: {
        width: '100%',
        borderCollapse: 'collapse',
        fontSize: '12px',
        fontFamily: 'Consolas, Monaco, monospace',
    },
    resultTableHeader: {
        color: tokens.colorNeutralForeground3,
        fontWeight: 500,
        padding: '4px 8px',
        textAlign: 'left',
        borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
        fontSize: '11px',
    },
    resultTableCell: {
        padding: '4px 8px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke3}`,
        color: tokens.colorNeutralForeground2,
    },
    resultTableRow: {
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    resultSummary: {
        display: 'flex',
        gap: '12px',
        marginBottom: '8px',
        color: tokens.colorNeutralForeground4,
        fontSize: '11px',
    },
    resultSection: {
        padding: '4px 12px 8px 12px',
    },
});

const McpToolExecutionMessage = ({ execution }: McpToolExecutionMessageProps) => {
    const classes = useStyles();
    const intl = useIntl();
    const [isExpanded, setIsExpanded] = useState(execution.status === 'Running');

    const displayConfig = useMemo(
        () => getMcpToolDisplayConfig(execution.mcpServerName, execution.toolName),
        [execution.mcpServerName, execution.toolName]
    );

    // Get icon for summary line and header
    const getIcon = () => {
        switch (displayConfig.colorScheme) {
            case 'kusto':
                return <Database20Regular className={classes.kustoIcon} />;
            case 'ado':
                return <BranchFork20Regular className={classes.adoIcon} />;
            default:
                return <AppsAddIn20Regular className={classes.mcpIcon} />;
        }
    };

    // Get result info for summary line
    const getResultInfo = useMemo(() => {
        switch (execution.status) {
            case 'Completed':
                return intl.formatMessage(SreAgentResources.completed);
            case 'Failed':
                return intl.formatMessage(SreAgentResources.failed);
            case 'Running':
                return intl.formatMessage(SreAgentResources.running);
            case 'Cancelled':
                return intl.formatMessage(SreAgentResources.canceled);
            default:
                return '';
        }
    }, [execution.status, intl]);

    const isError = execution.status === 'Failed';
    const isSuccess = execution.status === 'Completed';
    const isLoading = execution.status === 'Running';

    const statusBadge = useMemo(() => {
        switch (execution.status) {
            case 'Completed':
                return (
                    <Badge color="success" icon={<CheckmarkCircle16Regular />} className={classes.statusBadge}>
                        <FormattedMessage {...SreAgentResources.completed} />
                    </Badge>
                );
            case 'Failed':
                return (
                    <Badge color="danger" size="large" icon={<DismissCircle16Filled />} className={classes.statusBadge}>
                        <FormattedMessage {...SreAgentResources.failed} />
                    </Badge>
                );
            case 'Running':
                return (
                    <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />} className={classes.statusBadge}>
                        <FormattedMessage {...SreAgentResources.running} />
                    </Badge>
                );
            case 'Cancelled':
                return (
                    <Badge color="informative" className={classes.statusBadge}>
                        <FormattedMessage {...SreAgentResources.canceled} />
                    </Badge>
                );
            default:
                return null;
        }
    }, [execution.status, classes.statusBadge]);

    const handleToggleExpand = () => setIsExpanded(prev => !prev);

    const handleKeyDown = (e: React.KeyboardEvent) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            handleToggleExpand();
        }
    };

    const renderParameters = () => {
        if (!execution.parameters) return null;

        const params = execution.parameters;

        // For Kusto query, show a special KQL code block
        if (displayConfig.showQueryEditor && params.query) {
            return (
                <div className={classes.parameterSection}>
                    {/* Show cluster and database info */}
                    {(params.clusterUrl || params.database) && (
                        <div className={classes.parameterGrid}>
                            {params.clusterUrl && (
                                <>
                                    <Caption1 className={classes.parameterName}>Cluster:</Caption1>
                                    <Text className={classes.parameterValue}>{params.clusterUrl}</Text>
                                </>
                            )}
                            {params.database && (
                                <>
                                    <Caption1 className={classes.parameterName}>Database:</Caption1>
                                    <Text className={classes.parameterValue}>{params.database}</Text>
                                </>
                            )}
                        </div>
                    )}

                    {/* KQL Query block */}
                    <Caption1 className={classes.parameterLabel}>{intl.formatMessage(SreAgentResources.mcpQueryKql)}</Caption1>
                    <div className={mergeClasses(classes.codeBlock, classes.kqlBlock)}>
                        <div className={classes.copyButtonContainer}>
                            <CopyButton textToCopy={params.query} />
                        </div>
                        <pre className={classes.outputPre}>{params.query}</pre>
                    </div>
                </div>
            );
        }

        // For Kusto sample/schema
        if (params.tableName || params.database || params.clusterUrl) {
            return (
                <div className={classes.parameterSection}>
                    <div className={classes.parameterGrid}>
                        {params.clusterUrl && (
                            <>
                                <Caption1 className={classes.parameterName}>Cluster:</Caption1>
                                <Text className={classes.parameterValue}>{params.clusterUrl}</Text>
                            </>
                        )}
                        {params.database && (
                            <>
                                <Caption1 className={classes.parameterName}>Database:</Caption1>
                                <Text className={classes.parameterValue}>{params.database}</Text>
                            </>
                        )}
                        {params.tableName && (
                            <>
                                <Caption1 className={classes.parameterName}>Table:</Caption1>
                                <Text className={classes.parameterValue}>{params.tableName}</Text>
                            </>
                        )}
                        {params.sampleSize && (
                            <>
                                <Caption1 className={classes.parameterName}>{intl.formatMessage(SreAgentResources.mcpSampleSize)}</Caption1>
                                <Text className={classes.parameterValue}>{params.sampleSize}</Text>
                            </>
                        )}
                    </div>
                </div>
            );
        }

        // For ADO PR creation - show only key info: repository, source branch, and title
        if (params.repository || params.sourceBranch || params.raw?.repositoryid || params.raw?.sourcerefname) {
            const repoName = params.repository || (params.raw?.repositoryid as string);
            const sourceBranch = params.sourceBranch || (params.raw?.sourcerefname as string)?.replace('refs/heads/', '');
            const prTitle = params.title || (params.raw?.title as string);

            return (
                <div className={classes.parameterSection}>
                    <div className={classes.parameterGrid}>
                        {repoName && (
                            <>
                                <Caption1 className={classes.parameterName}>Repository:</Caption1>
                                <Text className={classes.parameterValue}>{repoName}</Text>
                            </>
                        )}
                        {sourceBranch && (
                            <>
                                <Caption1 className={classes.parameterName}>Branch:</Caption1>
                                <Text className={classes.parameterValue}>{sourceBranch}</Text>
                            </>
                        )}
                        {prTitle && (
                            <>
                                <Caption1 className={classes.parameterName}>Title:</Caption1>
                                <Text className={classes.parameterValue}>{prTitle}</Text>
                            </>
                        )}
                    </div>
                </div>
            );
        }

        // Generic parameters - show raw if available
        if (params.raw && Object.keys(params.raw).length > 0) {
            return (
                <div className={classes.parameterSection}>
                    <Caption1 className={classes.parameterLabel}>Parameters:</Caption1>
                    <div className={classes.codeBlock}>
                        <pre className={classes.outputPre}>{JSON.stringify(params.raw, null, 2)}</pre>
                    </div>
                </div>
            );
        }

        return null;
    };

    // Truncate tool name for summary
    const truncatedToolName =
        (execution.displayName || execution.toolName).length > 50
            ? (execution.displayName || execution.toolName).slice(0, 47) + '...'
            : execution.displayName || execution.toolName;

    return (
        <div className={classes.root}>
            {/* Summary Line */}
            <div
                className={classes.summaryLine}
                onClick={handleToggleExpand}
                onKeyDown={handleKeyDown}
                role="button"
                tabIndex={0}
                aria-expanded={isExpanded}
            >
                {isLoading ? (
                    <Spinner size="extra-tiny" className={classes.spinner} />
                ) : isExpanded ? (
                    <ChevronDown16Regular className={classes.chevron} />
                ) : (
                    <ChevronRight16Regular className={classes.chevron} />
                )}

                <span className={classes.summaryIcon}>{getIcon()}</span>

                <span className={classes.summaryToolName}>{truncatedToolName}</span>

                <span className={classes.separator}>·</span>
                <span className={classes.summaryServerName}>{execution.mcpServerName}</span>

                <span className={classes.separator}>·</span>
                <span
                    className={mergeClasses(
                        classes.summaryResultInfo,
                        isSuccess && classes.summaryResultInfoSuccess,
                        isError && classes.summaryResultInfoError
                    )}
                >
                    {getResultInfo}
                </span>
            </div>

            {/* Expanded Content */}
            {isExpanded && (
                <div className={classes.expandedContainer}>
                    {/* Header - minimal */}
                    <div className={classes.contentHeader}>
                        <div className={classes.contentHeaderLeft}>
                            {getIcon()}
                            <Text weight="semibold" size={200}>
                                {execution.displayName || execution.toolName}
                            </Text>
                            <Caption1 className={classes.toolNameBadge}>{execution.mcpServerName}</Caption1>
                        </div>
                        {statusBadge}
                    </div>

                    {/* Parameters */}
                    {renderParameters()}

                    {/* Show result if completed */}
                    {execution.status === 'Completed' &&
                        execution.result &&
                        (() => {
                            // Try to parse as Kusto result for kusto-mcp tools
                            const kustoResult = displayConfig.colorScheme === 'kusto' ? parseKustoResult(execution.result) : null;

                            // Only show table for results with 4 or fewer columns
                            const MAX_TABLE_COLUMNS = 4;
                            const shouldShowTable =
                                kustoResult && kustoResult.columns.length > 0 && kustoResult.columns.length <= MAX_TABLE_COLUMNS;

                            if (shouldShowTable) {
                                // Render as formatted table
                                return (
                                    <div className={classes.resultSection}>
                                        <Accordion collapsible defaultOpenItems={['result']}>
                                            <AccordionItem value="result">
                                                <AccordionHeader>
                                                    <Text weight="semibold">{intl.formatMessage(SreAgentResources.mcpResult)}</Text>
                                                    <span
                                                        style={{
                                                            marginLeft: '8px',
                                                            color: tokens.colorNeutralForeground3,
                                                            fontSize: '12px',
                                                        }}
                                                    >
                                                        ({kustoResult.rowCount} row{kustoResult.rowCount !== 1 ? 's' : ''})
                                                    </span>
                                                </AccordionHeader>
                                                <AccordionPanel>
                                                    <div className={classes.resultSummary}>
                                                        <span>Status: {kustoResult.status}</span>
                                                        {kustoResult.duration !== undefined && (
                                                            <span>Duration: {kustoResult.duration}s</span>
                                                        )}
                                                    </div>
                                                    <div style={{ overflowX: 'auto', maxHeight: '300px' }}>
                                                        <table className={classes.resultTable}>
                                                            <thead>
                                                                <tr>
                                                                    {kustoResult.columns.map((col, i) => (
                                                                        <th key={i} className={classes.resultTableHeader}>
                                                                            {col.name}
                                                                            <span
                                                                                style={{
                                                                                    fontWeight: 'normal',
                                                                                    color: tokens.colorNeutralForeground4,
                                                                                    fontSize: '10px',
                                                                                    marginLeft: '4px',
                                                                                }}
                                                                            >
                                                                                ({col.type})
                                                                            </span>
                                                                        </th>
                                                                    ))}
                                                                </tr>
                                                            </thead>
                                                            <tbody>
                                                                {kustoResult.rows.map((row, rowIdx) => (
                                                                    <tr key={rowIdx} className={classes.resultTableRow}>
                                                                        {row.map((cell, cellIdx) => (
                                                                            <td key={cellIdx} className={classes.resultTableCell}>
                                                                                {cell === null ? (
                                                                                    <span
                                                                                        style={{
                                                                                            color: tokens.colorNeutralForeground4,
                                                                                            fontStyle: 'italic',
                                                                                        }}
                                                                                    >
                                                                                        {intl.formatMessage(SreAgentResources.mcpNullValue)}
                                                                                    </span>
                                                                                ) : typeof cell === 'object' ? (
                                                                                    JSON.stringify(cell)
                                                                                ) : (
                                                                                    String(cell)
                                                                                )}
                                                                            </td>
                                                                        ))}
                                                                    </tr>
                                                                ))}
                                                            </tbody>
                                                        </table>
                                                    </div>
                                                    <div style={{ marginTop: '8px', display: 'flex', justifyContent: 'flex-end' }}>
                                                        <CopyButton textToCopy={execution.result} />
                                                    </div>
                                                </AccordionPanel>
                                            </AccordionItem>
                                        </Accordion>
                                    </div>
                                );
                            }

                            // For Kusto results with many columns, show summary + formatted JSON
                            if (kustoResult && kustoResult.columns.length > MAX_TABLE_COLUMNS) {
                                // Try to pretty-print the JSON
                                let formattedResult = execution.result;
                                try {
                                    formattedResult = JSON.stringify(JSON.parse(execution.result), null, 2);
                                } catch {
                                    // Keep original if parsing fails
                                }

                                return (
                                    <div className={classes.resultSection}>
                                        <Accordion collapsible defaultOpenItems={[]}>
                                            <AccordionItem value="result">
                                                <AccordionHeader>
                                                    <Text weight="semibold">{intl.formatMessage(SreAgentResources.mcpResult)}</Text>
                                                    <span
                                                        style={{
                                                            marginLeft: '8px',
                                                            color: tokens.colorNeutralForeground3,
                                                            fontSize: '12px',
                                                        }}
                                                    >
                                                        ({kustoResult.rowCount} row{kustoResult.rowCount !== 1 ? 's' : ''} ×{' '}
                                                        {kustoResult.columns.length} columns)
                                                    </span>
                                                </AccordionHeader>
                                                <AccordionPanel>
                                                    <div className={classes.resultSummary}>
                                                        <span>Status: {kustoResult.status}</span>
                                                        {kustoResult.duration !== undefined && (
                                                            <span>Duration: {kustoResult.duration}s</span>
                                                        )}
                                                    </div>
                                                    <div className={classes.codeBlock}>
                                                        <div className={classes.copyButtonContainer}>
                                                            <CopyButton textToCopy={execution.result} />
                                                        </div>
                                                        <pre className={mergeClasses(classes.outputPre)}>{formattedResult}</pre>
                                                    </div>
                                                </AccordionPanel>
                                            </AccordionItem>
                                        </Accordion>
                                    </div>
                                );
                            }

                            // Fallback to raw display
                            return (
                                <div className={classes.resultSection}>
                                    <Accordion collapsible defaultOpenItems={[]}>
                                        <AccordionItem value="result">
                                            <AccordionHeader>
                                                <Text weight="semibold">{intl.formatMessage(SreAgentResources.mcpResult)}</Text>
                                            </AccordionHeader>
                                            <AccordionPanel>
                                                <div className={classes.codeBlock}>
                                                    <div className={classes.copyButtonContainer}>
                                                        <CopyButton textToCopy={execution.result} />
                                                    </div>
                                                    <pre className={mergeClasses(classes.outputPre)}>{execution.result}</pre>
                                                </div>
                                            </AccordionPanel>
                                        </AccordionItem>
                                    </Accordion>
                                </div>
                            );
                        })()}

                    {/* Show error if failed */}
                    {execution.status === 'Failed' && execution.error && (
                        <div className={classes.parameterSection}>
                            <Caption1 className={classes.parameterLabel} style={{ color: tokens.colorPaletteRedForeground1 }}>
                                Error:
                            </Caption1>
                            <div className={classes.codeBlock} style={{ borderColor: tokens.colorPaletteRedBorder1 }}>
                                <pre className={classes.outputPre}>{execution.error}</pre>
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
};

export default McpToolExecutionMessage;
