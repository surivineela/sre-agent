import { bundleIcon, LinkSettings24Filled, LinkSettings24Regular, Settings20Filled, Settings20Regular } from '@fluentui/react-icons';
import { FC, memo, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SettingNames, useConfigSetting } from '../../../Common/Hooks/ConfigSettings';
import { IncidentManagementResources, SettingsTabResources, SreAgentTabResources } from '../../../Strings/SREAgentResources';
import { CategoryNavItemInput, PrimaryNavItemValues, SecondaryNavItemValues, SubNavItemInput } from '../../Contracts/SreAgentSpace';
import CategoryNavItem from './CategoryNavItem';

interface ISettingsCategoryNavItemProps {
    isNavOpen: boolean;
    controlPlaneTabsVisible: boolean;
    incidentVisible: boolean;
    incidentDisabled: boolean;
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const LinkSettings20FilledWrapper = (props: any) => <LinkSettings24Filled {...props} style={{ width: 20, height: 20, ...props.style }} />;
const LinkSettings20RegularWrapper = (props: any) => <LinkSettings24Regular {...props} style={{ width: 20, height: 20, ...props.style }} />;

const SettingsCategoryNavItem: FC<ISettingsCategoryNavItemProps> = ({
    isNavOpen,
    controlPlaneTabsVisible,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
}) => {
    const intl = useIntl();

    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);

    const showControlPlaneDependentFeatures = !AzPortalProxy.inStandaloneMode && !isCrossTenantPortalMode;
    const showConnectors = useConfigSetting(SettingNames.Connectors);
    const showSubAgents = useConfigSetting(SettingNames.ShowSubAgentsItemInSettings);
    const showMcpServer = useConfigSetting(SettingNames.McpServer);
    const showPermissionsInSettings = useConfigSetting(SettingNames.ShowPermissionsInSettings);
    const showKnowledgeSettings = useConfigSetting(SettingNames.KnowledgeSettings);

    const categoryItem = useMemo(
        (): CategoryNavItemInput => ({
            value: PrimaryNavItemValues.Settings,
            label: intl.formatMessage(SreAgentTabResources.settings),
            icon: bundleIcon(Settings20Filled, Settings20Regular),
            filledIcon: Settings20Filled,
            isCollapsed: !isNavOpen,
            isVisible:
                controlPlaneTabsVisible ||
                (incidentVisible && showControlPlaneDependentFeatures) ||
                (showPermissionsInSettings && !isCrossTenantPortalMode),
            disabled: false,
        }),
        [
            intl,
            isNavOpen,
            controlPlaneTabsVisible,
            incidentVisible,
            showControlPlaneDependentFeatures,
            showPermissionsInSettings,
            isCrossTenantPortalMode,
        ]
    );

    const subItems = useMemo((): SubNavItemInput[] => {
        const items: SubNavItemInput[] = [
            {
                value: SecondaryNavItemValues.Basics,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.basics),
            },
            {
                value: SecondaryNavItemValues.IncidentPlatform,
                isVisible: incidentVisible && showControlPlaneDependentFeatures,
                disabled: incidentDisabled,
                label: intl.formatMessage(IncidentManagementResources.incidentPlatform),
                icon: bundleIcon(LinkSettings20FilledWrapper, LinkSettings20RegularWrapper),
            },
            {
                value: SecondaryNavItemValues.ManagedResources,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.managedResources),
            },
            {
                value: SecondaryNavItemValues.GrafanaDashboard,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.grafanaDashboard),
            },
            {
                value: SecondaryNavItemValues.Connectors,
                isVisible: showConnectors && controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.connectors),
            },
            {
                value: SecondaryNavItemValues.KnowledgeBase,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.knowledgeBase),
            },
            {
                value: SecondaryNavItemValues.KnowledgeSettings,
                isVisible: showKnowledgeSettings && controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.knowledgeSettings),
            },
            {
                value: SecondaryNavItemValues.AzureSettings,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.azureSettings),
            },
            {
                value: SecondaryNavItemValues.Permissions,
                isVisible: showPermissionsInSettings && !isCrossTenantPortalMode,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.crossTenantPermissions),
            },
            {
                value: SecondaryNavItemValues.SubAgents,
                isVisible: showSubAgents && controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.subAgents),
            },
            {
                value: SecondaryNavItemValues.McpServers,
                isVisible: showMcpServer && controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.mcpServers),
            },
            {
                value: SecondaryNavItemValues.Usage,
                isVisible: controlPlaneTabsVisible,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.usage),
            },
        ];

        return items;
    }, [
        intl,
        controlPlaneTabsVisible,
        incidentVisible,
        showControlPlaneDependentFeatures,
        incidentDisabled,
        showConnectors,
        showSubAgents,
        showMcpServer,
        showPermissionsInSettings,
        showKnowledgeSettings,
        isCrossTenantPortalMode,
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

export default memo(SettingsCategoryNavItem);
