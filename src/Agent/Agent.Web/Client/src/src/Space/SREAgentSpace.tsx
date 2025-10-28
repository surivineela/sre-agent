import { ThemeContext } from '@fluentui/react';
import {
    Button,
    SelectTabData,
    SelectTabEvent,
    Tab,
    TabList,
    TeachingPopover,
    TeachingPopoverBody,
    TeachingPopoverFooter,
    TeachingPopoverSurface,
    TeachingPopoverTitle,
} from '@fluentui/react-components';
import { LineHorizontal120Regular, Open16Regular, PersonFeedback20Regular } from '@fluentui/react-icons';
import type { Theme } from '@fluentui/theme';
import { FC, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { createHashRouter, Outlet, RouterProvider, useLocation, useNavigate } from 'react-router-dom';
import AzPortalProxy from '../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext, useAzPortalContext } from '../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../Common/Clients/AppInsightsClient';
import RbacWarningBanner from '../Common/Components/RbacWarningBanner';
import { AgentAccessLevel, IncidentManagementType } from '../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../Common/Helpers/ResourceDescriptors';
import { SettingNames, useConfigSetting } from '../Common/Hooks/ConfigSettings';
import { useFeatureFlags } from '../Common/Hooks/useFeatureFlags';
import { LocalStorageFlags, useLocalStorage } from '../Common/Hooks/useLocalStorage';
import {
    ExtendedAgentsGraphResources,
    IncidentManagementResources,
    SreAgentResources,
    SreAgentTabResources,
} from '../Strings/SREAgentResources';
import Activities from './Activities/Activities.ReactView';
import { FeedbackDialog } from './Components/FeedbackDialog';
import { SreAgentContext } from './Contracts/Context';
import { PermissionProvider } from './Contracts/PermissionContext';
import DailyReports from './DailyReports/DailyReports';
import ExtendedAgentGraph from './Graph/ExtendedAgentGraph';
import Graph from './Graph/Graph';
import { useIncidentManagementConnectivity } from './Hooks/useIncidentManagementConnectivity';
import { useIncidentPlatformType } from './Hooks/useIncidentPlatformType';
import IncidentManagement from './IncidentManagement/IncidentManagement';
import { ScheduledTasks } from './ScheduledTasks/V2/ScheduledTasks.ReactView';
import { useSreAgent } from './Settings/Hooks/useSreAgent';
import Settings from './Settings/Settings.ReactView';
import { useSreAgentSpaceStyles } from './Settings/Styles/SreAgentSpaceStyles';

/*
NOTE: The current idea is to do only data plane (agent site), and NOT control plane (ARM) calls, in
the Activities and Resource mapping tabs so that they can be used in standalone and cross-tenant.

If you do see AzPortalContext, it's likely telemetry, and/or something that checks these states internally
*/

const getTabListStyle = (theme: Theme) => {
    return {
        backgroundColor: theme.semanticColors.bodyBackground,
    };
};

enum TabValues {
    Activities = 'activities',
    Settings = 'settings',
    Graph = 'graph',
    Logs = 'logs',
    IncidentManagement = 'incidentmanagement',
    DailyReports = 'dailyreports',
    ScheduledTasks = 'scheduledtasks',
    ExtendedAgentsGraph = 'extendedagentsgraph',
}

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

const query = `traces | where timestamp > ago(1d)`;
const source = `PaasServerless.SreAgentSpace`;

interface UseControlPlaneDependentTabsProps {
    inStandaloneMode: boolean | undefined;
    isCrossTenantPortalMode: boolean | undefined;
}

const useControlPlaneDependentTabs = ({ inStandaloneMode, isCrossTenantPortalMode }: UseControlPlaneDependentTabsProps) => {
    const environmentContext = useContext(EnvironmentContext);
    const sreAgentContext = useContext(SreAgentContext);
    const {
        agent: { setMode, setAccessLevel },
        incidentManagement: { incidentPlatformType, incidentPlatformTypeLoaded },
    } = sreAgentContext;

    const { agent, agentLoaded } = useSreAgent(environmentContext.resourceId);

    const [appInsightsResourceId, setAppInsightsResourceId] = useState<string>();

    const fetchAppInsightsId = useCallback(async () => {
        const { subscription, resourceGroup } = new ArmResourceDescriptor(environmentContext.resourceId);
        const response = await AppInsightsClient.getAppInsightsComponentFromAppId(
            [subscription],
            resourceGroup,
            agent?.properties.logConfiguration?.applicationInsightsConfiguration?.appId || ''
        );
        if (response) {
            setAppInsightsResourceId(response);
        }
    }, [
        environmentContext.resourceId,
        agent?.properties.logConfiguration?.applicationInsightsConfiguration?.appId,
        setAppInsightsResourceId,
    ]);

    const onLogsClick = useCallback(() => {
        if (appInsightsResourceId) {
            window.open(
                `https://portal.azure.com#view/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/query/${query}/resourceId/${encodeURIComponent(
                    appInsightsResourceId
                )}/source/${source}`,
                '_blank'
            );
        }
    }, [appInsightsResourceId]);

    useEffect(() => {
        if (agent) {
            fetchAppInsightsId();
            setMode(agent.properties.actionConfiguration?.mode ?? '');
            setAccessLevel(agent.properties.actionConfiguration?.accessLevel ?? AgentAccessLevel.low);
        }
    }, [agent, fetchAppInsightsId, setAccessLevel, setMode]);

    return {
        controlPlaneTabsVisible: !inStandaloneMode && !isCrossTenantPortalMode,
        incidentManagementTabVisible:
            (!inStandaloneMode && !isCrossTenantPortalMode) || (!!incidentPlatformType && incidentPlatformType !== 'None'),
        incidentManagementTabDisabled: !incidentPlatformTypeLoaded,
        logsTabDisabled: !agentLoaded || !appInsightsResourceId,
        onLogsClick,
    };
};

const TabsListWrapper: FC = () => {
    const theme = useContext(ThemeContext);
    const sreAgentContext = useContext(SreAgentContext);
    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const { logAmplitudeNavigationEvent } = useAzPortalContext();

    const {
        activities: { lastVisitedThreadId },
        agent: { setMode },
    } = sreAgentContext;
    const intl = useIntl();
    const location = useLocation();
    const navigate = useNavigate();
    const styles = useSreAgentSpaceStyles();

    const [isFeedbackDialogOpen, setIsFeedbackDialogOpen] = useState(false);

    const incidentManagementTabRef = useRef<HTMLButtonElement>(null);
    const { item: isIncidentManagementTeachingPopoverDismissed, setItem: setIsIncidentManagementTeachingPopoverDismissed } =
        useLocalStorage(LocalStorageFlags.IncidentManagementPopoverDismissed);

    const showIncidentManagementTeachingPopover = useMemo(() => {
        return isIncidentManagementTeachingPopoverDismissed !== 'true';
    }, [isIncidentManagementTeachingPopoverDismissed]);

    const showScheduledTasksTab = useConfigSetting(SettingNames.ShowScheduledTasksTab);

    // Show extended agents graph tab based on backend feature flag only
    const { features } = useFeatureFlags();
    const showExtendedAgentsGraphTab = features.extendedAgentsGraph;

    const { controlPlaneTabsVisible, incidentManagementTabVisible, incidentManagementTabDisabled, logsTabDisabled, onLogsClick } =
        useControlPlaneDependentTabs({
            inStandaloneMode,
            isCrossTenantPortalMode,
        });

    const selectedValue = useMemo(() => {
        if (location.pathname?.startsWith('/views/activities')) {
            return TabValues.Activities;
        }
        if (location.pathname?.startsWith('/views/resourcegraph')) {
            return TabValues.Graph;
        }
        if (location.pathname?.startsWith('/views/settings')) {
            return TabValues.Settings;
        }
        if (location.pathname?.startsWith('/views/incidentmanagement')) {
            return TabValues.IncidentManagement;
        }
        if (location.pathname?.startsWith('/views/dailyreports')) {
            return TabValues.DailyReports;
        }
        if (location.pathname?.startsWith('/views/scheduledtasks')) {
            return TabValues.ScheduledTasks;
        }
        if (location.pathname?.startsWith('/views/extendedagentsgraph')) {
            return TabValues.ExtendedAgentsGraph;
        }
        return TabValues.Activities;
    }, [location.pathname]);

    const onTabSelect = useCallback(
        (_: SelectTabEvent, data: SelectTabData) => {
            const tabValue = typeof data.value === 'string' ? data.value : 'unknown';
            logAmplitudeNavigationEvent({
                targetType: 'tab',
                targetAction: 'tabItem',
                targetName: tabValue,
                targetFriendlyName: tabValue,
            });

            if (data.value === TabValues.Activities) {
                if (!location.pathname?.startsWith('/views/activities')) {
                    navigate({ ...location, pathname: `/views/activities${lastVisitedThreadId ? `/threads/${lastVisitedThreadId}` : ''}` });
                }
            } else if (data.value === TabValues.Graph) {
                navigate({ ...location, pathname: '/views/resourcegraph' });
            } else if (data.value === TabValues.IncidentManagement) {
                if (!location.pathname?.startsWith('/views/incidentmanagement')) {
                    navigate({ ...location, pathname: '/views/incidentmanagement' });
                }
            } else if (data.value === TabValues.Settings) {
                if (!location.pathname?.startsWith('/views/settings')) {
                    navigate({ ...location, pathname: '/views/settings' });
                }
            } else if (data.value === TabValues.DailyReports) {
                navigate({ ...location, pathname: '/views/dailyreports' });
            } else if (data.value === TabValues.ScheduledTasks) {
                navigate({ ...location, pathname: '/views/scheduledtasks' });
            } else if (data.value === TabValues.ExtendedAgentsGraph) {
                navigate({ ...location, pathname: '/views/extendedagentsgraph' });
            } else if (data.value === TabValues.Logs) {
                onLogsClick();
            }
        },
        [location, navigate, onLogsClick, logAmplitudeNavigationEvent, lastVisitedThreadId]
    );

    useEffect(() => {
        if (inStandaloneMode) {
            setMode('standalone');
        }
    }, [setMode]);

    return (
        <div>
            <RbacWarningBanner />
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'row',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    backgroundColor: (theme as Theme).semanticColors.bodyBackground,
                }}
            >
                <TabList selectedValue={selectedValue} onTabSelect={onTabSelect} style={getTabListStyle(theme as Theme)}>
                    <Tab id="Activities" value={TabValues.Activities}>
                        {intl.formatMessage(SreAgentTabResources.activities)}
                    </Tab>
                    {incidentManagementTabVisible && (
                        <>
                            <Tab
                                id="IncidentManagement"
                                value={TabValues.IncidentManagement}
                                ref={incidentManagementTabRef}
                                disabled={incidentManagementTabDisabled}
                            >
                                {intl.formatMessage(SreAgentTabResources.incidentManagement)}
                            </Tab>
                            <TeachingPopover
                                appearance="brand"
                                open={showIncidentManagementTeachingPopover}
                                withArrow={true}
                                positioning={{ target: incidentManagementTabRef.current, position: 'below', align: 'start' }}
                                onOpenChange={(_, data) => {
                                    if (!data.open) {
                                        setIsIncidentManagementTeachingPopoverDismissed('true');
                                    }
                                }}
                            >
                                <TeachingPopoverSurface>
                                    <TeachingPopoverBody>
                                        <TeachingPopoverTitle>
                                            {intl.formatMessage(IncidentManagementResources.incidentThreadsMovedTitle)}
                                        </TeachingPopoverTitle>
                                        <div style={{ maxWidth: '280px', wordWrap: 'break-word' }}>
                                            {intl.formatMessage(IncidentManagementResources.incidentThreadsMovedDescription)}
                                        </div>
                                    </TeachingPopoverBody>
                                    <TeachingPopoverFooter primary={intl.formatMessage(SreAgentResources.gotIt)} />
                                </TeachingPopoverSurface>
                            </TeachingPopover>
                        </>
                    )}
                    <Tab id="Knowledge" value={TabValues.Graph}>
                        {intl.formatMessage(SreAgentTabResources.resourceMapping)}
                    </Tab>
                    <Tab id="DailyReports" value={TabValues.DailyReports}>
                        {intl.formatMessage(SreAgentTabResources.dailyReports)}
                    </Tab>
                    {showScheduledTasksTab && (
                        <Tab id="ScheduledTasks" value={TabValues.ScheduledTasks}>
                            {'Scheduled Tasks'}
                        </Tab>
                    )}
                    {showExtendedAgentsGraphTab && (
                        <Tab id="ExtendedAgentsGraph" value={TabValues.ExtendedAgentsGraph}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.extendedAgentsTab)}
                        </Tab>
                    )}
                    {controlPlaneTabsVisible && (
                        <>
                            <Tab id="Settings" value={TabValues.Settings}>
                                {intl.formatMessage(SreAgentTabResources.settings)}
                            </Tab>{' '}
                            <LineHorizontal120Regular className={styles.lineIconStyle} />
                            <Tab id="Logs" value={TabValues.Logs} disabled={logsTabDisabled}>
                                <div className={styles.logsMenuItemContainer}>
                                    <Open16Regular />
                                    {intl.formatMessage(SreAgentTabResources.logs)}
                                </div>
                            </Tab>
                        </>
                    )}
                </TabList>
                <Button
                    style={{ fontWeight: 'normal' }}
                    appearance="transparent"
                    icon={<PersonFeedback20Regular />}
                    onClick={() => setIsFeedbackDialogOpen(true)}
                >
                    {intl.formatMessage(SreAgentTabResources.feedback)}
                </Button>
            </div>
            <FeedbackDialog isOpen={isFeedbackDialogOpen} setIsOpen={setIsFeedbackDialogOpen} />
            <Outlet />
        </div>
    );
};

const router = createHashRouter([
    {
        path: '/',
        element: <TabsListWrapper />,
        children: [
            { index: true, element: <Activities /> },
            { path: 'views/settings/:menuItem', element: <Settings /> },
            { path: 'views/settings', element: <Settings /> },
            { path: 'views/resourcegraph/groups/:groupId', element: <Graph /> },
            { path: 'views/resourcegraph', element: <Graph /> },
            { path: 'views/incidentmanagement/:menuItem', element: <IncidentManagement /> },
            { path: 'views/incidentmanagement', element: <IncidentManagement /> },
            { path: 'views/activities/threads/:threadId', element: <Activities /> },
            { path: 'views/activities', element: <Activities /> },
            { path: 'views/dailyreports', element: <DailyReports /> },
            { path: 'views/scheduledtasks', element: <ScheduledTasks /> },
            { path: 'views/extendedagentsgraph', element: <ExtendedAgentGraph /> },
            { path: '*', element: <Activities /> },
        ],
    },
]);

const SREAgentSpace: FC = () => {
    const azPortalProxy = useContext(AzPortalContext);
    const { isCrossTenantPortalMode, resourceId } = useContext(EnvironmentContext);

    const {
        agent,
        agentLoading,
        agentLoaded,
        agentLoadFailure,
        agentPatching,
        agentPatched,
        agentPatchFailure,
        agentLastUpdatedTime,
        patchAgent,
        refresh,
    } = useSreAgent(resourceId);

    const incidentPlatformTypeHook = useIncidentPlatformType();

    const {
        incidentPlatformType,
        incidentPlatformTypeLoading,
        incidentPlatformTypeLoaded,
        incidentPlatformTypeLoadFailure,
        refreshIncidentPlatformType,
    } = useMemo(() => {
        if (!isCrossTenantPortalMode && !inStandaloneMode) {
            return {
                incidentPlatformType: agent
                    ? agent.properties.incidentManagementConfiguration?.type || IncidentManagementType.None
                    : undefined,
                incidentPlatformTypeLoading: agentLoading,
                incidentPlatformTypeLoaded: agentLoaded,
                incidentPlatformTypeLoadFailure: agentLoadFailure,
                refreshIncidentPlatformType: refresh,
            };
        }

        return {
            incidentPlatformType: incidentPlatformTypeHook.incidentPlatformType,
            incidentPlatformTypeLoading: incidentPlatformTypeHook.incidentPlatformTypeLoading,
            incidentPlatformTypeLoaded: incidentPlatformTypeHook.incidentPlatformTypeLoaded,
            incidentPlatformTypeLoadFailure: incidentPlatformTypeHook.incidentPlatformTypeLoadFailure,
            refreshIncidentPlatformType: incidentPlatformTypeHook.refreshIncidentPlatformType,
        };
    }, [
        isCrossTenantPortalMode,
        agent,
        agentLoading,
        agentLoaded,
        agentLoadFailure,
        refresh,
        incidentPlatformTypeHook.incidentPlatformType,
        incidentPlatformTypeHook.incidentPlatformTypeLoading,
        incidentPlatformTypeHook.incidentPlatformTypeLoaded,
        incidentPlatformTypeHook.incidentPlatformTypeLoadFailure,
        incidentPlatformTypeHook.refreshIncidentPlatformType,
    ]);

    const [isGrafanaUpdating, setIsGrafanaUpdating] = useState(false);
    const [deploymentId, setDeploymentId] = useState<string>('');
    const [notificationId, setNotificationId] = useState<string>('');
    const [agentMode, setAgentMode] = useState<string>('');
    const [agentAccessLevel, setAgentAccessLevel] = useState<AgentAccessLevel>(AgentAccessLevel.low);
    const [lastVisitedThreadId, setLastVisitedThreadId] = useState<string>();

    const shouldPoll = useMemo(
        () => !!incidentPlatformType && incidentPlatformType !== 'None' && !incidentPlatformTypeLoading,
        [incidentPlatformType, incidentPlatformTypeLoading]
    );

    const {
        refresh: refreshConnectivity,
        incidentManagementConnectionState,
        isIncidentManagementConnected,
        setIsIncidentManagementConnected,
        hasFilters,
        setHasFilters,
        isLoading: checkingConnectivity,
    } = useIncidentManagementConnectivity(shouldPoll, agentLastUpdatedTime, incidentPlatformType);

    useEffect(() => {
        const logSiteVersion = () => {
            const version = import.meta.env.SRE_UX_VERSION;
            if (!inStandaloneMode && version) {
                console.log(`
                    ╔═══════════════════════════════════╗
                      🤖 SRE Agent Version: ${version}
                    ╚═══════════════════════════════════╝
                `);
                azPortalProxy.log({
                    action: 'AgentSiteVersion',
                    actionModifier: 'info',
                    data: { version },
                });
            }
        };

        // Log initial and every 60 minutes (for long-running sessions)
        logSiteVersion();
        const interval = setInterval(logSiteVersion, 60 * 60 * 1000);

        return () => clearInterval(interval);
    }, [azPortalProxy]);

    return (
        <SreAgentContext.Provider
            value={{
                activities: {
                    lastVisitedThreadId,
                    setLastVisitedThreadId,
                },
                grafana: {
                    isGrafanaUpdating,
                    deploymentId,
                    notificationId,
                    setNotificationId,
                    setIsGrafanaUpdating,
                    setDeploymentId,
                },
                incidentManagement: {
                    incidentPlatformType,
                    incidentPlatformTypeLoading,
                    incidentPlatformTypeLoaded,
                    incidentPlatformTypeLoadFailure,
                    refreshIncidentPlatformType,
                    incidentManagementConnectionState,
                    isIncidentManagementConnected,
                    setIsIncidentManagementConnected,
                    hasFilters,
                    setHasFilters,
                    checkingConnectivity,
                    refreshConnectivity,
                },
                agent: {
                    mode: agentMode,
                    setMode: setAgentMode,
                    accessLevel: agentAccessLevel,
                    setAccessLevel: setAgentAccessLevel,
                },
                agentObj: agent,
                agentLoading,
                agentLoaded,
                agentLoadFailure,
                agentPatching,
                agentPatched,
                agentPatchFailure,
                agentLastUpdatedTime,
                patchAgent,
                refresh,
            }}
        >
            <PermissionProvider>
                <RouterProvider router={router} />
            </PermissionProvider>
        </SreAgentContext.Provider>
    );
};

export default SREAgentSpace;
