import {
    Button,
    Card,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Text,
} from '@fluentui/react-components';
import { Delete16Regular } from '@fluentui/react-icons';
import { useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessageOrStringify } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import PermissionedButton from '../../../Common/Components/PermissionedButton';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { useDialogStyles, useSettingsStyles } from '../Styles/Settings.styles';

export interface DeleteAgentCardProps {
    resourceId: string;
    resourceName: string;
    canDeleteAgent: boolean;
}

export const DeleteAgentCard = ({ resourceId, resourceName, canDeleteAgent }: DeleteAgentCardProps) => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const dialogStyles = useDialogStyles();
    const az = useContext(AzPortalContext);

    const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);

    const onDeleteAgent = useCallback(async () => {
        setDeleteDialogOpen(false);
        const notificationId = az.startNotification(
            intl.formatMessage(SreAgentResources.deleteAgentNotificationTitle, { count: 1 }),
            intl.formatMessage(SreAgentResources.deleteAgentNotificationInProgress, { count: 1, name: resourceName })
        );

        az.log({
            action: 'deleteAgent',
            actionModifier: 'started',
            resourceId,
            logLevel: 'info',
            data: {
                resourceId,
            },
        });
        az.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'confirmDeleteAgent',
            targetFriendlyName: 'Confirm delete agent',
            valueObjectName: resourceId,
            valueObjectFriendlyName: resourceId,
        });

        const response = await SreAgentClient.deleteAgent(resourceId);

        if (response.metadata.success) {
            az.stopNotification(
                notificationId,
                true,
                intl.formatMessage(SreAgentResources.deleteAgentNotificationSuccess, { count: 1, name: resourceName })
            );
            az.log({
                action: 'deleteAgent',
                actionModifier: 'succeeded',
                resourceId,
                logLevel: 'info',
                data: {
                    resourceId,
                },
            });
            az.openBlade({
                extension: 'Microsoft_Azure_PaasServerless',
                detailBlade: 'SreAgentHome.ReactView',
                detailBladeInputs: {},
            });
        } else {
            az.stopNotification(
                notificationId,
                false,
                intl.formatMessage(SreAgentResources.deleteAgentNotificationFailure, {
                    count: 1,
                    name: resourceName,
                    errorMessage: getErrorMessageOrStringify(response.metadata.error),
                })
            );
            az.log({
                action: 'deleteAgent',
                actionModifier: 'failed',
                resourceId,
                logLevel: 'error',
                data: {
                    resourceId,
                    error: response.metadata.error,
                },
            });
        }
    }, [az, intl, resourceId, resourceName]);

    const onOpenDialog = useCallback(() => {
        setDeleteDialogOpen(true);
        az.logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'deleteAgent',
            targetFriendlyName: 'Delete agent (dialog)',
            valueObjectName: SpecialControlValue.DoAction,
            valueObjectFriendlyName: SpecialControlValue.DoAction,
        });
    }, [az]);

    return (
        <Card style={{ ...styles.basicsCardStyle, marginBottom: 6 }}>
            <div style={styles.actionSectionStyle}>
                <div style={styles.actionTextContainerStyle}>
                    <div style={styles.sectionTitleStyle}>{intl.formatMessage(SreAgentResources.deleteAgentTitle)}</div>
                    <Text style={styles.sectionDescriptionStyle}>{intl.formatMessage(SreAgentResources.deleteAgentDescription)}</Text>
                </div>
                <Dialog open={deleteDialogOpen}>
                    <DialogTrigger disableButtonEnhancement>
                        <PermissionedButton
                            icon={<Delete16Regular />}
                            appearance="primary"
                            className={dialogStyles.dangerButton}
                            canPerform={canDeleteAgent}
                            noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionDeleteAgent)}
                            onClick={onOpenDialog}
                        >
                            {intl.formatMessage(SreAgentResources.delete)}
                        </PermissionedButton>
                    </DialogTrigger>
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle>{intl.formatMessage(SreAgentResources.deleteAgentTitle)}</DialogTitle>
                            <DialogContent>{intl.formatMessage(SreAgentResources.deleteAgentDescription)}</DialogContent>
                            <DialogActions>
                                <Button appearance="primary" className={dialogStyles.dangerButton} onClick={onDeleteAgent}>
                                    {intl.formatMessage(SreAgentResources.yes)}
                                </Button>
                                <DialogTrigger disableButtonEnhancement>
                                    <Button appearance="secondary" onClick={() => setDeleteDialogOpen(false)}>
                                        {intl.formatMessage(SreAgentResources.no)}
                                    </Button>
                                </DialogTrigger>
                            </DialogActions>
                        </DialogBody>
                    </DialogSurface>
                </Dialog>
            </div>
        </Card>
    );
};
