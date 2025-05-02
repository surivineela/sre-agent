import { ThemeContext } from '@fluentui/react';
import { SelectTabData, SelectTabEvent, Tab, TabList } from '@fluentui/react-components';
import { LineHorizontal120Regular, Open16Regular } from '@fluentui/react-icons';
import type { Theme } from '@fluentui/theme';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { BrowserRouter, Route, Routes, useLocation, useNavigate } from 'react-router';
import AzPortalProxy from '../Common/AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../Common/AzPortalProxy/Providers/StartupInfoContext';
import { WorkspaceClient } from '../Common/Clients/WorkspaceClient';
import { ArmResourceDescriptor } from '../Common/Helpers/ResourceDescriptors';
import { SreAgentTabResources } from '../Strings/SREAgentResources';
import Activities from './Activities/Activities.ReactView';
import Graph from './Graph/Graph';
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
}

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

const query = `ContainerAppConsoleLogs_CL | where TimeGenerated > ago(1d)`;
const source = `PaasServerless.SreAgentSpace`;

const TabsListWrapper: FC = () => {
    const environmentContext = useContext(EnvironmentContext);
    const theme = useContext(ThemeContext);
    const intl = useIntl();
    const location = useLocation();
    const navigate = useNavigate();

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
        return TabValues.Activities;
    }, [location.pathname]);

    const [workspaceId, setWorkspaceId] = useState<string>();

    const styles = useSreAgentSpaceStyles();

    const { agent, agentLoaded } = useSreAgent(environmentContext.resourceId);

    const fetchWorkspaceId = useCallback(async () => {
        const { subscription, resourceGroup } = new ArmResourceDescriptor(environmentContext.resourceId);
        const response = await WorkspaceClient.getWorkspaceFromId(
            [subscription],
            resourceGroup,
            agent?.properties.logConfiguration?.logAnalyticsConfiguration?.workspaceId || ''
        );
        if (response) {
            setWorkspaceId(response);
        }
    }, [environmentContext.resourceId, agent?.properties?.logConfiguration?.logAnalyticsConfiguration?.workspaceId]);

    const onLogsClick = useCallback(async () => {
        if (workspaceId) {
            window.open(
                `https://portal.azure.com#view/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/query/${query}/resourceId/${encodeURIComponent(
                    workspaceId
                )}/source/${source}`,
                '_blank'
            );
        }
    }, [workspaceId, agent?.properties?.logConfiguration?.logAnalyticsConfiguration?.workspaceId]);

    const onTabSelect = useCallback(
        (_: SelectTabEvent, data: SelectTabData) => {
            if (data.value === TabValues.Activities) {
                if (!location.pathname?.startsWith('/views/activities')) {
                    navigate({ ...location, pathname: '/views/activities' });
                }
            } else if (data.value === TabValues.Graph) {
                navigate({ ...location, pathname: '/views/resourcegraph' });
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
            fetchWorkspaceId();
        }
    }, [agent, fetchWorkspaceId]);

    return (
        <TabList selectedValue={selectedValue} onTabSelect={onTabSelect} style={getTabListStyle(theme as Theme)}>
            <Tab id="Activities" value={TabValues.Activities}>
                {intl.formatMessage(SreAgentTabResources.activities)}
            </Tab>
            <Tab id="Knowledge" value={TabValues.Graph}>
                {intl.formatMessage(SreAgentTabResources.managedResources)}
            </Tab>
            {!inStandaloneMode && (
                <>
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
    );
};

const SREAgentSpace: FC = () => {
    return (
        <div>
            <BrowserRouter basename="/static">
                <TabsListWrapper />
                <Routes>
                    <Route path="/views/settings/:menuItem" element={<Settings />} />
                    <Route path="/views/settings" element={<Settings />} />
                    <Route path="/views/resourcegraph" element={<Graph />} />
                    <Route path="/views/activities/threads/:threadId" element={<Activities />} />
                    <Route path="/views/activities" element={<Activities />} />
                    <Route path="*" element={<Activities />} />
                </Routes>
            </BrowserRouter>
        </div>
    );
};

export default SREAgentSpace;
