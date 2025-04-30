import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import SubscriptionClient from '../../../Common/Clients/SubscriptionClient';
import { Subscription } from '../../../Common/Contracts/Azure/Subscription';

export function useSubscription(subscriptionGuid: string) {
    const [subscription, setSubscription] = useState<Subscription>();
    const [subscriptionLoading, setSubscriptionLoading] = useState(false);
    const [subscriptionLoaded, setSubscriptionLoaded] = useState(false);
    const [subscriptionLoadFailure, setSubscriptionLoadFailure] = useState('');
    const resourceId = useMemo(() => (subscriptionGuid ? `/subscriptions/${subscriptionGuid}` : undefined), [subscriptionGuid]);
    const azPortalContext = useContext(AzPortalContext);

    const getSubscription = useCallback(() => {
        azPortalContext.log({
            action: 'fetch-subscription',
            actionModifier: 'start',
            logLevel: 'info',
            resourceId,
        });
        setSubscription(undefined);
        setSubscriptionLoading(true);
        setSubscriptionLoaded(false);
        setSubscriptionLoadFailure('');

        if (resourceId) {
            SubscriptionClient.getSubscription(resourceId).then(response => {
                setSubscriptionLoading(false);
                if (response?.metadata?.success && response.data) {
                    azPortalContext.log({
                        action: 'fetch-subscription',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId,
                    });
                    setSubscription(response.data);
                    setSubscriptionLoaded(true);
                } else {
                    azPortalContext.log({
                        action: 'fetch-subscription',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId,
                        data: { error: response.metadata.error },
                    });
                    setSubscriptionLoadFailure(response?.metadata?.error || 'Failed to load subscription');
                }
            });
        }
    }, [resourceId]);

    useEffect(() => {
        getSubscription();
    }, [getSubscription]);

    return {
        subscription,
        subscriptionLoading,
        subscriptionLoaded,
        subscriptionLoadFailure,
        refresh: getSubscription,
    };
}
