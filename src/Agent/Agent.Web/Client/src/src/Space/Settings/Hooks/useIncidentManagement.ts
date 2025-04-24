import { useCallback, useContext, useMemo, useState } from "react";
import { AzPortalContext } from "../../../Common/AzPortalProxy/Providers/AzPortalProxyContext";
import LogicAppClient, { generatePagerDutyLogicAppPayload } from "../../../Common/Clients/LogicAppClient";
import ManagedConnectionClient from "../../../Common/Clients/ManagedConnectionClient";
import SreAgentClient from "../../../Common/Clients/SreAgentClient";
import { IncidentManagementType } from "../../../Common/Contracts/Azure/SreAgent";
import { ArmResourceDescriptor } from "../../../Common/Helpers/ResourceDescriptors";
import { IncidentManagementFormValues, IncidentManagementPlatform } from "../../Contracts/IncidentManagement";
import { useSreAgent } from "./useSreAgent";
import { IncidentManagementNotifications, IncidentManagementSaveErrors } from "../../../Strings/SREResources.resjson";

export function useIncidentManagement(resourceId: string) {
    const azPortalContext = useContext(AzPortalContext);

    const { agent, agentLoading, agentLoaded, agentLoadFailure } = useSreAgent(resourceId);
    const { subscription, resourceGroup } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);
    const [saving, setSaving] = useState(false);
    const [saveFailure, setSaveFailure] = useState<string>();

    const save = useCallback((formValues: IncidentManagementFormValues) => {
        if (!agent) {
            return;
        }

        const location = agent.location;
        const connectionName = "pagerduty";
        const connectionResourceId = `/subscriptions/${subscription}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${connectionName}`;
        const managedApiResourceId = `/subscriptions/${subscription}/providers/Microsoft.Web/locations/${location}/managedApis/${connectionName}`;

        const notificationId = azPortalContext.startNotification(IncidentManagementNotifications.saveTitle, IncidentManagementNotifications.saveStarted);

        setSaving(true);
        setSaveFailure(undefined);

        if (formValues.platform == IncidentManagementPlatform.Disconnected) {
            SreAgentClient.patchAgent(
                resourceId,
                {
                    properties: {
                        incidentManagementConfiguration: null
                    }
                }
            ).then(patchResult => {
                if (!patchResult.metadata.success) {
                    setSaving(false);
                    setSaveFailure(IncidentManagementSaveErrors.configFailure);
                    azPortalContext.stopNotification(notificationId, false, IncidentManagementNotifications.saveFailed);
                } else {
                    setSaving(false);
                    setSaveFailure(undefined);
                    azPortalContext.stopNotification(notificationId, true, IncidentManagementNotifications.saveSucceeded);
                }
            });
        }
    
        ManagedConnectionClient.putManagedConnection(
            connectionResourceId,
            {
                id: connectionResourceId,
                name: connectionName,
                kind: 'V1',
                location: location,
                properties: {
                    api: { id: managedApiResourceId },
                    parameterValues: {
                        apiKey: formValues.connectionKey!,
                    },
                    displayName: 'pagerDuty'
                }
            }
        ).then(managedConnectionResult => {
            if (!managedConnectionResult.metadata.success) {
                setSaving(false);
                setSaveFailure(IncidentManagementSaveErrors.managedConnectionFailure);
                azPortalContext.stopNotification(notificationId, false, IncidentManagementNotifications.saveFailed);
            } else {
                const logicAppName = `${agent.name}-pagerduty`;
                const logicAppResourceId = `/subscriptions/${subscription}/resourceGroups/${resourceGroup}/providers/Microsoft.Logic/workflows/${logicAppName}`;
                const logicAppPayload = generatePagerDutyLogicAppPayload(
                    logicAppResourceId,
                    logicAppName,
                    location,
                    agent.properties.agentEndpoint,
                    formValues.connectionKey!,
                    managedApiResourceId,
                    connectionResourceId,
                    connectionName
                );
                LogicAppClient.putPagerDutyLogicApp(
                    logicAppResourceId,
                    logicAppPayload
                ).then(logicAppResult => {
                    if (!logicAppResult.metadata.success) {
                        setSaving(false);
                        setSaveFailure(IncidentManagementSaveErrors.logicAppFailure);
                        azPortalContext.stopNotification(notificationId, false, IncidentManagementNotifications.saveFailed);
                    } else {
                        SreAgentClient.patchAgent(
                            resourceId,
                            {
                                properties: {
                                    incidentManagementConfiguration: {
                                        type: IncidentManagementType.PagerDuty,
                                        connectionName: connectionName,
                                        connectionUrl: formValues.connectionUrl,
                                        connectionKey: formValues.connectionKey,
                                    }
                                }
                            }
                        ).then(patchResult => {
                            if (!patchResult.metadata.success) {
                                setSaving(false);
                                setSaveFailure(IncidentManagementSaveErrors.configFailure);
                                azPortalContext.stopNotification(notificationId, false, IncidentManagementNotifications.saveFailed);
                            } else {
                                setSaving(false);
                                setSaveFailure(undefined);
                                setInitialValues({
                                    platform: formValues.platform,
                                    connectionUrl: formValues.connectionUrl,
                                    connectionKey: formValues.connectionKey,
                                });
                                azPortalContext.stopNotification(notificationId, true, IncidentManagementNotifications.saveSucceeded);
                            }
                        });
                    }
                });
            }
        });

    }, [subscription, resourceGroup, agent?.name, agent?.location, agent?.properties?.agentEndpoint, azPortalContext.startNotification, azPortalContext.stopNotification]);

    const platform: IncidentManagementPlatform = useMemo(() => {
        return agent?.properties?.incidentManagementConfiguration?.type === IncidentManagementType.PagerDuty
            ? IncidentManagementPlatform.PagerDuty
            : IncidentManagementPlatform.Disconnected;
    }, [agent?.properties?.incidentManagementConfiguration?.type]);

    const [initialValues, setInitialValues] = useState<IncidentManagementFormValues>({
        platform: platform,
        connectionUrl: agent?.properties?.incidentManagementConfiguration?.connectionUrl,
        connectionKey: "",
    });

    return {
        loading: agentLoading,
        loaded: agentLoaded,
        loadFailure: agentLoadFailure,
        saving,
        saveFailure,
        platform,
        initialValues,
        save
    };
}
