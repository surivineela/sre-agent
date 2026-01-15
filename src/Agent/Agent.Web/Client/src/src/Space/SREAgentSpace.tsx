import { CopilotNavDrawer, CopilotNavDrawerBody, CopilotNavDrawerHeader, tokens as copilotTokens } from '@fluentui-copilot/react-copilot';
import { Body1, Button, mergeClasses, Subtitle1 } from '@fluentui/react-components';
import { FC, memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { createHashRouter, Outlet, RouterProvider, useLocation } from 'react-router-dom';
import { HttpResponseObject } from '../Common/ArmHelper.types';
import AzPortalProxy from '../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../Common/Clients/AppInsightsClient';
import { RouteErrorBoundary } from '../Common/Components/RouteErrorBoundary';
import WarningBanner from '../Common/Components/WarningBanner';
import { ArmObj } from '../Common/Contracts/Azure/ArmObj';
import { Agent, AgentAccessLevel, IncidentManagementType } from '../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../Common/Helpers/ResourceDescriptors';
import { AntUxStringComparison, equals } from '../Common/Helpers/Strings';
import { useScrollableComponentStyles } from '../Common/Styles/Scrollable';
import { SreAgentResources } from '../Strings/SREAgentResources';
import Thread from './Activities/Thread';
import FeedbackMenu from './Components/Nav/FeedbackMenu';
import NavBarOpenCloseButton from './Components/Nav/NavBarOpenCloseButton';
import NonThreadCategoryNavItems from './Components/Nav/NonThreadCategoryNavItems';
import ThreadsNav from './Components/Nav/ThreadsNav';
import { DirtyStateContext, SreAgentContext, SreAgentSpaceContext } from './Contracts/Context';
import { PermissionProvider } from './Contracts/PermissionContext';
import { PrimaryNavItemValues, SecondaryNavItemValues } from './Contracts/SreAgentSpace';
import DailyReports from './DailyReports/DailyReports';
import ExtendedAgentGraph from './Graph/ExtendedAgentGraph';
import Graph from './Graph/Graph';
import { useIncidentManagementConnectivity } from './Hooks/useIncidentManagementConnectivity';
import { useIncidentPlatformType } from './Hooks/useIncidentPlatformType';
import { useSreAgentSpace } from './Hooks/useSreAgentSpace';
import { DirtyStateNavigationConfirmDialog } from './IncidentManagement/CreateIncidentHandler/NavigationConfirmDialog';
import IncidentManagement from './IncidentManagement/IncidentManagement';
import { ScheduledTasks } from './ScheduledTasks/ScheduledTasks.ReactView';
import SessionInsights from './SessionInsights/SessionInsights';
import { useSreAgent } from './Settings/Hooks/useSreAgent';
import Settings from './Settings/Settings.ReactView';
import { useCommonStyles } from './Styles/Common.styles';
import { useSreAgentSpaceStyles } from './Styles/SreAgentSpaceStyles';
import { getCategoryNavItemIdFromPathName, getNavItemIdFromPathName, getPathName } from './Utilities';

/*
NOTE: The current idea is to do only data plane (agent site), and NOT control plane (ARM) calls, in
the Activities and Resource mapping tabs so that they can be used in standalone and cross-tenant.
If you do see AzPortalContext, it's likely telemetry, and/or something that checks these states internally
*/

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
    const sreAgentContext = useContext(SreAgentContext);
    const { isDirty } = useContext(DirtyStateContext);
    const { isCrossTenantPortalMode, resourceId } = useContext(EnvironmentContext);
    const azPortalProxy = useContext(AzPortalContext);

    const {
        agent: { setMode },
        agentObj,
        startAgent,
    } = sreAgentContext;

    const location = useLocation();
    const styles = useSreAgentSpaceStyles();
    const { scrollable } = useScrollableComponentStyles();

    // Log route/view changes
    useEffect(() => {
        azPortalProxy.log({
            action: 'route-navigation',
            actionModifier: 'view',
            resourceId,
            data: {
                hostname: window.location.hostname,
                pathname: location.pathname,
                hash: location.hash,
                referrer: document.referrer,
            },
        });
    }, [location, azPortalProxy, resourceId]);

    const { controlPlaneTabsVisible, incidentManagementTabVisible, incidentManagementTabDisabled, logsTabDisabled, onLogsClick } =
        useControlPlaneDependentTabs({
            inStandaloneMode,
            isCrossTenantPortalMode,
        });

    const {
        threadsRenderKey,
        addThread,
        selectThread,
        updateThreadLastReadTime,
        deleteThread,
        subscribeThreadFavoriteUpdate,
        subscribeThreadTitleUpdate,
        updateThreadTitle,
        updateThreadFavorite,
        isNavOpen,
        onExpandOrCollapseNavBar,
        navBarType,
        navBarTypeWhenOpen,
        isNavBarHidden,
        openedCategoryNavItems,
        onClickCategoryNavItem,
        onClickNonThreadSubNavItem,
        ...threadsNavProps
    } = useSreAgentSpace();

    const isAgentStopped = useMemo(
        () => equals(agentObj?.properties?.powerState || '', 'Stopped', AntUxStringComparison.IgnoreCase),
        [agentObj]
    );

    useEffect(() => {
        if (inStandaloneMode) {
            setMode('standalone');
        }
    }, [setMode]);

    return (
        <SreAgentSpaceContext.Provider
            value={{
                isNavOpen,
                navBarTypeWhenOpen,
                onExpandOrCollapseNavBar,
                addThread,
                selectThread,
                deleteThread,
                updateThreadLastReadTime,
                updateThreadTitle,
                updateThreadFavorite,
                threadsRenderKey,
                subscribeThreadTitleUpdate,
                subscribeThreadFavoriteUpdate,
            }}
        >
            <div className={styles.root}>
                <DirtyStateNavigationConfirmDialog isDirty={isDirty} />
                <WarningBanner />
                <div className={styles.content}>
                    {!isAgentStopped && (
                        <div>
                            <CopilotNavDrawer
                                open={true}
                                type={navBarType}
                                selectedValue={getNavItemIdFromPathName(location.pathname)}
                                selectedCategoryValue={getCategoryNavItemIdFromPathName(location.pathname)}
                                style={{
                                    width: isNavOpen ? undefined : '56px',
                                    transition: 'width 0.25s ease',
                                    height: '100%',
                                    padding: `${copilotTokens.spacingVerticalS} 0px 0px 0px`,
                                }}
                                openCategories={openedCategoryNavItems}
                                tabbable={true}
                            >
                                <CopilotNavDrawerHeader
                                    style={{
                                        padding: `${copilotTokens.spacingVerticalS} ${copilotTokens.spacingVerticalM}`,
                                        display: 'flex',
                                        flexDirection: 'column',
                                        justifyContent: 'center',
                                        alignItems: 'flex-start',
                                    }}
                                >
                                    <NavBarOpenCloseButton isNavOpen={isNavOpen} onExpandOrCollapseNavBar={onExpandOrCollapseNavBar} />
                                </CopilotNavDrawerHeader>

                                <CopilotNavDrawerBody
                                    className={mergeClasses(scrollable, styles.navBody, isNavOpen ? undefined : styles.collapsedNavBody)}
                                >
                                    <NonThreadCategoryNavItems
                                        isNavOpen={isNavOpen}
                                        openedCategoryNavItems={openedCategoryNavItems}
                                        threads={threadsNavProps.threadListsState.regularThreadListState.threads}
                                        selectThread={selectThread}
                                        excludedSources={threadsNavProps.excludedSources}
                                        controlPlaneTabsVisible={controlPlaneTabsVisible}
                                        logsTabDisabled={logsTabDisabled}
                                        onLogsClick={onLogsClick}
                                        incidentVisible={incidentManagementTabVisible}
                                        incidentDisabled={incidentManagementTabDisabled}
                                        onClickCategoryNavItem={onClickCategoryNavItem}
                                        onClickSubNavItem={onClickNonThreadSubNavItem}
                                    />
                                    <ThreadsNav
                                        {...threadsNavProps}
                                        isNavOpen={isNavOpen}
                                        onClickCategoryNavItem={onClickCategoryNavItem}
                                    />
                                </CopilotNavDrawerBody>
                                <div
                                    style={{
                                        padding: copilotTokens.spacingVerticalS,
                                        borderTop: `1px solid ${copilotTokens.colorNeutralStroke2}`,
                                        display: 'flex',
                                    }}
                                >
                                    <FeedbackMenu isNavOpen={isNavOpen} />
                                </div>
                            </CopilotNavDrawer>
                        </div>
                    )}
                    <OutletComponent isAgentStopped={isAgentStopped} startAgent={startAgent} isNavBarHidden={isNavBarHidden} />
                </div>
            </div>
        </SreAgentSpaceContext.Provider>
    );
};

const OutletComponent = memo(
    ({
        isNavBarHidden,
        isAgentStopped,
        startAgent,
    }: {
        isNavBarHidden: boolean;
        isAgentStopped: boolean;
        startAgent: () => Promise<HttpResponseObject<ArmObj<Agent>>>;
    }) => {
        const intl = useIntl();
        const { stoppedAgentComponentContainer, stoppedAgentComponentFlexBox, outletRoot, outletRootWithNoNavBar } =
            useSreAgentSpaceStyles();
        const commonStyles = useCommonStyles();

        return (
            <div className={mergeClasses(outletRoot, isNavBarHidden ? outletRootWithNoNavBar : undefined)}>
                {isAgentStopped ? (
                    <div className={mergeClasses(stoppedAgentComponentContainer, commonStyles.contentRootBorderAndBackground)}>
                        <div className={stoppedAgentComponentFlexBox}>
                            <img
                                src={'ChatDismissSpotIllustration.svg'}
                                style={{ width: '120px', height: '120px' }}
                                alt={intl.formatMessage(SreAgentResources.stopped)}
                            />
                            <Subtitle1>{intl.formatMessage(SreAgentResources.sreAgentStoppedTitle)}</Subtitle1>
                            <Body1>{intl.formatMessage(SreAgentResources.sreAgentStoppedDescription)}</Body1>
                            <Button appearance="primary" onClick={() => startAgent()}>
                                {intl.formatMessage(SreAgentResources.startAgent)}
                            </Button>
                        </div>
                    </div>
                ) : (
                    <Outlet />
                )}
            </div>
        );
    }
);

const router = createHashRouter([
    {
        path: '/',
        element: <TabsListWrapper />,
        errorElement: <RouteErrorBoundary />,
        children: [
            { index: true, element: <Thread /> },

            {
                path: getPathName(false, PrimaryNavItemValues.Activities),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.IncidentOverview} />,
            },
            {
                path: getPathName(false, PrimaryNavItemValues.Activities, [SecondaryNavItemValues.IncidentOverview]),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.IncidentOverview} />,
            },
            {
                path: getPathName(false, PrimaryNavItemValues.Activities, [SecondaryNavItemValues.IncidentOverview, ':incidentId']),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.IncidentOverview} />,
            },
            { path: getPathName(false, PrimaryNavItemValues.Activities, [SecondaryNavItemValues.DailyReports]), element: <DailyReports /> },

            { path: getPathName(false, PrimaryNavItemValues.Monitor), element: <SessionInsights /> },
            {
                path: getPathName(false, PrimaryNavItemValues.Monitor, [SecondaryNavItemValues.SessionInsights]),
                element: <SessionInsights />,
            },
            { path: getPathName(false, PrimaryNavItemValues.Monitor, [SecondaryNavItemValues.Graphs]), element: <Graph /> },
            {
                path: getPathName(false, PrimaryNavItemValues.Monitor, [SecondaryNavItemValues.Graphs, 'groups', ':groupId']),
                element: <Graph />,
            },
            {
                path: getPathName(false, PrimaryNavItemValues.Monitor, [SecondaryNavItemValues.Metrics]),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.Metrics} />,
            },

            {
                path: getPathName(false, PrimaryNavItemValues.Builder),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.ResponsePlans} />,
            },
            {
                path: getPathName(false, PrimaryNavItemValues.Builder, [SecondaryNavItemValues.ResponsePlans]),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.ResponsePlans} />,
            },
            {
                path: getPathName(false, PrimaryNavItemValues.Builder, [SecondaryNavItemValues.ScheduledTasks]),
                element: <ScheduledTasks />,
            },
            {
                path: getPathName(false, PrimaryNavItemValues.Builder, [SecondaryNavItemValues.ExtendedAgentsGraph]),
                element: <ExtendedAgentGraph />,
            },

            { path: getPathName(false, PrimaryNavItemValues.Settings), element: <Settings /> },
            { path: getPathName(false, PrimaryNavItemValues.Settings, [':menuItem']), element: <Settings /> },
            {
                path: getPathName(false, PrimaryNavItemValues.Settings, [SecondaryNavItemValues.IncidentPlatform]),
                element: <IncidentManagement menuItem={SecondaryNavItemValues.IncidentPlatform} />,
            },

            { path: getPathName(false, PrimaryNavItemValues.Threads, [':threadId']), element: <Thread /> },
            { path: getPathName(false, PrimaryNavItemValues.Threads), element: <Thread /> },
            { path: '*', element: <Thread /> },
        ],
    },
]);

const SREAgentSpace: FC = () => {
    const azPortalProxy = useContext(AzPortalContext);
    const { isCrossTenantPortalMode, resourceId } = useContext(EnvironmentContext);
    const [isDirty, setIsDirty] = useState(false);

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
        startAgent,
        stopAgent,
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
            const isCrossTenantMode = AzPortalProxy.envInfo.isCrossTenantPortalMode;
            const isHostedInSreaPortal = AzPortalProxy.isHostedInSreaPortal;

            let mode = 'standalone';
            if (!inStandaloneMode) {
                const portalPrefix = isHostedInSreaPortal ? 'srea-' : '';
                mode = isCrossTenantMode ? `cross-tenant-${portalPrefix}portal` : `${portalPrefix}portal`;
            }

            if (version) {
                console.log(`
                    ╔═══════════════════════════════════╗
                      🤖 SRE Agent Version: ${version}
                      Mode: ${mode}
                    ╚═══════════════════════════════════╝
                `);
            }

            if (!inStandaloneMode) {
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
                startAgent,
                stopAgent,
            }}
        >
            <PermissionProvider>
                <DirtyStateContext.Provider value={{ isDirty, setIsDirty }}>
                    <RouterProvider router={router} />
                </DirtyStateContext.Provider>
            </PermissionProvider>
        </SreAgentContext.Provider>
    );
};

export default SREAgentSpace;
