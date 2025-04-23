import { memo, useEffect, useState } from "react";
import { ResourceExtended, Subscription } from "../Contracts/Graph";
import axios from "axios";
import { Dropdown, Field, OptionOnSelectData, SelectionEvents, Skeleton, SkeletonItem, Option, Text, Caption1 } from "@fluentui/react-components";
import { useResourceSelectorStyles } from "../Styles/Graph.styles";
import { getAgentHeaders } from "../../Common/Helpers/headers";

interface IResourceSelectorProps {
    onAppGroupUpdate: (appGroup?: ResourceExtended) => void
}

const getSubscriptions = async (): Promise<Subscription[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/subscriptions`, {
            headers: getAgentHeaders()
        });
        return data ?? [];
    } catch {
        return [];
    }
}

const getAppGroups = async (subscriptionId: string): Promise<ResourceExtended[]> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/${subscriptionId}/appGroups`, {
            headers: getAgentHeaders()
        });
        return data ?? [];
    } catch {
        return [];
    }
}

const ResourceSelector = ({ onAppGroupUpdate }: IResourceSelectorProps) => {
    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [appGroups, setAppGroups] = useState<ResourceExtended[]>([]);

    const [selectedSubscription, setSelectedSubscription] = useState<Subscription>();
    const [selectedAppGroup, setSelectedAppGroup] = useState<ResourceExtended>();

    const [isSubscriptionLoading, setIsSubscriptionLoading] = useState<boolean>(false);
    const [isAppGroupLoading, setIsAppGroupLoading] = useState<boolean>(false);

    const { root, option, optionText, optionSubtext } = useResourceSelectorStyles();

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
    }

    const onSelectAppGroup = (_: SelectionEvents, data: OptionOnSelectData) => {
        const appGroupId = data.optionValue;

        const selectedAppGroup = appGroups.find(appGroup => appGroup.id === appGroupId);

        setSelectedAppGroup(selectedAppGroup);
    }

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
        }

        init();

        return () => {
            isSubscribed = false;
        }
    }, []);


    useEffect(() => {
        onAppGroupUpdate(selectedAppGroup);
    }, [selectedAppGroup])

    const Shimmer = () => <Skeleton><SkeletonItem /></Skeleton>

    return (
        <div className={root}>
            <Field label="Subscription" >
                {isSubscriptionLoading ? <Shimmer />
                    : <Dropdown
                        value={selectedSubscription?.name}
                        selectedOptions={selectedSubscription ? [selectedSubscription.id] : []}
                        onOptionSelect={onSelectSubscription}
                    >
                        {subscriptions.map(subscription => {
                            return <Option key={subscription.id} value={subscription.id}>
                                {subscription.name}
                            </Option>
                        })}
                    </Dropdown>}
            </Field>
            <Field label="App Group">
                {isAppGroupLoading ? <Shimmer />
                    : <Dropdown
                        value={selectedAppGroup?.name}
                        selectedOptions={selectedAppGroup ? [selectedAppGroup.id] : []}
                        onOptionSelect={onSelectAppGroup}
                    >
                        {appGroups.map(appGroup => {
                            return <Option key={appGroup.id} value={appGroup.id} text={appGroup.name}>
                                <div className={option}>
                                    <Text className={optionText}>{appGroup.name}</Text>
                                    <Caption1 className={optionSubtext}>{appGroup?.type ?? 'subscription'}</Caption1>
                                </div>
                            </Option>
                        })}
                    </Dropdown>}
            </Field>
        </div>
    )
}

export default memo(ResourceSelector);