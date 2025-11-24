import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { MsiIdentity } from '../../../../Common/Contracts/Azure/ArmObj';
import { SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ConnectorType, getConnectorIcon, getConnectorName, getConnectorService } from '../Wizard/Common/ConnectorType';
import { getUserAssignedIdentityOptions, renderConnectorForm } from '../Wizard/Common/DialogHelper.tsx';
import { ConnectorFormProps } from '../Wizard/ConnectorWizardFormik.tsx';
import { useConnectorEditDialogStyles } from './ConnectorEditDialog.styles.tsx';

interface ConnectorEditDialogProps {
    isOpen: boolean;
    onOpenChange: (isOpen: boolean) => void;
    agentName?: string;
    agentLocation?: string;
    agentIdentity?: MsiIdentity;
    refreshAgent: () => void;
}

export const ConnectorEditDialog: React.FC<ConnectorEditDialogProps> = ({
    isOpen,
    onOpenChange,
    agentName,
    agentLocation,
    agentIdentity,
    refreshAgent,
}) => {
    const intl = useIntl();
    const styles = useConnectorEditDialogStyles();

    const { values, isValid, isSubmitting, submitForm, resetForm, dirty } = useFormikContext<ConnectorFormProps>();

    const userAssignedIdentityOptions = useMemo(() => getUserAssignedIdentityOptions(agentIdentity), [agentIdentity]);

    const handleCancel = useCallback(() => {
        resetForm();
        onOpenChange(false);
    }, [onOpenChange, resetForm]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => onOpenChange(data.open)}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <div className={styles.dialogTitle}>
                        <div className={styles.titleContent}>
                            <img
                                src={getConnectorIcon(values.connectorType as ConnectorType, intl)}
                                alt={getConnectorService(values.connectorType as ConnectorType, intl)}
                                className={styles.titleIcon}
                            />
                            <div className={styles.titleTextContainer}>
                                <div className={styles.titleText}>{getConnectorName(values.connectorType as ConnectorType, intl)}</div>
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
            </DialogSurface>
        </Dialog>
    );
};
