import { ThemeContext } from '@fluentui/react';
import { Button, SelectTabData, SelectTabEvent, Tab, TabList } from '@fluentui/react-components';
import { LineHorizontal120Regular, Open16Regular, PersonFeedback20Regular } from '@fluentui/react-icons';
import type { Theme } from '@fluentui/theme';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { createHashRouter, Outlet, RouterProvider, useLocation, useNavigate } from 'react-router-dom';
import AzPortalProxy from '../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AppInsightsClient } from '../Common/Clients/AppInsightsClient';
import { IncidentManagementType } from '../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../Common/Helpers/ResourceDescriptors';
import { SreAgentTabResources } from '../Strings/SREAgentResources';
import Activities from './Activities/Activities.ReactView';
import { FeedbackDialog } from './Components/FeedbackDialog';
import { SreAgentContext } from './Contracts/Context';
import Graph from './Graph/Graph';
import IncidentManagement from './IncidentManagement/IncidentManagement';
import { useSreAgent } from './Settings/Hooks/useSreAgent';
import Settings from './Settings/Settings.ReactView';
import { useSreAgentSpaceStyles } from './Settings/Styles/SreAgentSpaceStyles';

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
}

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

const query = `traces | where timestamp > ago(1d)`;
const source = `PaasServerless.SreAgentSpace`;

const TabsListWrapper: FC = () => {
    const environmentContext = useContext(EnvironmentContext);
    const theme = useContext(ThemeContext);
    const sreAgentContext = useContext(SreAgentContext);

    const {
        incidentManagement: { isIncidentManagementConnected },
        agent: { setMode },
    } = sreAgentContext;
    const intl = useIntl();
    const location = useLocation();
    const navigate = useNavigate();

    const [isFeedbackDialogOpen, setIsFeedbackDialogOpen] = useState(false);

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
        return TabValues.Activities;
    }, [location.pathname]);

    const [appInsightsResourceId, setAppInsightsResourceId] = useState<string>();

    const styles = useSreAgentSpaceStyles();

    const { agent, agentLoaded } = useSreAgent(environmentContext.resourceId);

    const isIncidentManagementEnabled = useMemo(() => {
        return (
            agent?.properties?.incidentManagementConfiguration?.type === IncidentManagementType.PagerDuty ||
            agent?.properties?.incidentManagementConfiguration?.type === IncidentManagementType.Icm ||
            isIncidentManagementConnected
        );
    }, [agent?.properties?.incidentManagementConfiguration?.type, isIncidentManagementConnected]);

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
    }, [environmentContext.resourceId, agent?.properties.logConfiguration?.applicationInsightsConfiguration?.appId]);

    const onLogsClick = useCallback(async () => {
        if (appInsightsResourceId) {
            window.open(
                `https://portal.azure.com#view/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/query/${query}/resourceId/${encodeURIComponent(
                    appInsightsResourceId
                )}/source/${source}`,
                '_blank'
            );
        }
    }, [appInsightsResourceId]);

    const onTabSelect = useCallback(
        (_: SelectTabEvent, data: SelectTabData) => {
            if (data.value === TabValues.Activities) {
                if (!location.pathname?.startsWith('/views/activities')) {
                    navigate({ ...location, pathname: '/views/activities' });
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
            } else if (data.value === TabValues.Logs) {
                onLogsClick();
            }
        },
        [location, navigate, onLogsClick]
    );

    useEffect(() => {
        if (agent && !inStandaloneMode) {
            fetchAppInsightsId();
            setMode(agent.properties.actionConfiguration?.mode ?? '');
        }
    }, [agent, fetchAppInsightsId, setMode]);

    useEffect(() => {
        if (inStandaloneMode) {
            setMode('standalone');
        }
    }, [setMode]);

    const onSendFeedback = useCallback(() => {
        setIsFeedbackDialogOpen(true);
    }, []);

    return (
        <div>
            <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center' }}>
                <TabList selectedValue={selectedValue} onTabSelect={onTabSelect} style={getTabListStyle(theme as Theme)}>
                    <Tab id="Activities" value={TabValues.Activities}>
                        {intl.formatMessage(SreAgentTabResources.activities)}
                    </Tab>
                    <Tab id="Knowledge" value={TabValues.Graph}>
                        {intl.formatMessage(SreAgentTabResources.resourceMapping)}
                    </Tab>
                    {!inStandaloneMode && (
                        <>
                            {isIncidentManagementEnabled && (
                                <Tab id="IncidentManagement" value={TabValues.IncidentManagement} disabled={!agentLoaded}>
                                    {intl.formatMessage(SreAgentTabResources.incidentManagement)}
                                </Tab>
                            )}
                            <Tab id="Settings" value={TabValues.Settings}>
                                {intl.formatMessage(SreAgentTabResources.settings)}
                            </Tab>{' '}
                            <LineHorizontal120Regular className={styles.lineIconStyle} />
                            <Tab id="Logs" value={TabValues.Logs} disabled={!agentLoaded}>
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
                    onClick={() => onSendFeedback()}
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
            { path: 'views/incidentmanagement', element: <IncidentManagement /> },
            { path: 'views/activities/threads/:threadId', element: <Activities /> },
            { path: 'views/activities', element: <Activities /> },
            { path: '*', element: <Activities /> },
        ],
    },
]);

const SREAgentSpace: FC = () => {
    const azPortalProxy = useContext(AzPortalContext);

    const [isGrafanaUpdating, setIsGrafanaUpdating] = useState(false);
    const [deploymentId, setDeploymentId] = useState<string>('');
    const [notificationId, setNotificationId] = useState<string>('');
    const [isIncidentManagementConnected, setIsIncidentManagementConnected] = useState(false);
    const [hasFilters, setHasFilters] = useState(false);
    const [agentMode, setAgentMode] = useState<string>('');

    useEffect(() => {
        const logSiteVersion = () => {
            const version = import.meta.env.SRE_UX_VERSION;
            if (!inStandaloneMode && version) {
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
                    isIncidentManagementConnected,
                    setIsIncidentManagementConnected,
                    hasFilters,
                    setHasFilters,
                },
                agent: {
                    mode: agentMode,
                    setMode: setAgentMode,
                },
            }}
        >
            <RouterProvider router={router} />
        </SreAgentContext.Provider>
    );
};

export default SREAgentSpace;
