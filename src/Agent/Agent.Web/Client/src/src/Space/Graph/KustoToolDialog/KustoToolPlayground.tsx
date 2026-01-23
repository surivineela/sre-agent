import {
    Button,
    DialogActions,
    DialogContent,
    DialogTrigger,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
} from '@fluentui/react-components';
import { WarningFilled } from '@fluentui/react-icons';
import { Formik } from 'formik';
import { FC, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedConnector, ExtendedTool } from '../../Contracts/ExtendedAgentGraph';
import { SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useKustoToolSettings } from './Hooks/useKustoToolSettings';
import { useKustoToolCreateDialogStyles } from './KustoToolDialog.Styles';
import { KustoToolCreateForm } from './KustoToolForm';
import { KustoQueryTestResponse, KustoToolTestPanel } from './KustoToolTestPanel';
import { KustoToolFormProps } from './KustoToolUtilities';

interface KustoToolPlaygroundProps {
    connectors: ExtendedConnector[];
    agentName?: string;
    addToolsToAgent: (agentName: string, nonMcpToolNames: string[], mcpToolNames: string[]) => void;
    refresh?: () => void;
    kustoTool?: ExtendedTool;
    mode: KustoToolDialogMode;
}

export enum KustoToolDialogMode {
    Create,
    Edit,
}

export const KustoToolPlayground: FC<KustoToolPlaygroundProps> = ({ connectors, refresh, kustoTool, mode }) => {
    const intl = useIntl();
    const navigate = useNavigate();
    const styles = useKustoToolCreateDialogStyles();
    const {
        initialValues,
        validationSchema,
        save: saveKustoToolSettings,
    } = useKustoToolSettings(mode, mode === KustoToolDialogMode.Edit ? kustoTool : undefined);
    const [hasSuccessRunTest, setHasSuccessRunTest] = useState<boolean>(false);
    const [successfulTestRunResult, setSuccessfulTestRunResult] = useState<KustoQueryTestResponse | null>(null);

    useEffect(() => {
        setHasSuccessRunTest(false);
        setSuccessfulTestRunResult(null);
    }, []);

    return (
        <Formik<KustoToolFormProps>
            initialValues={initialValues}
            validationSchema={validationSchema}
            onSubmit={async (values: KustoToolFormProps) => {
                const response = await saveKustoToolSettings(values);
                if (response?.isSuccessful) {
                    refresh?.();
                }
            }}
        >
            {({ submitForm, dirty, isValid, resetForm }) => {
                return (
                    <div className={styles.fullScreenBody}>
                        <DialogContent className={styles.fullScreenContent}>
                            {/* No connectors message bar */}
                            {connectors?.length === 0 && (
                                <MessageBar intent="warning" icon={<WarningFilled />}>
                                    <MessageBarBody>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.toolNoConnectorsMessage)}
                                    </MessageBarBody>
                                    <MessageBarActions>
                                        <Button
                                            appearance="secondary"
                                            onClick={() => {
                                                navigate({
                                                    ...location,
                                                    pathname: `/views/settings/${SecondaryNavItemValues.Connectors}`,
                                                });
                                            }}
                                        >
                                            {intl.formatMessage(ExtendedAgentsGraphResources.goToConnectors)}
                                        </Button>
                                    </MessageBarActions>
                                </MessageBar>
                            )}

                            {/* Tool form */}
                            <div className={styles.toolForm}>
                                <div className={styles.toolFormLeft}>
                                    <KustoToolCreateForm connectors={connectors} />
                                </div>
                                <div className={styles.toolFormDivider}></div>
                                <div className={styles.toolFormRight}>
                                    <KustoToolTestPanel
                                        hasSuccessRunTest={hasSuccessRunTest}
                                        setHasSuccessRunTest={setHasSuccessRunTest}
                                        successfulTestRunResult={successfulTestRunResult}
                                        setSuccessfulTestRunResult={setSuccessfulTestRunResult}
                                        isPlaygroundMode={true}
                                    />
                                </div>
                            </div>
                        </DialogContent>
                        <DialogActions className={styles.dialogActions}>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="primary" onClick={submitForm} disabled={!dirty || !isValid || !hasSuccessRunTest}>
                                    {mode === KustoToolDialogMode.Create
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.createTool)
                                        : intl.formatMessage(SreAgentResources.save)}
                                </Button>
                            </DialogTrigger>
                            <DialogTrigger disableButtonEnhancement>
                                <Button appearance="secondary" onClick={() => resetForm()} disabled={!dirty}>
                                    {intl.formatMessage(SreAgentResources.discard)}
                                </Button>
                            </DialogTrigger>
                        </DialogActions>
                    </div>
                );
            }}
        </Formik>
    );
};
