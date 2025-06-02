import axios from 'axios';
import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { Guid } from '../../Common/Helpers/Guid';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ResourceInfoResources } from '../../Strings/SREAgentResources';
import { GraphNode, ResourceExtended } from '../Contracts/Graph';

export const getPropertyValue = (input?: string[]): string => {
    return input?.[0] ?? '';
};

export const useResourceInfo = (selectedNode?: GraphNode) => {
    const [resource, setResource] = useState<ResourceExtended>();
    const [initialRemarks, setInitialRemarks] = useState<string>('');
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [isUpdating, setIsUpdating] = useState<boolean>(false);

    const toasterId = useMemo(() => Guid.newGuid(), []);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);
    const intl = useIntl();

    const getResource = async (resourceId: string): Promise<ResourceExtended | undefined> => {
        try {
            const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/resource/${resourceId}`, {
                headers: getAgentHeaders(),
            });
            return (data ?? [])?.[0];
        } catch {
            return undefined;
        }
    };

    const patchResource = async (resourceId: string, remarks: string): Promise<void> => {
        await axios.patch(
            `${sreAgentEndpoint}/api/v1/graph/resource/${resourceId}/remarks`,
            { remarks },
            {
                headers: getAgentHeaders(),
            }
        );
    };

    const onSubmit = useCallback(
        async (remarks: string) => {
            if (selectedNode) {
                setIsUpdating(true);

                const notificationId = azPortalContext.startNotification(
                    intl.formatMessage(ResourceInfoResources.annotationUpdateTitle),
                    intl.formatMessage(ResourceInfoResources.annotationUpdateInProgressDescription, { name: selectedNode.name })
                );

                try {
                    await patchResource(selectedNode.id, remarks);
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ResourceInfoResources.annotationUpdateSuccessDescription)
                    );
                } catch (e: any) {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ResourceInfoResources.annotationUpdateFailureDescription, {
                            errorMessage: e?.message || e?.response?.data,
                        })
                    );
                } finally {
                    setIsUpdating(false);
                    setInitialRemarks(remarks);
                }
            }
        },
        [selectedNode]
    );

    useEffect(() => {
        let isSubscribed = true;
        if (selectedNode) {
            setIsLoading(true);
            getResource(selectedNode.id)
                .then(resource => {
                    if (isSubscribed) {
                        setResource(resource);
                        setInitialRemarks(getPropertyValue(resource?.properties?.remarks));
                    }
                })
                .finally(() => {
                    if (isSubscribed) {
                        setIsLoading(false);
                    }
                });
        }

        return () => {
            isSubscribed = false;
        };
    }, [selectedNode]);

    return {
        resource,
        initialRemarks,
        isLoading,
        isUpdating,
        onSubmit,
        toasterId,
    };
};
