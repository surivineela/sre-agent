import {
    InfoLabel,
    MessageBar,
    MessageBarBody,
    OptionOnSelectData,
    SelectionEvents,
    Skeleton,
    SkeletonItem,
} from '@fluentui/react-components';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ComboboxPillFilter } from '../../Common/Components/PillFilter/ComboboxPillFilter';
import { ResourceTypeToDisplayNameMap } from '../../Common/Contracts/Azure/Permission';
import { resolveResourceIcon } from '../../Common/Helpers/Resources';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources, GraphResources, SreAgentResources } from '../../Strings/SREAgentResources';
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

    const { selectorPanel } = useIntegratedSelectorStyles();

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
                <ComboboxPillFilter
                    label={intl.formatMessage(SreAgentResources.subscriptionEquals)}
                    options={subscriptions.map(s => ({ key: s.id, label: s.name, iconSrc: resolveResourceIcon('Subscription') }))}
                    selectedKeys={selectedSubscription ? [selectedSubscription.id] : []}
                    onApply={keys => {
                        const key = keys[0];
                        if (key !== selectedSubscription?.id) {
                            onSelectSubscription(undefined as unknown as SelectionEvents, { optionValue: key } as OptionOnSelectData);
                        }
                    }}
                    disabled={!hasChatPermissions}
                    labelDelimiter=""
                />
            )}

            <ComboboxPillFilter
                label={intl.formatMessage(SreAgentResources.primaryResourceType)}
                options={(() => {
                    const rest = resourceTypeFilterOptions.map(o => ({
                        key: o.key,
                        label: ResourceTypeToDisplayNameMap[o.key.toLowerCase()] || o.text,
                        iconSrc: resolveResourceIcon(o.key),
                    }));
                    return [...rest];
                })()}
                selectedKeys={selectedRscType ? [selectedRscType] : []}
                onApply={keys => {
                    const key = keys[0] || allKey;
                    if (key !== selectedRscType) {
                        onSelectRscType(undefined as unknown as SelectionEvents, { optionValue: key } as OptionOnSelectData);
                    }
                }}
                disabled={!hasChatPermissions}
                labelDelimiter=""
            />

            {isAppGroupLoading ? (
                <Shimmer />
            ) : (
                <ComboboxPillFilter
                    label={intl.formatMessage(SreAgentResources.primaryResourceName)}
                    options={filteredAppGroups.map(ag => ({ key: ag.id, label: ag.name, iconSrc: resolveResourceIcon(ag.type) }))}
                    selectedKeys={selectedAppGroup ? [selectedAppGroup.id] : []}
                    onApply={keys => {
                        const key = keys[0];
                        if (key !== selectedAppGroup?.id) {
                            onSelectAppGroupDropdown(undefined as unknown as SelectionEvents, { optionValue: key } as OptionOnSelectData);
                        }
                    }}
                    disabled={!hasChatPermissions}
                    labelDelimiter=""
                />
            )}

            <InfoLabel
                info={intl.formatMessage(GraphResources.resourceSelectorDescription)}
                style={{ alignSelf: 'center', paddingTop: 0 }}
            />

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
