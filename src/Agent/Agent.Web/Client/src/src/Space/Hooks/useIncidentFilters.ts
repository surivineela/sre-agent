import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentFilter, IncidentFilterDocumentPayload } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementNotificationResources } from '../../Strings/SREAgentResources';

export const useIncidentFilters = (filterType: 'subagentTrigger' | 'filter' | 'all' = 'all') => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const [incidentFilters, setIncidentFilters] = useState<IncidentFilter[]>();
    const [isLoading, setIsLoading] = useState<boolean>(true);
    const [loadingError, setLoadingError] = useState<string | null>(null);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, portalContext.log.bind(portalContext)),
        [sreAgentEndpoint, portalContext]
    );

    const getIncidentFilters = useCallback(async (): Promise<IncidentFilter[]> => {
        setLoadingError(null);
        const incidentResults = await incidentHandlerClient.listIncidentFilters();
        if (incidentResults.isSuccessful) {
            const allFilters = incidentResults.content ?? [];
            if (filterType === 'all') {
                return allFilters;
            } else {
                return allFilters.filter(filter => (filterType === 'subagentTrigger' ? !!filter.handlingAgent : !filter.handlingAgent));
            }
        } else {
            setLoadingError(`Failed to load incident filters: ${incidentResults.error}`);
            return [];
        }
    }, [incidentHandlerClient, filterType]);

    const refresh = useCallback(async () => {
        setIsLoading(true);
        const results = await getIncidentFilters();
        setIncidentFilters(results);
        setIsLoading(false);
    }, [getIncidentFilters]);

    const deleteIncidentFilter = useCallback(
        async (id: string): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.deleteFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.deleteFilterInProgress)
            );

            portalContext.logAmplitudeOperationEvent({
                targetAction: 'start',
                targetType: 'delete',
                targetName: 'deleteIncidentHandler',
                targetFriendlyName: 'Delete incident handler',
                metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
            });

            const deleteFilterResponse = await incidentHandlerClient.deleteIncidentFilter(id);
            if (deleteFilterResponse.isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    intl.formatMessage(IncidentManagementNotificationResources.deleteFilterSuccess)
                );
                setIncidentFilters(prev => prev?.filter(filter => filter.id !== id));
                portalContext.logAmplitudeOperationEvent({
                    targetAction: 'success',
                    targetType: 'delete',
                    targetName: 'deleteIncidentHandler',
                    targetFriendlyName: 'Delete incident handler',
                    metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
                });
            } else {
                const errorMessage = deleteFilterResponse.error;
                portalContext.log({
                    action: 'deleteIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to delete incident filter`,
                        errorMessage,
                    },
                });
                portalContext.logAmplitudeOperationEvent({
                    targetAction: 'failed',
                    targetType: 'delete',
                    targetName: 'deleteIncidentHandler',
                    targetFriendlyName: 'Delete incident handler',
                    metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.deleteFilterError, {
                        errorMessage,
                    })
                );
            }
        },
        [incidentHandlerClient, intl, portalContext, incidentFilters]
    );

    const createIncidentFilter = useCallback(
        async (incidentFilter: IncidentFilterDocumentPayload): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.createFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.createFilterInProgress)
            );

            portalContext.logAmplitudeOperationEvent({
                targetAction: 'start',
                targetType: 'create',
                targetName: 'createIncidentHandler',
                targetFriendlyName: 'Create incident handler',
                metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
            });

            const createFilterResponse = await incidentHandlerClient.createIncidentFilter(incidentFilter);
            if (createFilterResponse.isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    intl.formatMessage(IncidentManagementNotificationResources.createFilterSuccess)
                );
                if (createFilterResponse.content) {
                    setIncidentFilters(prev => [...(prev ?? []), createFilterResponse.content as IncidentFilter]);
                }
                portalContext.logAmplitudeOperationEvent({
                    targetAction: 'success',
                    targetType: 'create',
                    targetName: 'createIncidentHandler',
                    targetFriendlyName: 'Create incident handler',
                    metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
                });
            } else {
                const errorMessage = createFilterResponse.error;
                portalContext.log({
                    action: 'createIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to create incident filter`,
                        errorMessage,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.createFilterError, {
                        errorMessage,
                    })
                );
                portalContext.logAmplitudeOperationEvent({
                    targetAction: 'failed',
                    targetType: 'create',
                    targetName: 'createIncidentHandler',
                    targetFriendlyName: 'Create incident handler',
                    metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
                });
            }
        },
        [incidentHandlerClient, intl, portalContext, incidentFilters]
    );

    const updateIncidentFilter = useCallback(
        async (incidentFilter: IncidentFilterDocumentPayload): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.updateFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.updateFilterInProgress)
            );

            portalContext.logAmplitudeOperationEvent({
                targetAction: 'start',
                targetType: 'update',
                targetName: 'updateIncidentHandler',
                targetFriendlyName: 'Update incident handler',
                metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
            });

            const updateFilterResponse = await incidentHandlerClient.updateIncidentFilter(incidentFilter);
            if (updateFilterResponse.isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    intl.formatMessage(IncidentManagementNotificationResources.updateFilterSuccess)
                );
                if (updateFilterResponse.content) {
                    setIncidentFilters(prev => {
                        const updated = prev?.map(filter =>
                            filter.id === incidentFilter.id ? (updateFilterResponse.content as IncidentFilter) : filter
                        );
                        return updated ?? [];
                    });
                }
                portalContext.logAmplitudeOperationEvent({
                    targetAction: 'success',
                    targetType: 'update',
                    targetName: 'updateIncidentHandler',
                    targetFriendlyName: 'Update incident handler',
                    metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
                });
            } else {
                const errorMessage = updateFilterResponse.error;
                portalContext.log({
                    action: 'updateIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to update incident filter`,
                        errorMessage,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.updateFilterError, {
                        errorMessage,
                    })
                );
                portalContext.logAmplitudeOperationEvent({
                    targetAction: 'failed',
                    targetType: 'update',
                    targetName: 'updateIncidentHandler',
                    targetFriendlyName: 'Update incident handler',
                    metadata: { incidentHandlersCount: incidentFilters?.length ?? 0 },
                });
            }
        },
        [incidentHandlerClient, intl, portalContext, incidentFilters]
    );

    const enableIncidentFilter = useCallback(
        async (id: string): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.enableFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.enableFilterInProgress)
            );
            const enableFilterResponse = await incidentHandlerClient.enableIncidentFilter(id);
            if (enableFilterResponse.isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    intl.formatMessage(IncidentManagementNotificationResources.enableFilterSuccess)
                );
                setIncidentFilters(prev => {
                    const updated = prev?.map(filter => (filter.id === id ? (enableFilterResponse.content as IncidentFilter) : filter));
                    return updated ?? [];
                });
            } else {
                const errorMessage = enableFilterResponse.error;
                portalContext.log({
                    action: 'enableIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to enable incident filter`,
                        errorMessage,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.enableFilterError, {
                        errorMessage,
                    })
                );
            }
        },
        [incidentHandlerClient, intl, portalContext]
    );

    const disableIncidentFilter = useCallback(
        async (id: string): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.disableFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.disableFilterInProgress)
            );
            const disableFilterResponse = await incidentHandlerClient.disableIncidentFilter(id);
            if (disableFilterResponse.isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    intl.formatMessage(IncidentManagementNotificationResources.disableFilterSuccess)
                );
                setIncidentFilters(prev => {
                    const updated = prev?.map(filter => (filter.id === id ? (disableFilterResponse.content as IncidentFilter) : filter));
                    return updated ?? [];
                });
            } else {
                const errorMessage = disableFilterResponse.error;
                portalContext.log({
                    action: 'disableIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to disable incident filter`,
                        errorMessage,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.disableFilterError, {
                        errorMessage,
                    })
                );
            }
        },
        [incidentHandlerClient, intl, portalContext]
    );

    useEffect(() => {
        let isSubscribed = true;

        const fetch = async () => {
            setIsLoading(true);
            const initialResults = await getIncidentFilters();
            if (!isSubscribed) return;
            setIncidentFilters(initialResults);
            setIsLoading(false);
        };
        fetch();

        return () => {
            isSubscribed = false;
        };
    }, [getIncidentFilters]);

    return {
        refresh,
        incidentFilters,
        incidentFiltersLoading: isLoading,
        incidentFiltersLoadingError: loadingError,
        deleteIncidentFilter,
        createIncidentFilter,
        updateIncidentFilter,
        enableIncidentFilter,
        disableIncidentFilter,
    };
};
