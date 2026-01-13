import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular, WarningFilled } from '@fluentui/react-icons';
import { Formik } from 'formik';
import { FC, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedConnector, ExtendedTool } from '../../Contracts/ExtendedAgentGraph';
import { PrimaryNavItemValues, SecondaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import { useKustoToolSettings } from './Hooks/useKustoToolSettings';
import { useKustoToolCreateDialogStyles } from './KustoToolDialog.Styles';
import { KustoToolCreateForm } from './KustoToolForm';
import { KustoQueryTestResponse, KustoToolTestPanel } from './KustoToolTestPanel';
import { KustoToolFormProps } from './KustoToolUtilities';

interface KustoToolDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (open: boolean) => void;
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

export const KustoToolDialog: FC<KustoToolDialogProps> = ({
    isDialogOpen,
    setIsDialogOpen,
    connectors,
    agentName,
    addToolsToAgent,
    refresh,
    kustoTool,
    mode,
}) => {
    const intl = useIntl();
    const navigate = useAgentSiteNavigate();
    const styles = useKustoToolCreateDialogStyles();
    const {
        initialValues,
        validationSchema,
        save: saveKustoToolSettings,
    } = useKustoToolSettings(mode, mode === KustoToolDialogMode.Edit ? kustoTool : undefined);
    const [hasSuccessRunTest, setHasSuccessRunTest] = useState<boolean>(false);
    const [successfulTestRunResult, setSuccessfulTestRunResult] = useState<KustoQueryTestResponse | null>(null);

    useEffect(() => {
        if (isDialogOpen) {
            setHasSuccessRunTest(false);
            setSuccessfulTestRunResult(null);
        }
    }, [isDialogOpen]);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)} modalType="alert">
            <Formik<KustoToolFormProps>
                initialValues={initialValues}
                validationSchema={validationSchema}
                onSubmit={async (values: KustoToolFormProps) => {
                    const response = await saveKustoToolSettings(values);
                    if (response?.isSuccessful) {
                        setIsDialogOpen(false);
                        if (mode === KustoToolDialogMode.Create) {
                            if (agentName) {
                                await addToolsToAgent(agentName, [values.name], []);
                            }
                        } else {
                            refresh?.();
                        }
                    }
                }}
            >
                {({ submitForm, dirty, isValid }) => {
                    return (
                        <DialogSurface className={styles.dialogSurface}>
                            <DialogBody className={styles.dialogBody}>
                                <div className={styles.dialogTitleWrapper}>
                                    <DialogTitle
                                        className={styles.dialogTitle}
                                        action={
                                            <ToolbarButton
                                                aria-label={intl.formatMessage(SreAgentResources.close)}
                                                appearance="transparent"
                                                icon={<Dismiss24Regular />}
                                                onClick={() => setIsDialogOpen(false)}
                                            />
                                        }
                                    >
                                        {mode === KustoToolDialogMode.Create
                                            ? intl.formatMessage(ExtendedAgentsGraphResources.createKustoTool)
                                            : intl.formatMessage(ExtendedAgentsGraphResources.editKustoTool)}
                                    </DialogTitle>
                                </div>
                                <DialogContent className={styles.dialogContent}>
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
                                                            primaryNavItemValue: PrimaryNavItemValues.Settings,
                                                            secondaryNavItemValue: SecondaryNavItemValues.Connectors,
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
                                            />
                                        </div>
                                    </div>
                                </DialogContent>
                                <DialogActions className={styles.dialogActions}>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button
                                            appearance="primary"
                                            onClick={submitForm}
                                            disabled={!dirty || !isValid || !hasSuccessRunTest}
                                        >
                                            {mode === KustoToolDialogMode.Create
                                                ? intl.formatMessage(ExtendedAgentsGraphResources.createTool)
                                                : intl.formatMessage(SreAgentResources.save)}
                                        </Button>
                                    </DialogTrigger>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary" onClick={e => e.stopPropagation()}>
                                            {intl.formatMessage(SreAgentResources.cancel)}
                                        </Button>
                                    </DialogTrigger>
                                </DialogActions>
                            </DialogBody>
                        </DialogSurface>
                    );
                }}
            </Formik>
        </Dialog>
    );
};
