import { ThemeContext } from '@fluentui/react';
import { SelectTabData, SelectTabEvent, Tab, TabList } from '@fluentui/react-components';
import { LineHorizontal120Regular, Open16Regular } from '@fluentui/react-icons';
import type { Theme } from '@fluentui/theme';
import { FC, useCallback, useContext, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
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

const SREAgentSpace: FC = () => {
    const environmentContext = useContext(EnvironmentContext);
    const theme = useContext(ThemeContext);
    const intl = useIntl();

    const [selectedValue, setSelectedValue] = useState<TabValues>(TabValues.Activities);
    const [initialThreadId, setInitialThreadId] = useState<string | null | undefined>(null);
    const [workspaceId, setWorkspaceId] = useState<string>();

    const styles = useSreAgentSpaceStyles();

    const { agent, agentLoaded } = useSreAgent(environmentContext.resourceId);

    const onTabSelect = useCallback((_: SelectTabEvent, data: SelectTabData) => {
        setInitialThreadId(null);
        setSelectedValue(data.value as TabValues);
    }, []);

    const transferDataToActivities = useCallback((threadId: string | null | undefined) => {
        setInitialThreadId(threadId);
        setSelectedValue(TabValues.Activities);
    }, []);

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
    }, [agent?.properties?.logConfiguration?.logAnalyticsConfiguration?.workspaceId]);

    const onLogsClick = useCallback(async () => {
        if (workspaceId) {
            window.open(
                `https://portal.azure.com#view/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/query/${query}/resourceId/${encodeURIComponent(
                    workspaceId
                )}/source/${source}`,
                '_blank'
            );
        }
        setSelectedValue(TabValues.Activities);
    }, [workspaceId, agent?.properties?.logConfiguration?.logAnalyticsConfiguration?.workspaceId]);

    useEffect(() => {
        if (selectedValue === TabValues.Logs) {
            onLogsClick();
        }
    }, [selectedValue, onLogsClick]);

    useEffect(() => {
        if (agent && !inStandaloneMode) {
            fetchWorkspaceId();
        }
    }, [agent, fetchWorkspaceId]);

    return (
        <div>
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
            <div>
                {selectedValue === TabValues.Activities && <Activities initialThreadId={initialThreadId} />}
                {selectedValue === TabValues.Graph && <Graph transferDataToActivities={transferDataToActivities} />}
                {selectedValue === TabValues.Settings && <Settings />}
            </div>
        </div>
    );
};

export default SREAgentSpace;
