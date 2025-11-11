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
import { FC, useState } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedConnector } from '../../Contracts/ExtendedAgentGraph';
import { SettingsKeys } from '../../Settings/Settings.ReactView';
import { useKustoToolSettings } from './Hooks/useKustoToolSettings';
import { useKustoToolCreateDialogStyles } from './KustoToolCreateDialog.Styles';
import { KustoToolCreateForm } from './KustoToolCreateForm';
import { KustoToolTestPanel } from './KustoToolTestPanel';
import { KustoToolFormProps } from './KustoToolUtilities';

interface KustoToolCreateDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (open: boolean) => void;
    connectors: ExtendedConnector[];
}

export const KustoToolCreateDialog: FC<KustoToolCreateDialogProps> = ({ isDialogOpen, setIsDialogOpen, connectors }) => {
    const intl = useIntl();
    const navigate = useNavigate();
    const styles = useKustoToolCreateDialogStyles();
    const { initialValues, validationSchema, save } = useKustoToolSettings();
    const [hasSuccessRunTest, setHasSuccessRunTest] = useState<boolean>(false);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)} modalType="alert">
            <Formik<KustoToolFormProps> initialValues={initialValues} validationSchema={validationSchema} onSubmit={save}>
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
                                        {intl.formatMessage(ExtendedAgentsGraphResources.createKustoTool)}
                                    </DialogTitle>
                                </div>
                                <DialogContent>
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
                                                            pathname: `/views/settings/${SettingsKeys.Connectors}`,
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
                                            {intl.formatMessage(ExtendedAgentsGraphResources.createTool)}
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
