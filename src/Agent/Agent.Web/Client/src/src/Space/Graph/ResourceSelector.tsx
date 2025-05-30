import {
    Caption1,
    Dropdown,
    Field,
    Option,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
    Text,
} from '@fluentui/react-components';
import axios from 'axios';
import { memo, useContext, useEffect, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { GraphResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ResourceExtended, Subscription } from '../Contracts/Graph';
import { useResourceSelectorStyles } from '../Styles/Graph.styles';

interface IResourceSelectorProps {
    onAppGroupUpdate: (appGroup?: ResourceExtended) => void;
}

const ResourceSelector = ({ onAppGroupUpdate }: IResourceSelectorProps) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [appGroups, setAppGroups] = useState<ResourceExtended[]>([]);

    const [selectedSubscription, setSelectedSubscription] = useState<Subscription>();
    const [selectedAppGroup, setSelectedAppGroup] = useState<ResourceExtended>();

    const [isSubscriptionLoading, setIsSubscriptionLoading] = useState<boolean>(false);
    const [isAppGroupLoading, setIsAppGroupLoading] = useState<boolean>(false);

    const { root, field, option, optionText, optionSubtext } = useResourceSelectorStyles();

    const getSubscriptions = async (): Promise<Subscription[]> => {
        try {
            const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/subscriptions`, {
                headers: getAgentHeaders(),
            });
            return data ?? [];
        } catch {
            return [];
        }
    };

    const getAppGroups = async (subscriptionId: string): Promise<ResourceExtended[]> => {
        try {
            const { data } = await axios.get(`${sreAgentEndpoint}/api/v1/graph/${subscriptionId}/appGroups`, {
                headers: getAgentHeaders(),
            });
            return data ?? [];
        } catch {
            return [];
        }
    };

    const onSelectSubscription = async (_: SelectionEvents, data: OptionOnSelectData) => {
        const id = data.optionValue;
        const selectedSubscription = subscriptions.find(subscription => subscription.id === id);
        if (selectedSubscription) {
            setSelectedSubscription(selectedSubscription);
            setIsAppGroupLoading(true);

            const appGroups = await getAppGroups(selectedSubscription.id);
            setAppGroups(appGroups);
            setSelectedAppGroup(appGroups[0]);
            setIsAppGroupLoading(false);
        }
    };

    const onSelectAppGroup = (_: SelectionEvents, data: OptionOnSelectData) => {
        const appGroupId = data.optionValue;

        const selectedAppGroup = appGroups.find(appGroup => appGroup.id === appGroupId);

        setSelectedAppGroup(selectedAppGroup);
    };

    useEffect(() => {
        let isSubscribed = true;

        const init = async () => {
            setIsSubscriptionLoading(true);
            setIsAppGroupLoading(true);

            const subscriptions = await getSubscriptions();
            if (isSubscribed) {
                setSubscriptions(subscriptions);
                setIsSubscriptionLoading(false);
            }

            const selectedSubscription = subscriptions[0];

            if (isSubscribed) {
                setSelectedSubscription(selectedSubscription);
            }

            if (selectedSubscription?.id) {
                const appGroups = await getAppGroups(subscriptions[0].id);

                if (isSubscribed) {
                    setAppGroups(appGroups);
                    setSelectedAppGroup(appGroups[0]);
                }
            }

            if (isSubscribed) {
                setIsAppGroupLoading(false);
            }
        };

        init();

        return () => {
            isSubscribed = false;
        };
    }, []);

    useEffect(() => {
        onAppGroupUpdate(selectedAppGroup);
    }, [selectedAppGroup]);

    const Shimmer = () => (
        <Skeleton>
            <SkeletonItem />
        </Skeleton>
    );

    return (
        <div className={root}>
            <Text>
                <FormattedMessage {...GraphResources.resourceSelectorDescription} />
            </Text>
            <Field label={<FormattedMessage {...SreAgentResources.subscription} />} className={field}>
                {isSubscriptionLoading ? (
                    <Shimmer />
                ) : (
                    <Dropdown
                        value={selectedSubscription?.name}
                        selectedOptions={selectedSubscription ? [selectedSubscription.id] : []}
                        onOptionSelect={onSelectSubscription}
                    >
                        {subscriptions.map(subscription => {
                            return (
                                <Option key={subscription.id} value={subscription.id}>
                                    {subscription.name}
                                </Option>
                            );
                        })}
                    </Dropdown>
                )}
            </Field>
            <Field label={<FormattedMessage {...SreAgentResources.appGroup} />} className={field}>
                {isAppGroupLoading ? (
                    <Shimmer />
                ) : (
                    <Dropdown
                        value={selectedAppGroup?.name}
                        selectedOptions={selectedAppGroup ? [selectedAppGroup.id] : []}
                        onOptionSelect={onSelectAppGroup}
                    >
                        {appGroups.map(appGroup => {
                            return (
                                <Option key={appGroup.id} value={appGroup.id} text={appGroup.name}>
                                    <div className={option}>
                                        <Text className={optionText}>{appGroup.name}</Text>
                                        <Caption1 className={optionSubtext}>{appGroup?.type ?? 'subscription'}</Caption1>
                                    </div>
                                </Option>
                            );
                        })}
                    </Dropdown>
                )}
            </Field>
        </div>
    );
};

export default memo(ResourceSelector);
