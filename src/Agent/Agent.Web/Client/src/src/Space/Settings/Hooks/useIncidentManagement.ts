import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { IncidentManagementNotificationResources, IncidentManagementSaveErrorResources } from '../../../Strings/SREAgentResources';
import { IncidentManagementFormValues, IncidentManagementPlatform } from '../../Contracts/IncidentManagement';
import { useSreAgent } from './useSreAgent';

const getInitialValues = (agent?: ArmObj<Agent>): IncidentManagementFormValues => {
    const platform =
        agent?.properties?.incidentManagementConfiguration?.type === IncidentManagementType.PagerDuty
            ? IncidentManagementPlatform.PagerDuty
            : IncidentManagementPlatform.Disconnected;
    return {
        platform,
        connectionKey: agent?.properties?.incidentManagementConfiguration?.connectionKey,
    };
};

export function useIncidentManagement(resourceId: string) {
    const azPortalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const { agent, agentLoading, agentLoaded, agentLoadFailure } = useSreAgent(resourceId);
    const { subscription, resourceGroup } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);
    const [saving, setSaving] = useState(false);
    const [saveFailure, setSaveFailure] = useState<string>();

    const platform: IncidentManagementPlatform = useMemo(() => {
        return agent?.properties?.incidentManagementConfiguration?.type === IncidentManagementType.PagerDuty
            ? IncidentManagementPlatform.PagerDuty
            : IncidentManagementPlatform.Disconnected;
    }, [agent?.properties?.incidentManagementConfiguration?.type]);

    const [initialValues, setInitialValues] = useState<IncidentManagementFormValues>(getInitialValues(agent));

    useEffect(() => {
        setInitialValues(getInitialValues(agent));
    }, [agent]);

    const save = useCallback(
        (formValues: IncidentManagementFormValues) => {
            if (!agent) {
                return;
            }
            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.saveTitle),
                intl.formatMessage(IncidentManagementNotificationResources.saveStarted)
            );

            setSaving(true);
            setSaveFailure(undefined);

            if (formValues.platform == IncidentManagementPlatform.Disconnected) {
                azPortalContext.log({
                    action: 'save-incidentManagement',
                    actionModifier: 'start',
                    logLevel: 'info',
                    resourceId,
                });
                SreAgentClient.patchAgent(resourceId, {
                    properties: {
                        incidentManagementConfiguration: null,
                    },
                }).then(patchResult => {
                    if (!patchResult.metadata.success) {
                        azPortalContext.log({
                            action: 'save-incidentManagement',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId,
                            data: { error: patchResult.metadata.error },
                        });
                        setSaving(false);
                        setSaveFailure(intl.formatMessage(IncidentManagementSaveErrorResources.configFailure));
                        azPortalContext.stopNotification(
                            notificationId,
                            false,
                            intl.formatMessage(IncidentManagementNotificationResources.saveFailed, {
                                errorMessage: getErrorMessage(patchResult.metadata.error),
                            })
                        );
                    } else {
                        azPortalContext.log({
                            action: 'save-incidentManagement',
                            actionModifier: 'success',
                            logLevel: 'info',
                            resourceId,
                        });
                        setSaving(false);
                        setSaveFailure(undefined);
                        setInitialValues({ platform: formValues.platform });
                        azPortalContext.stopNotification(
                            notificationId,
                            true,
                            intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                        );
                    }
                });
            } else {
                azPortalContext.log({
                    action: 'save-incidentManagement',
                    actionModifier: 'start',
                    logLevel: 'info',
                    resourceId,
                });
                SreAgentClient.patchAgent(resourceId, {
                    properties: {
                        incidentManagementConfiguration: {
                            type: IncidentManagementType.PagerDuty,
                            connectionName: 'pagerduty',
                            connectionKey: formValues.connectionKey,
                        },
                    },
                }).then(patchResult => {
                    if (!patchResult.metadata.success) {
                        azPortalContext.log({
                            action: 'save-incidentManagement',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            resourceId,
                            data: { error: patchResult.metadata.error },
                        });
                        setSaving(false);
                        setSaveFailure(intl.formatMessage(IncidentManagementSaveErrorResources.configFailure));
                        azPortalContext.stopNotification(
                            notificationId,
                            false,
                            intl.formatMessage(IncidentManagementNotificationResources.saveFailed, {
                                errorMessage: getErrorMessage(patchResult.metadata.error),
                            })
                        );
                    } else {
                        azPortalContext.log({
                            action: 'save-incidentManagement',
                            actionModifier: 'success',
                            logLevel: 'info',
                            resourceId,
                        });
                        setSaving(false);
                        setSaveFailure(undefined);
                        setInitialValues({ platform: formValues.platform });
                        azPortalContext.stopNotification(
                            notificationId,
                            true,
                            intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                        );
                    }
                });
            }
        },
        [
            intl,
            subscription,
            resourceGroup,
            agent?.name,
            agent?.location,
            agent?.properties?.agentEndpoint,
            azPortalContext.startNotification,
            azPortalContext.stopNotification,
            intl.formatMessage,
        ]
    );

    return {
        loading: agentLoading,
        loaded: agentLoaded,
        loadFailure: agentLoadFailure,
        saving,
        saveFailure,
        platform,
        initialValues,
        save,
    };
}
