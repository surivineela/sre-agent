import { useCallback, useContext, useState } from 'react';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../../../Common/Clients/ArmClient';
import {
    Connector,
    ConnectorService,
    GetConnectorOptions,
    PutConnectorAccessPoliciesOptions,
    PutConnectorOptions,
} from '../../../../Common/Clients/ConnectorService';

export const useApiConnection = () => {
    const { log } = useContext(AzPortalContext);

    const [apiConnection, setApiConnection] = useState<Connector>();
    const [apiConnectionLoading, setApiConnectionLoading] = useState(false);
    const [apiConnectionLoaded, setApiConnectionLoaded] = useState(false);
    const [apiConnectionLoadFailure, setApiConnectionLoadFailure] = useState('');

    const [apiConnectionCreating, setApiConnectionCreating] = useState(false);
    const [apiConnectionCreated, setApiConnectionCreated] = useState(false);
    const [apiConnectionCreateFailure, setApiConnectionCreateFailure] = useState('');

    const fetchApiConnection = useCallback(
        async (options: GetConnectorOptions) => {
            log({
                action: 'fetch-api-connection',
                actionModifier: 'start',
                logLevel: 'info',
            });

            const response = await ConnectorService.getConnector(options);

            setApiConnectionLoading(false);

            if (response.metadata.success) {
                setApiConnection(response.data);
                setApiConnectionLoaded(true);
                return response.data;
            } else {
                const error = getErrorMessage((response?.data as any).error.message);
                log({
                    action: 'fetch-api-connection',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: error,
                });
                setApiConnectionLoadFailure(error || 'Failed to load API connection');
            }
        },
        [log]
    );

    const refresh = useCallback(
        (options: GetConnectorOptions) => {
            setApiConnection(undefined);
            setApiConnectionLoading(true);
            setApiConnectionLoaded(false);
            setApiConnectionLoadFailure('');

            fetchApiConnection(options);
        },
        [fetchApiConnection]
    );

    const assignAccessPolicies = useCallback(
        async (options: PutConnectorAccessPoliciesOptions) => {
            log({
                action: 'create-api-connection-access-policies',
                actionModifier: 'start',
                logLevel: 'info',
            });

            const response = await ConnectorService.putConnectorAccessPolicies(options);

            if (response?.metadata?.success) {
                log({
                    action: 'create-api-connection-access-policies',
                    actionModifier: 'success',
                    logLevel: 'info',
                });
            } else {
                const error = getErrorMessage(response?.metadata?.error);
                log({
                    action: 'create-api-connection-access-policies',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: { error },
                });
            }
        },
        [log]
    );

    const createApiConnection = useCallback(
        async (options: PutConnectorOptions & PutConnectorAccessPoliciesOptions) => {
            log({
                action: 'create-api-connection',
                actionModifier: 'start',
                logLevel: 'info',
            });
            setApiConnectionCreating(true);
            setApiConnectionCreated(false);
            setApiConnectionCreateFailure('');

            const response = await ConnectorService.putConnector(options);
            if (response?.metadata?.success && response.data) {
                log({
                    action: 'create-api-connection',
                    actionModifier: 'success',
                    logLevel: 'info',
                });
                setApiConnection(response.data);
                setApiConnectionCreated(true);
                await assignAccessPolicies(options);

                return response.data;
            } else {
                const error = getErrorMessage(response?.metadata?.error);
                log({
                    action: 'create-api-connection',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: { error },
                });
                setApiConnectionCreateFailure(error || 'Failed to create API connection');
            }

            setApiConnectionCreating(false);
        },
        [assignAccessPolicies, log]
    );

    return {
        apiConnection,
        apiConnectionLoading,
        apiConnectionLoaded,
        apiConnectionLoadFailure,
        apiConnectionCreating,
        apiConnectionCreated,
        apiConnectionCreateFailure,
        fetchApiConnection,
        createApiConnection,
        refresh,
    };
};
