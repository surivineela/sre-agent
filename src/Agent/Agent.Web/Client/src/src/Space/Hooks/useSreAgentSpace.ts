import { Theme } from '@fluentui/react';
import { SelectTabData, SelectTabEvent } from '@fluentui/react-components';
import { useCallback, useEffect, useMemo, useState } from 'react';
import AzPortalProxy from '../../Common/AzPortalProxy/AzPortalProxy';
import { WorkspaceClient } from '../../Common/Clients/WorkspaceClient';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { useSreAgent } from '../Settings/Hooks/useSreAgent';

const query = `ContainerAppConsoleLogs_CL | where TimeGenerated > ago(1d)`;
const source = `PaasServerless.SreAgentSpace`;

export const getTabListStyle = (theme: Theme) => {
    return {
        backgroundColor: theme.semanticColors.bodyBackground,
    };
};

export enum TabValues {
    Activities = 'activities',
    Settings = 'settings',
    Graph = 'graph',
    Logs = 'logs',
}

export const inStandaloneMode = AzPortalProxy.inStandaloneMode;

export function useSreAgentSpace(resourceId: string) {
    const [selectedValue, setSelectedValue] = useState<TabValues>(TabValues.Activities);
    const [initialThreadId, setInitialThreadId] = useState<string | null | undefined>(null);
    const [workspaceId, setWorkspaceId] = useState<string>('');

    const { subscription, resourceGroup } = useMemo(() => new ArmResourceDescriptor(resourceId), [resourceId]);

    const { agent, agentLoaded } = useSreAgent(resourceId);

    const isLogsItemDisabled = useMemo(() => !agentLoaded || !workspaceId, [agentLoaded, workspaceId]);

    const onTabSelect = useCallback((_: SelectTabEvent, data: SelectTabData) => {
        setInitialThreadId(null);
        setSelectedValue(data.value as TabValues);
    }, []);

    const transferDataToActivities = useCallback((threadId: string | null | undefined) => {
        setInitialThreadId(threadId);
        setSelectedValue(TabValues.Activities);
    }, []);

    const fetchWorkspaceId = useCallback(async () => {
        const response = await WorkspaceClient.getWorkspaceFromId(
            [subscription],
            resourceGroup,
            agent?.properties.logConfiguration?.logAnalyticsConfiguration?.workspaceId || ''
        );
        if (response) {
            setWorkspaceId(response);
        }
    }, [subscription, resourceGroup, agent?.properties.logConfiguration?.logAnalyticsConfiguration?.workspaceId]);

    const onLogsClick = useCallback(() => {
        window.open(
            `https://portal.azure.com#view/Microsoft_OperationsManagementSuite_Workspace/Logs.ReactView/query/${query}/resourceId/${encodeURIComponent(
                workspaceId
            )}/source/${source}`,
            '_blank'
        );
        setSelectedValue(TabValues.Activities);
    }, [workspaceId]);

    useEffect(() => {
        if (agent) {
            fetchWorkspaceId();
        }
    }, [agent, fetchWorkspaceId]);

    useEffect(() => {
        if (selectedValue === TabValues.Logs) {
            onLogsClick();
        }
    }, [selectedValue, onLogsClick]);

    return {
        selectedValue,
        initialThreadId,
        isLogsItemDisabled,
        transferDataToActivities,
        onTabSelect,
        onLogsClick,
    };
}
