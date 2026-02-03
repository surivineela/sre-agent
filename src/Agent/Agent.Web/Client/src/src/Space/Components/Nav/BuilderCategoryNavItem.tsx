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
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/ConfigSettings';
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
    controlPlaneTabsVisible: boolean;
}

const BuilderCategoryNavItem: FC<IBuilderCategoryNavItemProps> = ({
    isNavOpen,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
    controlPlaneTabsVisible,
}) => {
    const intl = useIntl();

    const { features } = useFeatureFlags();
    const showScheduledTasksTab = features.scheduledTasks;
    const showExtendedAgentsGraphTab = features.extendedAgentsGraph;
    const showConnectors = useConfigSetting(SettingNames.Connectors);
    const showKnowledgeSettings = useConfigSetting(SettingNames.KnowledgeSettings);

    const categoryItem = useMemo(
        (): CategoryNavItemInput => ({
            value: PrimaryNavItemValues.Builder,
            label: intl.formatMessage(SreAgentTabResources.builder),
            icon: bundleIcon(Toolbox20Filled, Toolbox20Regular),
            filledIcon: Toolbox20Filled,
            isCollapsed: !isNavOpen,
            isVisible:
                incidentVisible ||
                showScheduledTasksTab ||
                showExtendedAgentsGraphTab ||
                showConnectors ||
                showKnowledgeSettings ||
                controlPlaneTabsVisible,
            disabled: false,
        }),
        [
            isNavOpen,
            incidentVisible,
            showScheduledTasksTab,
            showExtendedAgentsGraphTab,
            showConnectors,
            showKnowledgeSettings,
            controlPlaneTabsVisible,
            intl,
        ]
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
            {
                value: SecondaryNavItemValues.Connectors,
                isVisible: showConnectors && controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SreAgentTabResources.connectors),
            },
            {
                value: SecondaryNavItemValues.KnowledgeBase,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SreAgentTabResources.knowledgeBase),
            },
            {
                value: SecondaryNavItemValues.KnowledgeSettings,
                isVisible: showKnowledgeSettings && controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SreAgentTabResources.knowledgeSettings),
            },
        ];
    }, [
        incidentVisible,
        intl,
        incidentDisabled,
        showScheduledTasksTab,
        showExtendedAgentsGraphTab,
        showConnectors,
        controlPlaneTabsVisible,
        showKnowledgeSettings,
    ]);

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
