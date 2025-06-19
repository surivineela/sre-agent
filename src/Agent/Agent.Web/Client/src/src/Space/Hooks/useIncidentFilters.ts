import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentFilter, IncidentFilterPayload } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementNotificationResources } from '../../Strings/SREAgentResources';

export const useIncidentFilters = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const [incidentFilters, setIncidentFilters] = useState<IncidentFilter[]>();
    const [isLoading, setIsLoading] = useState<boolean>(true);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, portalContext.log.bind(portalContext)),
        [sreAgentEndpoint, portalContext]
    );

    const getIncidentFilters = useCallback(async (): Promise<IncidentFilter[]> => {
        const incidentResults = await incidentHandlerClient.listIncidentFilters();
        return incidentResults?.content ?? [];
    }, [incidentHandlerClient]);

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
            const deleteFilterResponse = await incidentHandlerClient.deleteIncidentFilter(id);
            if (deleteFilterResponse.isSuccessful) {
                portalContext.stopNotification(
                    notification,
                    true,
                    intl.formatMessage(IncidentManagementNotificationResources.deleteFilterSuccess)
                );
                setIncidentFilters(prev => prev?.filter(filter => filter.id !== id));
            } else {
                portalContext.log({
                    action: 'deleteIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to delete incident filter`,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.deleteFilterError)
                );
            }
        },
        [incidentHandlerClient, intl, portalContext]
    );

    const createIncidentFilter = useCallback(
        async (incidentFilter: IncidentFilterPayload): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.createFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.createFilterInProgress)
            );
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
            } else {
                portalContext.log({
                    action: 'createIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to create incident filter`,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.createFilterError)
                );
            }
        },
        [incidentHandlerClient, intl, portalContext]
    );

    const updateIncidentFilter = useCallback(
        async (incidentFilter: IncidentFilterPayload): Promise<void> => {
            const notification = portalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.updateFilterTitle),
                intl.formatMessage(IncidentManagementNotificationResources.updateFilterInProgress)
            );
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
                            filter.id === incidentFilter.Id ? (updateFilterResponse.content as IncidentFilter) : filter
                        );
                        return updated ?? [];
                    });
                }
            } else {
                portalContext.log({
                    action: 'updateIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to update incident filter`,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.updateFilterError)
                );
            }
        },
        [incidentHandlerClient, intl, portalContext]
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
                portalContext.log({
                    action: 'enableIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to enable incident filter`,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.enableFilterError)
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
                portalContext.log({
                    action: 'disableIncidentFilter',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: {
                        message: `Failed to disable incident filter`,
                    },
                });
                portalContext.stopNotification(
                    notification,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.disableFilterError)
                );
            }
        },
        [incidentHandlerClient, intl, portalContext]
    );

    useEffect(() => {
        let isSubscribed = true;

        const fetch = async () => {
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
        deleteIncidentFilter,
        createIncidentFilter,
        updateIncidentFilter,
        enableIncidentFilter,
        disableIncidentFilter,
    };
};
