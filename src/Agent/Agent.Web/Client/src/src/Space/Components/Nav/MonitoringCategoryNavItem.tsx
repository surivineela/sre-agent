import {
    bundleIcon,
    ChartMultiple20Filled,
    ChartMultiple20Regular,
    Eye20Filled,
    Eye20Regular,
    Open20Filled,
    Open20Regular,
    Organization20Filled,
    Organization20Regular,
} from '@fluentui/react-icons';
import { FC, memo, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { useFeatureFlags } from '../../../Common/Hooks/useFeatureFlags';
import { IncidentManagementResources, SettingsTabResources, SreAgentTabResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { CategoryNavItemInput, PrimaryNavItemValues, SecondaryNavItemValues, SubNavItemInput } from '../../Contracts/SreAgentSpace';
import { useAgentSiteNavigate } from '../../Hooks/useAgentSiteNavigate';
import CategoryNavItem from './CategoryNavItem';

interface IMonitoringCategoryNavItemProps {
    isNavOpen: boolean;
    controlPlaneTabsVisible: boolean;
    logsTabDisabled: boolean;
    onLogsClick: () => void;
    incidentVisible: boolean;
    incidentDisabled: boolean;
    onClickCategoryNavItem: (tabValue: PrimaryNavItemValues) => void;
    onClickSubNavItem: (tabValue: PrimaryNavItemValues, secondaryNavItem: SecondaryNavItemValues) => void;
}

const MonitoringCategoryNavItem: FC<IMonitoringCategoryNavItemProps> = ({
    isNavOpen,
    controlPlaneTabsVisible,
    onLogsClick,
    logsTabDisabled,
    incidentVisible,
    incidentDisabled,
    onClickCategoryNavItem,
    onClickSubNavItem,
}) => {
    const [disableAnalysis, setDisableAnalysis] = useState(false);

    const intl = useIntl();
    const { features } = useFeatureFlags();

    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const {
        agentObj,
        incidentManagement: { incidentPlatformType },
    } = useContext(SreAgentContext);

    const showSessionInsights = features.sessionInsights;
    const showControlPlaneDependentFeatures = !AzPortalProxy.inStandaloneMode && !isCrossTenantPortalMode;

    const agentAppInsightsAppId = useMemo<string | undefined>(
        () => agentObj?.properties?.logConfiguration?.applicationInsightsConfiguration?.appId,
        [agentObj]
    );

    const navigate = useAgentSiteNavigate();

    const categoryItem = useMemo(
        (): CategoryNavItemInput => ({
            value: PrimaryNavItemValues.Monitor,
            label: intl.formatMessage(SreAgentTabResources.monitor),
            icon: bundleIcon(Eye20Filled, Eye20Regular),
            isVisible: isNavOpen,
            disabled: false,
        }),
        [isNavOpen, intl]
    );

    const subItems = useMemo((): SubNavItemInput[] => {
        return [
            {
                value: SecondaryNavItemValues.SessionInsights,
                isVisible: showSessionInsights,
                disabled: false,
                label: intl.formatMessage(SettingsTabResources.sessionInsights),
            },
            {
                value: SecondaryNavItemValues.Graphs,
                isVisible: true,
                disabled: false,
                label: intl.formatMessage(SreAgentTabResources.resourceMapping),
                icon: bundleIcon(Organization20Filled, Organization20Regular),
            },
            {
                value: SecondaryNavItemValues.Metrics,
                isVisible: showControlPlaneDependentFeatures && incidentVisible,
                label: intl.formatMessage(IncidentManagementResources.metrics),
                disabled: disableAnalysis || !agentAppInsightsAppId || incidentDisabled,
                icon: bundleIcon(ChartMultiple20Filled, ChartMultiple20Regular),
            },
            {
                isVisible: controlPlaneTabsVisible,
                disabled: logsTabDisabled,
                icon: bundleIcon(Open20Filled, Open20Regular),
                value: SecondaryNavItemValues.Logs,
                label: intl.formatMessage(SreAgentTabResources.logs),
                onClick: onLogsClick,
            },
        ];
    }, [
        showSessionInsights,
        intl,
        showControlPlaneDependentFeatures,
        incidentVisible,
        disableAnalysis,
        agentAppInsightsAppId,
        incidentDisabled,
        controlPlaneTabsVisible,
        logsTabDisabled,
        onLogsClick,
    ]);

    useEffect(() => {
        if (incidentPlatformType) {
            if (incidentPlatformType === IncidentManagementType.None) {
                setDisableAnalysis(true);
                navigate({
                    primaryNavItemValue: PrimaryNavItemValues.Settings,
                    secondaryNavItemValue: SecondaryNavItemValues.IncidentPlatform,
                });
            } else {
                setDisableAnalysis(false);
            }
        }
    }, [incidentPlatformType, navigate]);

    return (
        <CategoryNavItem
            categoryItem={categoryItem}
            subItems={subItems}
            onClickCategoryNavItem={onClickCategoryNavItem}
            onClickSubNavItem={onClickSubNavItem}
        />
    );
};

export default memo(MonitoringCategoryNavItem);
