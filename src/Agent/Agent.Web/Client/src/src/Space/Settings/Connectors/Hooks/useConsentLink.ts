import { useCallback, useContext, useState } from 'react';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../../Common/Clients/ArmClient';
import { OAuthServiceClient } from '../../../../Common/Clients/OAuthService';
import { ConsentLink } from '../../../../Common/Contracts/Azure/ConsentLinks';
import { ArmResourceDescriptor } from '../../../../Common/Helpers/ResourceDescriptors';

export const useConsentLink = (connectionName: string) => {
    const { log } = useContext(AzPortalContext);
    const { resourceId, userInfo } = useContext(EnvironmentContext);
    const { objectId, directoryId: tenantId } = userInfo || {};

    const { subscription, resourceGroup } = new ArmResourceDescriptor(resourceId);

    const [consentLink, setConsentLink] = useState<ConsentLink>();
    const [consentLinkLoading, setConsentLinkLoading] = useState(false);
    const [consentLinkLoaded, setConsentLinkLoaded] = useState(false);
    const [consentLinkLoadFailure, setConsentLinkLoadFailure] = useState('');

    const fetchConsentLink = useCallback(async () => {
        log({
            action: 'fetch-consent-link',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId,
        });

        if (objectId && tenantId) {
            const response = await OAuthServiceClient.fetchConsentUrlForConnection({
                subscriptionId: subscription,
                resourceGroup,
                connectionName,
                tenantId,
                objectId,
            });

            setConsentLinkLoading(false);

            if (response.metadata.success) {
                setConsentLink(response.data.value[0]);
                setConsentLinkLoaded(true);
                return response.data.value[0];
            } else {
                const error = getErrorMessage(response?.metadata?.error);
                log({
                    action: 'fetch-consent-link',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: error,
                });
                setConsentLinkLoadFailure(error || 'Failed to load consent link');
            }
        } else {
            log({
                action: 'fetch-consent-link',
                actionModifier: 'failed',
                logLevel: 'error',
                resourceId,
                data: {
                    error: 'Missing objectId or tenantId',
                    objectId,
                    tenantId,
                },
            });
        }
    }, [log, resourceId, objectId, tenantId, subscription, resourceGroup, connectionName]);

    const refreshConsentLink = useCallback(async () => {
        setConsentLink(undefined);
        setConsentLinkLoading(true);
        setConsentLinkLoaded(false);
        setConsentLinkLoadFailure('');

        fetchConsentLink();
    }, [fetchConsentLink]);

    return {
        consentLink,
        consentLinkLoading,
        consentLinkLoaded,
        consentLinkLoadFailure,
        fetchConsentLink,
        refreshConsentLink,
    };
};
