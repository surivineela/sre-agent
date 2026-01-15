import {
    Accordion,
    AccordionHeader,
    AccordionItem,
    AccordionPanel,
    Badge,
    Caption1,
    Card,
    Divider,
    Spinner,
    Text,
    makeStyles,
    mergeClasses,
    tokens,
} from '@fluentui/react-components';
import {
    BranchFork20Regular,
    CheckmarkCircle16Regular,
    ChevronDownUp16Regular,
    ChevronUpDown16Regular,
    Database20Regular,
    Dismiss16Regular,
    DismissCircle16Filled,
    PlugConnected20Regular,
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
    card: {
        width: '100%',
        maxWidth: '100%',
        marginBottom: tokens.spacingVerticalS,
    },
    headerRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        columnGap: '8px',
        rowGap: '8px',
        flexWrap: 'wrap',
    },
    headerLeft: {
        display: 'flex',
        alignItems: 'center',
        columnGap: '8px',
    },
    mcpBadge: {
        backgroundColor: tokens.colorBrandBackground2,
        color: tokens.colorBrandForeground2,
    },
    toolNameBadge: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    statusBadge: {
        minWidth: '24px',
        borderRadius: tokens.borderRadiusLarge,
        height: '24px',
    },
    parameterSection: {
        marginTop: tokens.spacingVerticalS,
    },
    parameterLabel: {
        color: tokens.colorNeutralForeground3,
        fontWeight: tokens.fontWeightSemibold,
        marginBottom: tokens.spacingVerticalXS,
    },
    parameterValue: {
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    codeBlock: {
        position: 'relative',
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStrokeDisabled}`,
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorNeutralBackground2,
        padding: '10px',
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        overflow: 'auto',
        maxHeight: '300px',
    },
    kqlBlock: {
        backgroundColor: '#1e1e1e',
        color: '#d4d4d4',
    },
    copyButtonContainer: {
        position: 'absolute',
        top: '6px',
        right: '6px',
    },
    parameterGrid: {
        display: 'grid',
        gridTemplateColumns: 'auto 1fr',
        gap: `${tokens.spacingVerticalXS} ${tokens.spacingHorizontalM}`,
        marginBottom: tokens.spacingVerticalS,
    },
    parameterName: {
        color: tokens.colorNeutralForeground3,
        fontWeight: tokens.fontWeightSemibold,
    },
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '28px',
        height: '28px',
        borderRadius: tokens.borderRadiusMedium,
        backgroundColor: tokens.colorBrandBackground2,
    },
    kustoIcon: {
        backgroundColor: '#0078d4',
        color: 'white',
    },
    adoIcon: {
        backgroundColor: '#0078d4',
        color: 'white',
    },
    genericIcon: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    outputPre: {
        margin: 0,
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        fontFamily: 'Consolas, Monaco, monospace',
        fontSize: '12px',
        width: 'calc(100% - 32px)',
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
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
        padding: '8px 12px',
        textAlign: 'left',
        borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
    },
    resultTableCell: {
        padding: '6px 12px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
        color: tokens.colorNeutralForeground2,
    },
    resultTableRow: {
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    resultSummary: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
        marginBottom: tokens.spacingVerticalS,
        color: tokens.colorNeutralForeground3,
        fontSize: '12px',
    },
});

const McpToolExecutionMessage = ({ execution }: McpToolExecutionMessageProps) => {
    const classes = useStyles();
    const intl = useIntl();
    const [isCollapsed, setIsCollapsed] = useState(execution.status !== 'Running');

    const displayConfig = useMemo(
        () => getMcpToolDisplayConfig(execution.mcpServerName, execution.toolName),
        [execution.mcpServerName, execution.toolName]
    );

    const getIcon = () => {
        switch (displayConfig.colorScheme) {
            case 'kusto':
                return <Database20Regular />;
            case 'ado':
                return <BranchFork20Regular />;
            default:
                return <PlugConnected20Regular />;
        }
    };

    const getIconClass = () => {
        switch (displayConfig.colorScheme) {
            case 'kusto':
                return mergeClasses(classes.iconContainer, classes.kustoIcon);
            case 'ado':
                return mergeClasses(classes.iconContainer, classes.adoIcon);
            default:
                return mergeClasses(classes.iconContainer, classes.genericIcon);
        }
    };

    const statusBadge = useMemo(() => {
        switch (execution.status) {
            case 'Completed':
                return (
                    <Badge color="success" icon={<CheckmarkCircle16Regular />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.completed} />}
                    </Badge>
                );
            case 'Failed':
                return (
                    <Badge color="danger" size="large" icon={<DismissCircle16Filled />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.failed} />}
                    </Badge>
                );
            case 'Running':
                return (
                    <Badge appearance="outline" color="informative" icon={<Spinner size="extra-tiny" />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.running} />}
                    </Badge>
                );
            case 'Cancelled':
                return (
                    <Badge color="informative" icon={<Dismiss16Regular />} className={classes.statusBadge}>
                        {!isCollapsed && <FormattedMessage {...SreAgentResources.canceled} />}
                    </Badge>
                );
            default:
                return null;
        }
    }, [execution.status, classes.statusBadge, isCollapsed]);

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

    const toggleCollapse = () => setIsCollapsed(prev => !prev);

    return (
        <Card className={classes.card}>
            <div className={classes.headerRow}>
                <div className={classes.headerLeft}>
                    <div className={getIconClass()}>{getIcon()}</div>
                    <Badge className={classes.mcpBadge} size="medium">
                        {intl.formatMessage(SreAgentResources.mcpLabel)}
                    </Badge>
                    <Text weight="semibold">{execution.displayName || execution.toolName}</Text>
                    <Caption1 className={classes.toolNameBadge} style={{ padding: '2px 6px', borderRadius: '4px' }}>
                        {execution.mcpServerName}
                    </Caption1>
                </div>
                <div className={classes.headerLeft}>
                    {statusBadge}
                    <button
                        onClick={toggleCollapse}
                        style={{
                            background: 'none',
                            border: 'none',
                            cursor: 'pointer',
                            padding: '4px',
                            display: 'flex',
                            alignItems: 'center',
                        }}
                        aria-label={isCollapsed ? 'Expand' : 'Collapse'}
                    >
                        {isCollapsed ? <ChevronUpDown16Regular /> : <ChevronDownUp16Regular />}
                    </button>
                </div>
            </div>

            {!isCollapsed && (
                <>
                    <Divider style={{ margin: `${tokens.spacingVerticalS} 0` }} />
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
                                    <Accordion collapsible defaultOpenItems={['result']}>
                                        <AccordionItem value="result">
                                            <AccordionHeader>
                                                <Text weight="semibold">{intl.formatMessage(SreAgentResources.mcpResult)}</Text>
                                                <span
                                                    style={{ marginLeft: '8px', color: tokens.colorNeutralForeground3, fontSize: '12px' }}
                                                >
                                                    ({kustoResult.rowCount} row{kustoResult.rowCount !== 1 ? 's' : ''})
                                                </span>
                                            </AccordionHeader>
                                            <AccordionPanel>
                                                <div className={classes.resultSummary}>
                                                    <span>Status: {kustoResult.status}</span>
                                                    {kustoResult.duration !== undefined && <span>Duration: {kustoResult.duration}s</span>}
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
                                    <Accordion collapsible defaultOpenItems={[]}>
                                        <AccordionItem value="result">
                                            <AccordionHeader>
                                                <Text weight="semibold">{intl.formatMessage(SreAgentResources.mcpResult)}</Text>
                                                <span
                                                    style={{ marginLeft: '8px', color: tokens.colorNeutralForeground3, fontSize: '12px' }}
                                                >
                                                    ({kustoResult.rowCount} row{kustoResult.rowCount !== 1 ? 's' : ''} ×{' '}
                                                    {kustoResult.columns.length} columns)
                                                </span>
                                            </AccordionHeader>
                                            <AccordionPanel>
                                                <div className={classes.resultSummary}>
                                                    <span>Status: {kustoResult.status}</span>
                                                    {kustoResult.duration !== undefined && <span>Duration: {kustoResult.duration}s</span>}
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
                                );
                            }

                            // Fallback to raw display
                            return (
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
                </>
            )}
        </Card>
    );
};

export default McpToolExecutionMessage;
