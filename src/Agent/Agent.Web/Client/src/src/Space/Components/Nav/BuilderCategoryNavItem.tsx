import {
    Agents20Filled,
    Agents20Regular,
    bundleIcon,
    ClipboardTaskListRtl20Filled,
    ClipboardTaskListRtl20Regular,
    Timer20Filled,
    Timer20Regular,
    Toolbox20Filled,
    Toolbox20Regular,
} from '@fluentui/react-icons';
import { FC, memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { useFeatureFlags } from '../../../Common/Hooks/useFeatureFlags';
import { ExtendedAgentsGraphResources, IncidentManagementResources, SreAgentTabResources } from '../../../Strings/SREAgentResources';
import { CategoryNavItemInput, PrimaryNavItemValues, SecondaryNavItemValues, SubNavItemInput } from '../../Contracts/SreAgentSpace';
import CategoryNavItem from './CategoryNavItem';

interface IBuilderCategoryNavItemProps {
    isNavOpen: boolean;
    incidentVisible: boolean;
    incidentDisabled: boolean;
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const BuilderCategoryNavItem: FC<IBuilderCategoryNavItemProps> = ({
    isNavOpen,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
}) => {
    const intl = useIntl();

    const { features } = useFeatureFlags();
    const showScheduledTasksTab = features.scheduledTasks;
    const showExtendedAgentsGraphTab = features.extendedAgentsGraph;

    const categoryItem = useMemo(
        (): CategoryNavItemInput => ({
            value: PrimaryNavItemValues.Builder,
            label: intl.formatMessage(SreAgentTabResources.builder),
            icon: bundleIcon(Toolbox20Filled, Toolbox20Regular),
            filledIcon: Toolbox20Filled,
            isCollapsed: !isNavOpen,
            isVisible: incidentVisible || showScheduledTasksTab || showExtendedAgentsGraphTab,
            disabled: false,
        }),
        [isNavOpen, incidentVisible, showScheduledTasksTab, showExtendedAgentsGraphTab, intl]
    );

    const subItems = useMemo((): SubNavItemInput[] => {
        return [
            {
                value: SecondaryNavItemValues.ResponsePlans,
                isVisible: incidentVisible,
                label: intl.formatMessage(IncidentManagementResources.responsePlans),
                disabled: incidentDisabled,
                icon: bundleIcon(ClipboardTaskListRtl20Filled, ClipboardTaskListRtl20Regular),
            },
            {
                value: SecondaryNavItemValues.ScheduledTasks,
                isVisible: showScheduledTasksTab,
                disabled: false,
                icon: bundleIcon(Timer20Filled, Timer20Regular),
                label: intl.formatMessage(SreAgentTabResources.scheduledTasks),
            },
            {
                value: SecondaryNavItemValues.ExtendedAgentsGraph,
                isVisible: showExtendedAgentsGraphTab,
                disabled: false,
                icon: bundleIcon(Agents20Filled, Agents20Regular),
                label: intl.formatMessage(ExtendedAgentsGraphResources.extendedAgentsTab),
            },
        ];
    }, [incidentVisible, incidentDisabled, showScheduledTasksTab, showExtendedAgentsGraphTab, intl]);

    return (
        <CategoryNavItem
            categoryItem={categoryItem}
            subItems={subItems}
            onClickCategoryNavItem={onClickCategoryNavItem}
            onClickSubNavItem={onClickSubNavItem}
        />
    );
};

export default memo(BuilderCategoryNavItem);
