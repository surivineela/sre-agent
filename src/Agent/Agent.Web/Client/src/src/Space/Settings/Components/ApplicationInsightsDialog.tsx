import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    makeStyles,
    Option,
    Skeleton,
    SkeletonItem,
} from '@fluentui/react-components';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { AppInsightsClient } from '../../../Common/Clients/AppInsightsClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import SubscriptionClient from '../../../Common/Clients/SubscriptionClient';
import DropdownNoFormik from '../../../Common/Components/Dropdown/DropdownNoFormik';
import { Subscription } from '../../../Common/Contracts/Azure/Subscription';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { useApplicationInsights } from '../Hooks/useApplicationInsights';

const useStyles = makeStyles({
    dialogSurface: {
        fontSize: '14px',
        height: '35vh',
        maxHeight: '650px',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
        flex: '1',
        minHeight: '0',
    },
    dialogTitle: {
        paddingBottom: '15px',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '20px',
        flex: '1 1 auto',
        overflow: 'auto',
        minHeight: '0',
    },
    dropdown: {
        fontSize: '14px',
        maxWidth: '350px',
    },
    dialogActions: {
        padding: '12px 24px',
        paddingLeft: '0px',
    },
});

interface ApplicationInsightsDialogProps {
    isOpen: boolean;
    onClose: () => void;
    currentAppInsightsId?: string;
    agentResourceId: string;
    onSave: () => void;
}

export const ApplicationInsightsDialog = ({
    isOpen,
    onClose,
    currentAppInsightsId,
    agentResourceId,
    onSave,
}: ApplicationInsightsDialogProps) => {
    const intl = useIntl();
    const { startNotification, stopNotification, log } = useContext(AzPortalContext);
    const styles = useStyles();

    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [subscriptionsLoading, setSubscriptionsLoading] = useState(false);
    const [selectedSubscriptionId, setSelectedSubscriptionId] = useState<string | undefined>();
    const [selectedAppInsightsId, setSelectedAppInsightsId] = useState<string | undefined>(currentAppInsightsId);
    const [saving, setSaving] = useState(false);

    const { appInsights, isLoading: appInsightsLoading, refresh } = useApplicationInsights();

    useEffect(() => {
        if (isOpen) {
            setSubscriptionsLoading(true);
            SubscriptionClient.getSubscriptions()
                .then((response: any) => {
                    if (response?.metadata?.success && response.data?.value) {
                        setSubscriptions(response.data.value);

                        if (currentAppInsightsId) {
                            const subscriptionId = currentAppInsightsId.split('/')[2];
                            setSelectedSubscriptionId(subscriptionId);
                        }
                    }
                })
                .finally(() => setSubscriptionsLoading(false));
        }
    }, [isOpen, currentAppInsightsId]);

    useEffect(() => {
        if (selectedSubscriptionId) {
            refresh(selectedSubscriptionId);
        }
    }, [selectedSubscriptionId, refresh]);

    const subscriptionOptions = useMemo(() => {
        return subscriptions.map((sub: Subscription) => {
            const subscriptionId = sub.id.split('/')[2];
            return {
                id: subscriptionId,
                text: sub.displayName || subscriptionId,
                type: 0 as const,
            };
        });
    }, [subscriptions]);

    const appInsightsOptions = useMemo(() => {
        return appInsights.map((ai: any) => ({
            id: ai.id,
            text: ai.name,
            type: 0 as const,
        }));
    }, [appInsights]);

    const selectedSubscriptionValue = useMemo(() => {
        if (!selectedSubscriptionId) return '';
        const sub = subscriptions.find((sub: Subscription) => {
            const subscriptionId = sub.id.split('/')[2];
            return subscriptionId === selectedSubscriptionId;
        });
        return sub?.displayName || selectedSubscriptionId;
    }, [selectedSubscriptionId, subscriptions]);

    const selectedAppInsightsValue = useMemo(() => {
        return selectedAppInsightsId ? appInsights.find((ai: any) => ai.id === selectedAppInsightsId)?.name || '' : '';
    }, [selectedAppInsightsId, appInsights]);

    const hasChanged = useMemo(() => {
        return selectedAppInsightsId !== currentAppInsightsId;
    }, [selectedAppInsightsId, currentAppInsightsId]);

    const canSave = useMemo(() => {
        return hasChanged && selectedAppInsightsId && !saving;
    }, [hasChanged, selectedAppInsightsId, saving]);

    const handleSave = useCallback(async () => {
        if (!selectedAppInsightsId) return;

        const notificationId = startNotification(
            intl.formatMessage(SreAgentResources.updatingApplicationInsights),
            intl.formatMessage(SreAgentResources.updatingApplicationInsights)
        );

        onClose();
        setSaving(true);

        // Fetch the App Insights component details to get appId and connectionString
        const appInsightsResponse = await AppInsightsClient.getAppInsightsComponentById(selectedAppInsightsId);

        if (!appInsightsResponse.isSuccessful || !appInsightsResponse.data) {
            setSaving(false);
            stopNotification(notificationId, false, intl.formatMessage(SreAgentResources.failedToUpdateApplicationInsights));
            log({
                action: 'saveApplicationInsights',
                actionModifier: 'failed',
                resourceId: agentResourceId,
                logLevel: 'error',
                data: {
                    error: appInsightsResponse.error || 'Failed to fetch App Insights component details',
                },
            });
            return;
        }

        const { appId, connectionString } = appInsightsResponse.data;

        const response = await SreAgentClient.patchAgent(agentResourceId, {
            properties: {
                logConfiguration: {
                    applicationInsightsConfiguration: {
                        appId,
                        connectionString,
                        applicationInsightsResourceId: selectedAppInsightsId,
                    },
                },
            },
        });

        setSaving(false);

        if (response.metadata?.success) {
            stopNotification(notificationId, true, intl.formatMessage(SreAgentResources.applicationInsightsUpdatedSuccessfully));
            onSave();
        } else {
            stopNotification(notificationId, false, intl.formatMessage(SreAgentResources.failedToUpdateApplicationInsights));
            log({
                action: 'saveApplicationInsights',
                actionModifier: 'failed',
                resourceId: agentResourceId,
                logLevel: 'error',
                data: {
                    error: response.metadata?.error || 'Unknown error',
                },
            });
        }
    }, [selectedAppInsightsId, agentResourceId, onSave, onClose, startNotification, stopNotification, intl, log]);

    const handleCancel = useCallback(() => {
        setSelectedSubscriptionId(undefined);
        setSelectedAppInsightsId(currentAppInsightsId);
        onClose();
    }, [currentAppInsightsId, onClose]);

    return (
        <Dialog open={isOpen} onOpenChange={(_, data) => !data.open && handleCancel()}>
            <DialogSurface className={styles.dialogSurface}>
                <DialogBody className={styles.dialogBody}>
                    <DialogTitle className={styles.dialogTitle}>
                        {intl.formatMessage(
                            currentAppInsightsId ? SreAgentResources.editApplicationInsights : SreAgentResources.addApplicationInsights
                        )}
                    </DialogTitle>
                    <DialogContent className={styles.dialogContent}>
                        <DropdownNoFormik
                            label={intl.formatMessage(SreAgentResources.subscription)}
                            placeholder={intl.formatMessage(SreAgentResources.subscription)}
                            options={subscriptionOptions}
                            value={selectedSubscriptionValue}
                            onOptionSelect={(_, data) => {
                                setSelectedSubscriptionId(data.optionValue);
                                setSelectedAppInsightsId(undefined);
                            }}
                            isLoading={subscriptionsLoading}
                            required
                            orientation="vertical"
                            className={styles.dropdown}
                        >
                            {subscriptionsLoading ? (
                                <Skeleton>
                                    <SkeletonItem />
                                </Skeleton>
                            ) : (
                                subscriptionOptions.map(option => (
                                    <Option key={option.id} value={option.id}>
                                        {option.text}
                                    </Option>
                                ))
                            )}
                        </DropdownNoFormik>

                        <DropdownNoFormik
                            label={intl.formatMessage(SreAgentResources.applicationInsightsResource)}
                            placeholder={intl.formatMessage(SreAgentResources.selectApplicationInsightsResource)}
                            options={appInsightsOptions}
                            value={selectedAppInsightsValue}
                            onOptionSelect={(_, data) => setSelectedAppInsightsId(data.optionValue)}
                            isLoading={appInsightsLoading}
                            disabled={!selectedSubscriptionId}
                            required
                            orientation="vertical"
                            className={styles.dropdown}
                        >
                            {appInsightsLoading ? (
                                <Skeleton>
                                    <SkeletonItem />
                                </Skeleton>
                            ) : appInsightsOptions.length === 0 && selectedSubscriptionId ? (
                                <Option disabled>
                                    {intl.formatMessage(SreAgentResources.noApplicationInsightsResourcesFoundInSubscription)}
                                </Option>
                            ) : (
                                appInsightsOptions.map((option: any) => (
                                    <Option key={option.id} value={option.id}>
                                        {option.text}
                                    </Option>
                                ))
                            )}
                        </DropdownNoFormik>
                    </DialogContent>
                    <DialogActions className={styles.dialogActions}>
                        <Button appearance="primary" onClick={handleSave} disabled={!canSave}>
                            {intl.formatMessage(SreAgentResources.save)}
                        </Button>
                        <Button appearance="secondary" onClick={handleCancel}>
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
