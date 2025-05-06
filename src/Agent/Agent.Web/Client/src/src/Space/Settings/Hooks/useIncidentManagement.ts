import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent, IncidentManagementConfiguration, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { IncidentManagementNotificationResources, IncidentManagementSaveErrorResources } from '../../../Strings/SREAgentResources';
import { IncidentManagementFormValues, IncidentManagementPlatform } from '../../Contracts/IncidentManagement';
import { useSreAgent } from './useSreAgent';

const getIncidentManagementPlatform = (agent?: ArmObj<Agent>): IncidentManagementPlatform => {
    switch (agent?.properties?.incidentManagementConfiguration?.type) {
        case IncidentManagementType.PagerDuty:
            return IncidentManagementPlatform.PagerDuty;
        case IncidentManagementType.AzMonitor:
            return IncidentManagementPlatform.AzMonitor;
        default:
            return IncidentManagementPlatform.Disconnected;
    }
};

const getInitialValues = (agent?: ArmObj<Agent>): IncidentManagementFormValues => {
    return {
        platform: getIncidentManagementPlatform(agent),
        connectionKey: agent?.properties?.incidentManagementConfiguration?.connectionKey,
    };
};

const generateIncidentManagementConfiguration = (formValues: IncidentManagementFormValues): IncidentManagementConfiguration | null => {
    switch (formValues.platform) {
        case IncidentManagementPlatform.Disconnected:
            return null;
        case IncidentManagementPlatform.PagerDuty:
            return {
                type: IncidentManagementType.PagerDuty,
                connectionName: 'pagerduty',
                connectionKey: formValues.connectionKey,
            };
        case IncidentManagementPlatform.AzMonitor:
            return {
                type: IncidentManagementType.AzMonitor,
                connectionName: 'azmonitor',
            };
        default:
            throw new Error(`Unknown incident management platform: ${formValues.platform}`);
    }
};

export function useIncidentManagement(resourceId: string) {
    const azPortalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const { agent, agentLoading, agentLoaded, agentLoadFailure } = useSreAgent(resourceId);
    const { subscription, resourceGroup } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);
    const [saving, setSaving] = useState(false);
    const [saveFailure, setSaveFailure] = useState<string>();

    const platform: IncidentManagementPlatform = useMemo(
        () => getIncidentManagementPlatform(agent),
        [agent?.properties?.incidentManagementConfiguration?.type]
    );

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

            const additionalInfo = {
                platform: formValues.platform,
                previousPlatform: initialValues.platform,
            };

            azPortalContext.log({
                action: 'save-incidentManagement',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId,
                data: additionalInfo,
            });

            SreAgentClient.patchAgent(resourceId, {
                properties: {
                    incidentManagementConfiguration: generateIncidentManagementConfiguration(formValues),
                },
            }).then(patchResult => {
                if (!patchResult.metadata.success) {
                    azPortalContext.log({
                        action: 'save-incidentManagement',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId,
                        data: { ...additionalInfo, error: patchResult.metadata.error },
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
                        data: additionalInfo,
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
        },
        [
            subscription,
            resourceGroup,
            agent?.name,
            agent?.location,
            agent?.properties?.agentEndpoint,
            azPortalContext.startNotification,
            azPortalContext.stopNotification,
            intl.formatMessage,
            initialValues.platform,
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
