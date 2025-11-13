import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { MsiIdentity } from '../../../../Common/Contracts/Azure/ArmObj';
import { Connector } from '../../../../Common/Contracts/Azure/SreAgent';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ConnectorType, getConnectorIcon, getConnectorName, getConnectorService } from '../Wizard/Common/ConnectorType';
import { getUserAssignedIdentityOptions, handleConnectorSubmit, renderConnectorForm } from '../Wizard/Common/DialogHelper.tsx';
import { getValidationSchema } from '../Wizard/Common/ValidationHelper.ts';
import { AuthType, ConnectorFormProps } from '../Wizard/ConnectorWizardFormik';
import { useConnectorEditDialogStyles } from './ConnectorEditDialog.styles.tsx';

interface ConnectorEditDialogProps {
    isOpen: boolean;
    onOpenChange: (isOpen: boolean) => void;
    connector: Connector;
    onSubmit: (connector: Connector) => void;
    agentName?: string;
    agentLocation?: string;
    agentIdentity?: MsiIdentity;
    refreshAgent: () => void;
}

export const ConnectorEditDialog: React.FC<ConnectorEditDialogProps> = ({
    isOpen,
    onOpenChange,
    connector,
    onSubmit,
    agentName,
    agentLocation,
    agentIdentity,
    refreshAgent,
}) => {
    const intl = useIntl();
    const styles = useConnectorEditDialogStyles();

    const initialFormValues = useMemo((): ConnectorFormProps => {
        const connectorType = connector.dataConnectorType as ConnectorType;
        const initialFormProps: ConnectorFormProps = {
            connectorType: connector.dataConnectorType,
            name: connector.name,
            url: connector.dataSource || '',
            identity: connector.identity,
            authType: undefined,
            patOrApiKey: '',
            email: '',
            teamsChannelLink: '',
            channelId: '',
            teamsGroupId: '',
            customHeaders: [{ key: '', value: '' }],
        };

        if (connectorType === ConnectorType.TeamsSendNotification) {
            const parts = connector.dataSource?.split(';');
            if (parts && parts.length >= 3) {
                initialFormProps.url = parts[0];
                initialFormProps.teamsGroupId = parts[1];
                initialFormProps.channelId = parts[2];
            }
        } else if (connectorType === ConnectorType.McpServer || connectorType === ConnectorType.GitHub) {
            const parts = connector.dataSource?.split(';');
            if (parts && parts.length >= 2) {
                const endpointPart = parts.find(part => part.includes('Endpoint='));
                const endpoint = endpointPart?.split('=')[1];
                initialFormProps.url = endpoint || '';

                const authTypePart = parts.find(part => part.includes('AuthType='));
                const authType = authTypePart?.split('=')[1] as AuthType;
                initialFormProps.authType = authType;

                if (authType === AuthType.BearerToken) {
                    const bearerTokenPart = parts.find(part => part.includes('BearerToken='));
                    const bearerToken = bearerTokenPart?.split('=')[1];

                    initialFormProps.patOrApiKey = bearerToken || '';
                } else if (authType === AuthType.CustomHeaders) {
                    const customHeaders = parts
                        .filter(part => !part.includes('Endpoint=') && !part.includes('AuthType='))
                        .map(part => {
                            const partKeyValuePair = part.split('=');
                            return { key: partKeyValuePair[0], value: partKeyValuePair[1] };
                        });

                    initialFormProps.customHeaders = customHeaders;
                }
            }
        }
        return initialFormProps;
    }, [connector]);

    const validationSchema = useMemo(() => getValidationSchema([], intl, true), [intl]);

    const userAssignedIdentityOptions = useMemo(() => getUserAssignedIdentityOptions(agentIdentity), [agentIdentity]);

    const handleSubmit = useCallback(
        async (values: ConnectorFormProps, formikHelpers: FormikHelpers<ConnectorFormProps>) => {
            await handleConnectorSubmit({
                values,
                formikHelpers,
                onSubmit,
                onClose: () => onOpenChange(false),
            });
        },
        [onSubmit, onOpenChange]
    );

    const handleCancel = useCallback(() => {
        onOpenChange(false);
    }, [onOpenChange]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <Formik
                    initialValues={initialFormValues}
                    validationSchema={validationSchema}
                    onSubmit={handleSubmit}
                    enableReinitialize={true}
                    validateOnChange={true}
                >
                    {({ values, isValid, isSubmitting, submitForm, dirty }) => (
                        <DialogBody className={styles.dialogBody}>
                            <div className={styles.dialogTitle}>
                                <div className={styles.titleContent}>
                                    <img
                                        src={getConnectorIcon(values.connectorType as ConnectorType, intl)}
                                        alt={getConnectorService(values.connectorType as ConnectorType, intl)}
                                        className={styles.titleIcon}
                                    />
                                    <div className={styles.titleTextContainer}>
                                        <div className={styles.titleText}>
                                            {getConnectorName(values.connectorType as ConnectorType, intl)}
                                        </div>
                                        <div className={styles.subtitleText}>
                                            {getConnectorService(values.connectorType as ConnectorType, intl)}
                                        </div>
                                    </div>
                                </div>
                                <Button
                                    appearance="transparent"
                                    icon={<Dismiss24Regular />}
                                    onClick={handleCancel}
                                    aria-label={intl.formatMessage(SreAgentResources.close)}
                                />
                            </div>
                            <DialogContent className={styles.dialogContent}>
                                {renderConnectorForm({
                                    connectorType: values.connectorType,
                                    userAssignedIdentityOptions,
                                    agentIdentity,
                                    agentName,
                                    agentLocation,
                                    refreshAgent,
                                    isEditMode: true,
                                })}
                            </DialogContent>
                            <DialogActions className={styles.dialogActions}>
                                <div className={styles.actionsContainer}>
                                    <Button appearance="primary" onClick={submitForm} disabled={!isValid || isSubmitting || !dirty}>
                                        {intl.formatMessage(SreAgentResources.save)}
                                    </Button>
                                    <Button appearance="secondary" onClick={handleCancel}>
                                        {intl.formatMessage(SreAgentResources.cancel)}
                                    </Button>
                                </div>
                            </DialogActions>
                        </DialogBody>
                    )}
                </Formik>
            </DialogSurface>
        </Dialog>
    );
};
