import { Button, MessageBar, MessageBarBody, Text, tokens } from '@fluentui/react-components';
import { Play16Regular, WarningFilled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { ExtendedAgentClient } from '../../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { TestValueAccordion } from './Common/TestValueAccordion';
import { useKustoToolCreateDialogStyles } from './KustoToolCreateDialog.Styles';
import { KustoToolFormProps } from './KustoToolUtilities';

interface KustoToolTestPanelProps {
    hasSuccessRunTest: boolean;
    setHasSuccessRunTest: (hasSuccess: boolean) => void;
}

export const KustoToolTestPanel: FC<KustoToolTestPanelProps> = ({ hasSuccessRunTest, setHasSuccessRunTest }) => {
    const intl = useIntl();
    const styles = useKustoToolCreateDialogStyles();
    const { values, isValid, dirty } = useFormikContext<KustoToolFormProps>();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);
    const [isRunning, setIsRunning] = useState(false);
    const [testError, setTestError] = useState<string | null>(null);

    const onRunTest = useCallback(async () => {
        setIsRunning(true);
        try {
            const response = await extendedAgentClient.testKustoTool(values);
            if (response.isSuccessful) {
                setHasSuccessRunTest(true);
            } else {
                setTestError(response.error ?? null);
            }
        } catch (error) {
            const errorMessage = getErrorMessage(error);
            console.error('Test execution failed:', errorMessage);
            setTestError(errorMessage);
        } finally {
            setIsRunning(false);
        }
    }, [extendedAgentClient, setHasSuccessRunTest, values]);

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
                    <MessageBar intent="error" icon={<WarningFilled />} style={{ width: '100%' }}>
                        <MessageBarBody>{testError}</MessageBarBody>
                    </MessageBar>
                )}
            </div>
            <TestValueAccordion />
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
