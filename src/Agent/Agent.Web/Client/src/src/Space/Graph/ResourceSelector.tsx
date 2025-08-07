import {
    Caption1,
    Dropdown,
    MessageBar,
    MessageBarBody,
    Option,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
    Text,
} from '@fluentui/react-components';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { ResourceExtended, Subscription } from '../Contracts/Graph';
import { useIntegratedSelectorStyles } from '../Styles/Graph.styles';

interface IResourceSelectorProps {
    subscriptions: Subscription[];
    filteredAppGroups: ResourceExtended[];
    selectedSubscription?: Subscription;
    selectedRscType: string;
    selectedAppGroup?: ResourceExtended;
    isSubscriptionLoading: boolean;
    isAppGroupLoading: boolean;
    resourceTypeFilterOptions: Array<{ key: string; text: string }>;
    onSelectSubscription: (event: SelectionEvents, data: OptionOnSelectData) => void;
    onSelectRscType: (event: SelectionEvents, data: OptionOnSelectData) => void;
    onSelectAppGroupDropdown: (event: SelectionEvents, data: OptionOnSelectData) => void;
    allKey: string;
}

const ResourceSelector = ({
    subscriptions,
    filteredAppGroups,
    selectedSubscription,
    selectedRscType,
    selectedAppGroup,
    isSubscriptionLoading,
    isAppGroupLoading,
    resourceTypeFilterOptions,
    onSelectSubscription,
    onSelectRscType,
    onSelectAppGroupDropdown,
    allKey,
}: IResourceSelectorProps) => {
    const { hasChatPermissions, isKnowledgeGraphBuildCompleted, progressPercent } = useContext(KnowledgeGraphBuildStatusContext);
    const intl = useIntl();

    const { selectorPanel, option, optionText, optionSubtext } = useIntegratedSelectorStyles();

    const Shimmer = () => (
        <Skeleton style={{ width: '300px' }}>
            <SkeletonItem />
        </Skeleton>
    );

    return (
        <div className={selectorPanel}>
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

            {isAppGroupLoading ? (
                <Shimmer />
            ) : (
                <Dropdown
                    value={selectedAppGroup?.name}
                    selectedOptions={selectedAppGroup ? [selectedAppGroup.id] : []}
                    onOptionSelect={onSelectAppGroupDropdown}
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

            {!isKnowledgeGraphBuildCompleted && progressPercent !== 100 && (
                <MessageBar layout={'multiline'}>
                    <MessageBarBody>
                        {intl.formatMessage(ActivitiesResources.knowledgeGraphBuildStatus, { percent: progressPercent })}
                    </MessageBarBody>
                </MessageBar>
            )}
        </div>
    );
};

export default memo(ResourceSelector);
