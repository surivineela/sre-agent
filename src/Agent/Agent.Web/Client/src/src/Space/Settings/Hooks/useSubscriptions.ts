import * as React from 'react';
import { useEffect } from 'react';
import { useIntl } from 'react-intl';
import { SubscriptionsClient } from '../../../Common/Clients/SubscriptionsClient';
import { ManagedResourcesStringResources } from '../../../Strings/SREAgentResources';
import { sortDropdownOptionsFunc } from './useResourceGroups';

export type SubscriptionPolicies = {
    readonly locationPlacementId: string;
    readonly quotaId: string;
    readonly spendingLimit?: string;
};

export type SubscriptionPromotion = {
    readonly endDateTime: string;
    readonly category: string;
};

export type Subscription = {
    readonly uniqueDisplayName: string;
    readonly displayName: string;
    readonly subscriptionId: string;
    readonly tenantId: string;
    readonly state: string;
    readonly subscriptionPolicies: SubscriptionPolicies;
    readonly authorizationSource: string;
    readonly promotions?: ReadonlyArray<SubscriptionPromotion>;
};

const getSubscriptions = () =>
    SubscriptionsClient.getSubscriptions()
        .then(subscriptions => ({
            isSuccessful: true,
            content: subscriptions,
            error: undefined,
        }))
        .catch(reason => ({
            isSuccessful: false,
            content: undefined,
            error: reason,
        }));

export const useSubscriptions = () => {
    const [subscriptionsList, setSubscriptionsList] = React.useState<Subscription[]>();
    const [subscriptionsLoading, setSubscriptionsLoading] = React.useState<boolean>(true);
    const [subscriptionsLoadFailure, setSubscriptionsLoadFailure] = React.useState<string>('');
    const intl = useIntl();

    const subscriptionOptions = React.useMemo(() => {
        return (subscriptionsList || [])
            .map(sub => ({
                key: sub.subscriptionId,
                text: sub.displayName,
                data: sub,
            }))
            .sort(sortDropdownOptionsFunc);
    }, [subscriptionsList]);

    useEffect(() => {
        getSubscriptions().then(result => {
            const subList = result?.isSuccessful && result?.content?.data?.value ? [...result.content.data.value] : [];
            setSubscriptionsList(subList);
            setSubscriptionsLoading(false);
            setSubscriptionsLoadFailure(
                result.isSuccessful ? '' : intl.formatMessage(ManagedResourcesStringResources.subscriptionsLoadFailure)
            );
        });
    }, [intl]);

    return {
        subscriptionsList,
        subscriptionsLoading,
        subscriptionsLoadFailure,
        subscriptionOptions,
    };
};
