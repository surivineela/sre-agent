import { Button, Field, Input, Label, MessageBar, MessageBarBody, Text, tokens } from '@fluentui/react-components';
import { FC, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { IntlShape, defineMessages } from 'react-intl';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../../../Common/Helpers/headers';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';
import { useCreationDialogStyles } from '../styles';

type ToolTestStatus = 'idle' | 'running' | 'success' | 'error';

interface ToolTestState {
    status: ToolTestStatus;
    errorMessage?: string;
    lastRunFingerprint?: string | null;
}

interface KustoQueryTesterPanelProps {
    tool: Partial<ExtendedTool>;
    intl: IntlShape;
    toolTest?: ToolTestState;
    fingerprint: string | null;
    onTestStatusChange: (status: ToolTestStatus, options?: { error?: string; fingerprint?: string | null }) => void;
}

const toolTesterMessages = defineMessages({
    toolTestPending: {
        id: 'EicA1w',
        defaultMessage: 'Test the query to continue.',
    },
    toolTestRunning: {
        id: 'tRaGqN',
        defaultMessage: 'Testing query…',
    },
    toolTestError: {
        id: 'rXQSJp',
        defaultMessage: 'Query test failed: {message}',
    },
    toolTestSuccess: {
        id: 'zCp11f',
        defaultMessage: 'Query test succeeded.',
    },
    toolTestParamFieldWarning: {
        id: 'Q7/1w4',
        defaultMessage: 'Required for testing',
    },
    testingQueryCta: {
        id: 'tRaGqN',
        defaultMessage: 'Testing query…',
    },
    testQueryCta: {
        id: 'CTRqZs',
        defaultMessage: 'Test query',
    },
    toolTestMissingParams: {
        id: '++2B26',
        defaultMessage: 'Provide values for: {parameters}',
    },
});

export const KustoQueryTesterPanel: FC<KustoQueryTesterPanelProps> = ({ tool, intl, toolTest, fingerprint, onTestStatusChange }) => {
    const styles = useCreationDialogStyles();
    const environmentContext = useContext(EnvironmentContext);
    const sreAgentEndpoint = environmentContext?.sreAgentEndpoint || '';
    const [isTestingQuery, setIsTestingQuery] = useState(false);
    const [testResults, setTestResults] = useState<any>(null);
    const [testError, setTestError] = useState<string | null>(null);
    const [paramValues, setParamValues] = useState<Record<string, string>>({});

    const requiredParams = useMemo(
        () => (tool.parameters || []).filter(param => param.required !== false && !!param.name?.trim()),
        [tool.parameters]
    );

    const missingParamNames = useMemo(
        () =>
            requiredParams
                .filter(param => !(param.name && paramValues[param.name]?.trim()))
                .map(param => param.name?.trim())
                .filter((name): name is string => !!name),
        [paramValues, requiredParams]
    );

    useEffect(() => {
        setParamValues(prev => {
            const next: Record<string, string> = {};
            (tool.parameters || []).forEach(param => {
                const name = param.name?.trim();
                if (!name) {
                    return;
                }
                if (prev[name] !== undefined) {
                    next[name] = prev[name];
                }
            });
            return next;
        });
    }, [tool.parameters]);

    useEffect(() => {
        setTestResults(null);
        setTestError(null);
    }, [fingerprint]);

    const testerStatus = useMemo(() => {
        const status = toolTest?.status ?? 'idle';
        const lastSuccessFingerprint = toolTest?.lastRunFingerprint ?? null;
        const matchesCurrentFingerprint = fingerprint && lastSuccessFingerprint === fingerprint;

        if (status === 'running') {
            return {
                intent: 'info' as const,
                message: intl.formatMessage(toolTesterMessages.toolTestRunning),
            };
        }

        if (status === 'error') {
            const errorMessage = toolTest?.errorMessage?.trim() || testError || intl.formatMessage(SreAgentResources.error);
            return {
                intent: 'error' as const,
                message: intl.formatMessage(toolTesterMessages.toolTestError, { message: errorMessage }),
            };
        }

        if (status === 'success' && matchesCurrentFingerprint) {
            return {
                intent: 'success' as const,
                message: intl.formatMessage(toolTesterMessages.toolTestSuccess),
            };
        }

        return {
            intent: 'warning' as const,
            message: intl.formatMessage(toolTesterMessages.toolTestPending),
        };
    }, [fingerprint, intl, testError, toolTest]);

    const canTest = useMemo(() => {
        if (!tool.query || !tool.database) {
            return false;
        }

        if (missingParamNames.length > 0) {
            return false;
        }

        return true;
    }, [missingParamNames.length, tool.database, tool.query]);

    const handleTestQuery = useCallback(async () => {
        if (!canTest) return;

        setIsTestingQuery(true);
        setTestError(null);
        setTestResults(null);
        onTestStatusChange('running');

        try {
            const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/tools/kusto/test`, {
                method: 'POST',
                headers: {
                    ...getAgentHeaders(),
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({
                    query: tool.query,
                    connector: tool.connector || '',
                    database: tool.database,
                    mode: tool.mode || 'query',
                    parameters: paramValues,
                }),
            });

            if (!response.ok) {
                throw new Error(`Test failed: ${response.status} - ${await response.text()}`);
            }

            const results = await response.json();
            setTestResults(results);

            if (!results.success) {
                const errorMessage = results.errorMessage || intl.formatMessage(SreAgentResources.error);
                setTestError(errorMessage);
                onTestStatusChange('error', { error: errorMessage, fingerprint: fingerprint ?? null });
            } else {
                onTestStatusChange('success', { fingerprint: fingerprint ?? null });
            }
        } catch (error) {
            const errorMessage = error instanceof Error ? error.message : intl.formatMessage(SreAgentResources.error);
            setTestError(errorMessage);
            onTestStatusChange('error', { error: errorMessage, fingerprint: fingerprint ?? null });
        } finally {
            setIsTestingQuery(false);
        }
    }, [canTest, tool, paramValues, sreAgentEndpoint, intl, fingerprint, onTestStatusChange]);

    return (
        <div className={styles.testerPanel}>
            <div className={styles.testerHeader}>
                <Text size={500} weight="semibold">
                    {intl.formatMessage(SreAgentResources.kustoQueryTesterTitle)}
                </Text>
                <div className={styles.helpText}>{intl.formatMessage(SreAgentResources.kustoQueryTesterSubtitle)}</div>
            </div>

            <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
                <MessageBar intent={testerStatus.intent} role={testerStatus.intent === 'error' ? 'alert' : 'status'}>
                    <MessageBarBody>{testerStatus.message}</MessageBarBody>
                </MessageBar>

                <MessageBar intent="info">
                    <MessageBarBody>
                        <div
                            style={{
                                fontSize: tokens.fontSizeBase200,
                                lineHeight: '1.4',
                                wordWrap: 'break-word',
                                whiteSpace: 'normal',
                            }}
                        >
                            <strong>{intl.formatMessage(SreAgentResources.kustoQueryTesterParameterLabel)}</strong>{' '}
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterUsage, {
                                code: (chunks: ReactNode) => <code>{chunks}</code>,
                            })}
                            <br />
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterExample, {
                                code: (chunks: ReactNode) => <code>{chunks}</code>,
                            })}
                            <br />
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterNote)}
                        </div>
                    </MessageBarBody>
                </MessageBar>
            </div>

            {tool.parameters && tool.parameters.length > 0 && (
                <div className={styles.testerParametersSection}>
                    <Label style={{ marginBottom: tokens.spacingVerticalXS, display: 'block' }}>
                        {intl.formatMessage(SreAgentResources.kustoQueryTesterParameterValuesLabel)}
                    </Label>
                    <div className={styles.testerParametersList}>
                        {tool.parameters.map((param, index) => (
                            <Field key={index} label={param.name} size="small" style={{ marginBottom: tokens.spacingVerticalXS }}>
                                <Input
                                    size="small"
                                    value={paramValues[param.name] || ''}
                                    onChange={(_, data) => setParamValues(prev => ({ ...prev, [param.name]: data.value }))}
                                    placeholder={intl.formatMessage(SreAgentResources.kustoQueryTesterParameterPlaceholder, {
                                        type: param.type || 'string',
                                    })}
                                    appearance={missingParamNames.includes(param.name ?? '') ? 'outline' : undefined}
                                />
                                {missingParamNames.includes(param.name ?? '') && (
                                    <div className={styles.testerParameterWarning}>
                                        {intl.formatMessage(toolTesterMessages.toolTestParamFieldWarning)}
                                    </div>
                                )}
                            </Field>
                        ))}
                    </div>
                </div>
            )}

            <div style={{ marginTop: tokens.spacingVerticalS }}>
                <Button
                    appearance="primary"
                    disabled={!canTest || isTestingQuery}
                    onClick={handleTestQuery}
                    className={styles.testerButton}
                >
                    {isTestingQuery
                        ? intl.formatMessage(toolTesterMessages.testingQueryCta)
                        : intl.formatMessage(toolTesterMessages.testQueryCta)}
                </Button>
            </div>

            {missingParamNames.length > 0 && (
                <div style={{ marginTop: '12px' }}>
                    <MessageBar intent="warning">
                        <MessageBarBody>
                            {intl.formatMessage(toolTesterMessages.toolTestMissingParams, {
                                parameters: missingParamNames.join(', '),
                            })}
                        </MessageBarBody>
                    </MessageBar>
                </div>
            )}

            {testResults && testResults.success && (
                <div className={styles.testerResults} style={{ marginTop: '16px' }}>
                    <div className={styles.testerResultsHeader}>
                        <Text weight="semibold">
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterResultsLabel, {
                                count: testResults.rowCount,
                            })}
                        </Text>
                        <Text size={200}>
                            {intl.formatMessage(SreAgentResources.kustoQueryTesterExecutionTime, {
                                milliseconds: testResults.executionTimeMs,
                            })}
                        </Text>
                    </div>

                    {testResults.rows && testResults.rows.length > 0 && (
                        <div className={styles.testerResultsTable}>
                            <table className={styles.testerTable}>
                                <thead className={styles.testerTableHead}>
                                    <tr>
                                        {testResults.columns.map((col: string, idx: number) => (
                                            <th key={idx} className={styles.testerTableHeader}>
                                                {col}
                                            </th>
                                        ))}
                                    </tr>
                                </thead>
                                <tbody>
                                    {testResults.rows.map((row: any, rowIdx: number) => (
                                        <tr key={rowIdx}>
                                            {testResults.columns.map((col: string, colIdx: number) => (
                                                <td key={colIdx} className={styles.testerTableCell}>
                                                    {String(row[col] || '')}
                                                </td>
                                            ))}
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}

                    {testResults && testResults.rowCount === 0 && (
                        <MessageBar intent="info">
                            <MessageBarBody>{intl.formatMessage(SreAgentResources.kustoQueryTesterNoResults)}</MessageBarBody>
                        </MessageBar>
                    )}
                </div>
            )}
        </div>
    );
};
