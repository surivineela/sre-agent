import { Button, MessageBar, MessageBarBody, Text, tokens } from '@fluentui/react-components';
import { Play16Regular, WarningFilled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { TestValueAccordion } from './Common/TestValueAccordion';
import { useKustoToolCreateDialogStyles } from './KustoToolDialog.Styles';
import { KustoToolFormProps, parseKustoAuthorizationError, truncateErrorMessage } from './KustoToolUtilities';

interface KustoToolTestPanelProps {
    hasSuccessRunTest: boolean;
    setHasSuccessRunTest: (hasSuccess: boolean) => void;
}

interface KustoQueryTestResponse {
    success: boolean;
    rowCount: number;
    columns?: string[];
    rows?: Record<string, unknown>[] | null;
    executionTimeMs?: number;
    errorMessage?: string | null;
}

export const KustoToolTestPanel: FC<KustoToolTestPanelProps> = ({ hasSuccessRunTest, setHasSuccessRunTest }) => {
    const intl = useIntl();
    const styles = useKustoToolCreateDialogStyles();
    const { values, isValid, dirty } = useFormikContext<KustoToolFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);
    const [isRunning, setIsRunning] = useState(false);
    const [testError, setTestError] = useState<string | null>(null);
    const [testResult, setTestResult] = useState<KustoQueryTestResponse | null>(null);

    const onRunTest = useCallback(async () => {
        setIsRunning(true);
        setHasSuccessRunTest(false);
        setTestError(null);
        setTestResult(null);

        const response = await extendedAgentClient.testKustoTool(values);

        if (response.isSuccessful && response.content) {
            const result = response.content as KustoQueryTestResponse;
            if (result.success) {
                setTestResult(result);
                setHasSuccessRunTest(true);
            } else {
                const errorMessage = result.errorMessage ?? '';
                const processedErrorMessage = errorMessage.includes('403')
                    ? parseKustoAuthorizationError(errorMessage)
                    : truncateErrorMessage(errorMessage);
                setTestError(
                    processedErrorMessage ??
                        truncateErrorMessage(errorMessage) ??
                        intl.formatMessage(ExtendedAgentsGraphResources.failedToRunTest)
                );
            }
        } else {
            setTestError(response.error ?? intl.formatMessage(ExtendedAgentsGraphResources.failedToRunTest));
        }

        setIsRunning(false);
    }, [extendedAgentClient, intl, setHasSuccessRunTest, values]);

    return (
        <>
            <div className={styles.testPanelHeader}>
                <Text size={300} weight="semibold">
                    {intl.formatMessage(ExtendedAgentsGraphResources.testQuery)}
                </Text>
                <Button appearance="primary" icon={<Play16Regular />} onClick={onRunTest} disabled={!dirty || !isValid || isRunning}>
                    {intl.formatMessage(ExtendedAgentsGraphResources.runTest)}
                </Button>
                {testError && (
                    <MessageBar className={styles.testPanelMessageBar} intent="error" icon={<WarningFilled />}>
                        <MessageBarBody>{testError}</MessageBarBody>
                    </MessageBar>
                )}
            </div>
            <TestValueAccordion />
            {testResult?.success && <KustoToolTestResults result={testResult} />}
            {!hasSuccessRunTest && <EmptyContent />}
        </>
    );
};

const EmptyContent = () => {
    const intl = useIntl();
    const styles = useKustoToolCreateDialogStyles();
    return (
        <div className={styles.emptyContent}>
            <img src="./AIChatLM.svg" alt="AI Chat" style={{ height: 128 }} />
            <Text size={300} align="center" style={{ color: tokens.colorNeutralForeground2, width: '400px' }}>
                {intl.formatMessage(ExtendedAgentsGraphResources.runATestMessage)}
            </Text>
        </div>
    );
};

export const KustoToolTestResults: FC<{ result: KustoQueryTestResponse }> = ({ result }) => {
    const styles = useKustoToolCreateDialogStyles();
    const intl = useIntl();
    const columns = result.columns ?? [];
    const rows: Record<string, unknown>[] = Array.isArray(result.rows) ? result.rows : [];

    return (
        <div className={styles.testResultsContainer}>
            <div className={styles.testResultsHeader}>
                <Text weight="semibold">
                    {intl.formatMessage(SreAgentResources.kustoQueryTesterResultsLabel, { count: result.rowCount })}
                </Text>
                {typeof result.executionTimeMs === 'number' && (
                    <Text size={200}>
                        {intl.formatMessage(SreAgentResources.kustoQueryTesterExecutionTime, {
                            milliseconds: result.executionTimeMs,
                        })}
                    </Text>
                )}
            </div>

            {rows.length > 0 ? (
                <div className={styles.testResultsTableWrapper}>
                    <table className={styles.testResultsTable}>
                        <thead className={styles.testResultsTableHead}>
                            <tr>
                                {columns.map((column: string, columnIndex: number) => (
                                    <th key={`${column}-${columnIndex}`} className={styles.testResultsTableHeader}>
                                        {column}
                                    </th>
                                ))}
                            </tr>
                        </thead>
                        <tbody>
                            {rows.map((row: Record<string, unknown>, rowIndex: number) => (
                                <tr key={rowIndex}>
                                    {columns.map((column: string, columnIndex: number) => (
                                        <td key={`${rowIndex}-${columnIndex}`} className={styles.testResultsTableCell}>
                                            {String(row[column] ?? '')}
                                        </td>
                                    ))}
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>
            ) : (
                <MessageBar intent="info">
                    <MessageBarBody>{intl.formatMessage(SreAgentResources.kustoQueryTesterNoResults)}</MessageBarBody>
                </MessageBar>
            )}
        </div>
    );
};
