import {
    Caption1,
    Dropdown,
    Field,
    MessageBar,
    MessageBarBody,
    Option,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
    Text,
} from '@fluentui/react-components';
import axios from 'axios';
import { memo, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { useParams } from 'react-router-dom';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources, GraphResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ResourceExtended, Subscription } from '../Contracts/Graph';
import { useResourceSelectorStyles } from '../Styles/Graph.styles';

const allKey = 'all';

interface IResourceSelectorProps {
    onAppGroupUpdate: (appGroup?: ResourceExtended) => void;
}

const ResourceSelector = ({ onAppGroupUpdate }: IResourceSelectorProps) => {
    const { groupId: initialGroupId } = useParams();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { hasChatPermissions, isKnowledgeGraphBuildCompleted, progressPercent } = useContext(KnowledgeGraphBuildStatusContext);
    const intl = useIntl();

    const [subscriptions, setSubscriptions] = useState<Subscription[]>([]);
    const [appGroups, setAppGroups] = useState<ResourceExtended[]>([]);
    const [filteredAppGroups, setFilteredAppGroups] = useState<ResourceExtended[]>([]);

    const [selectedSubscription, setSelectedSubscription] = useState<Subscription>();
    const [selectedRscType, setSelectedRscType] = useState<string>(allKey);
    const [selectedAppGroup, setSelectedAppGroup] = useState<ResourceExtended>();

    const [isSubscriptionLoading, setIsSubscriptionLoading] = useState<boolean>(false);
    const [isAppGroupLoading, setIsAppGroupLoading] = useState<boolean>(false);

    const { root, field, option, optionText, optionSubtext } = useResourceSelectorStyles();

    const resourceTypeFilterOptions = useMemo(() => {
        const options = [{ key: allKey, text: intl.formatMessage(SreAgentResources.all) }];

        if (!isAppGroupLoading && appGroups.length > 0) {
            const uniqueTypes = new Set(appGroups.map(appGroup => appGroup.type));
            uniqueTypes.forEach(type => {
                options.push({ key: type, text: type });
            });
        }

        return options;
    }, [intl, appGroups, isAppGroupLoading]);

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

    const onSelectRscType = (_: SelectionEvents, data: OptionOnSelectData) => {
        const rscType = data.optionValue;
        setSelectedRscType(rscType ?? allKey);

        if (rscType === allKey) {
            setFilteredAppGroups(appGroups);
        } else {
            const filteredAppGroups = appGroups.filter(appGroup => appGroup.type === rscType);
            setFilteredAppGroups(filteredAppGroups);
            setSelectedAppGroup(filteredAppGroups[0]);
        }
    };

    const onSelectAppGroup = (_: SelectionEvents, data: OptionOnSelectData) => {
        const appGroupId = data.optionValue;

        const selectedAppGroup = appGroups.find(appGroup => appGroup.id === appGroupId);

        setSelectedAppGroup(selectedAppGroup);
    };

    useEffect(() => {
        if (!hasChatPermissions) {
            setSubscriptions([]);
            setAppGroups([]);
            setFilteredAppGroups([]);
            setIsSubscriptionLoading(false);
            setIsAppGroupLoading(false);
            return;
        }

        let isSubscribed = true;

        const init = async () => {
            setIsSubscriptionLoading(true);
            setIsAppGroupLoading(true);

            const subscriptions = await getSubscriptions();
            if (isSubscribed) {
                setSubscriptions(subscriptions);
                setIsSubscriptionLoading(false);
            }

            const initialGroupSubscriptionId = initialGroupId?.split('/')[1];
            const initialGroupSubscription = initialGroupSubscriptionId
                ? subscriptions.find(sub => sub.id === initialGroupSubscriptionId)
                : undefined;
            const selectedSubscription =
                initialGroupSubscriptionId && initialGroupSubscription ? initialGroupSubscription : subscriptions[0];

            if (isSubscribed) {
                setSelectedSubscription(selectedSubscription);
            }

            const appGroups = await getAppGroups(selectedSubscription.id);

            if (isSubscribed) {
                setAppGroups(appGroups);

                // NOTE: If deep linked and target resource doesn't have `resourceId`, won't select it - but
                // we shouldn't ever be deep linking to such resources. Could use appGroup.id, but there's parsing
                // considerations there ('_' replace '/', etc, but names can contain those...)
                const initialSelectedAppGroup = initialGroupId
                    ? appGroups.find(appGroup => appGroup.properties.resourceId?.[0] === initialGroupId)
                    : undefined;
                setSelectedAppGroup(initialSelectedAppGroup ?? appGroups[0]);
            }

            if (isSubscribed) {
                setIsAppGroupLoading(false);
            }
        };

        init();

        return () => {
            isSubscribed = false;
        };
    }, [hasChatPermissions]);

    useEffect(() => {
        setFilteredAppGroups([...appGroups]);
    }, [appGroups]);

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
                        disabled={!hasChatPermissions}
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

            <Field label={intl.formatMessage(SreAgentResources.resourceType)} className={field}>
                <Dropdown
                    value={selectedRscType === allKey ? intl.formatMessage(SreAgentResources.all) : selectedRscType}
                    selectedOptions={selectedRscType ? [selectedRscType] : []}
                    onOptionSelect={onSelectRscType}
                    disabled={!hasChatPermissions}
                >
                    {resourceTypeFilterOptions.map(rscTypeOption => {
                        return (
                            <Option key={rscTypeOption.key} value={rscTypeOption.key} text={rscTypeOption.text}>
                                <Text className={optionText}>{rscTypeOption.text}</Text>
                            </Option>
                        );
                    })}
                </Dropdown>
            </Field>

            <Field label={<FormattedMessage {...SreAgentResources.coreApplicationGroup} />} className={field}>
                {isAppGroupLoading ? (
                    <Shimmer />
                ) : (
                    <Dropdown
                        value={selectedAppGroup?.name}
                        selectedOptions={selectedAppGroup ? [selectedAppGroup.id] : []}
                        onOptionSelect={onSelectAppGroup}
                        disabled={!hasChatPermissions}
                    >
                        {filteredAppGroups.map(appGroup => {
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
            {!isKnowledgeGraphBuildCompleted && progressPercent !== 100 && (
                <MessageBar intent={'info'} shape={'rounded'} layout={'multiline'}>
                    <MessageBarBody>
                        {intl.formatMessage(ActivitiesResources.knowledgeGraphBuildStatus, { percent: progressPercent })}
                    </MessageBarBody>
                </MessageBar>
            )}
        </div>
    );
};

export default memo(ResourceSelector);
