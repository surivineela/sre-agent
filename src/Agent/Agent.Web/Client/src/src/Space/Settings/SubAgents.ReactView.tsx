import { Link, tokens } from '@fluentui/react-components';
import { CheckboxVisibility, ConstrainMode, DetailsListLayoutMode, IColumn, SelectionMode } from '@fluentui/react/lib/DetailsList';
import { ShimmeredDetailsList } from '@fluentui/react/lib/ShimmeredDetailsList';
import { FC, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { SubAgentClient } from '../../Common/Clients/SubAgentClient';
import { SubAgent } from '../../Common/Contracts/SubAgent';
import { SettingsTabResources, SreAgentResources, SubAgentsResources } from '../../Strings/SREAgentResources';
import { CreateSubAgentDialog } from './CreateSubAgentDialog';
import { useSreAgent } from './Hooks/useSreAgent';
import { useSubAgents } from './Hooks/useSubAgents';
import { useSettingsStyles } from './Styles/Settings.styles';
import SubAgentsToolbar from './SubAgentsToolbar';

enum SubAgentsColumnKey {
    name = 'id',
    runHistory = 'runHistory',
}

const SubAgents: FC = () => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const { resourceId } = useContext(EnvironmentContext);
    const portalContext = useContext(AzPortalContext);
    const { agent, agentLoading, refresh: refreshAgent } = useSreAgent(resourceId);
    const { subAgents, subAgentsLoading, initialize: initializeSubAgents, refresh: refreshSubAgents } = useSubAgents(resourceId);

    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isOperationInProgress, setIsOperationInProgress] = useState(false);
    const [isRefreshing, setIsRefreshing] = useState(false);

    const agentEndpoint = useMemo(() => {
        return agent?.properties?.agentEndpoint || '';
    }, [agent?.properties?.agentEndpoint]);

    useEffect(() => {
        if (agentEndpoint) {
            initializeSubAgents(agentEndpoint);
        }
    }, [agentEndpoint, initializeSubAgents]);

    const handleRefresh = useCallback(async () => {
        setIsRefreshing(true);
        await refreshAgent();
        await refreshSubAgents(agentEndpoint);
        setIsRefreshing(false);
    }, [refreshAgent, refreshSubAgents, agentEndpoint]);

    const openLogicAppDesigner = useCallback(
        (subAgentInfo: SubAgent) => {
            portalContext.openBlade({
                extension: 'Microsoft_Azure_EMA',
                detailBlade: 'SreAgentDesignerEditor.ReactView',
                detailBladeInputs: {
                    id: subAgentInfo.logicAppWorkflowId,
                    sreAgentId: resourceId,
                },
            });
        },
        [portalContext, resourceId]
    );

    const handleNewSubAgent = useCallback(() => {
        setIsDialogOpen(true);
    }, []);

    const createSubAgent = useCallback(
        async ({ name }: { name: string }) => {
            if (!agent) {
                return;
            }

            setIsOperationInProgress(true);
            const notificationId = portalContext.startNotification(
                intl.formatMessage(SubAgentsResources.creatingSubAgent),
                intl.formatMessage(SubAgentsResources.creatingSubAgentDescription, { name })
            );

            const response = await SubAgentClient.getInstance(agentEndpoint).createSubAgent({ name });
            if (response.isSuccessful) {
                handleRefresh();
                setIsDialogOpen(false);
                portalContext.stopNotification(notificationId, true, intl.formatMessage(SubAgentsResources.subAgentCreated, { name }));
            } else {
                portalContext.log({
                    action: 'createDataConnector',
                    actionModifier: 'failed',
                    resourceId,
                    logLevel: 'error',
                    data: {
                        message: `Failed to create data connector: ${response.error}`,
                    },
                });

                const errorMessage = response.error;
                portalContext.stopNotification(
                    notificationId,
                    false,
                    errorMessage
                        ? intl.formatMessage(SubAgentsResources.createSubAgentWithMessageFailed, { error: errorMessage })
                        : intl.formatMessage(SubAgentsResources.createSubAgentFailed)
                );
            }
            setIsOperationInProgress(false);
        },
        [agent, portalContext, intl, agentEndpoint, handleRefresh, resourceId]
    );

    const columns = useMemo<IColumn[]>(() => {
        return [
            {
                key: SubAgentsColumnKey.name,
                name: intl.formatMessage(SreAgentResources.name),
                fieldName: SubAgentsColumnKey.name,
                minWidth: 150,
                maxWidth: 200,
                isResizable: true,
                onRender: (item: SubAgent) => {
                    return <Link onClick={() => openLogicAppDesigner(item)}>{item.agentName}</Link>;
                },
            },
            {
                key: SubAgentsColumnKey.runHistory,
                name: intl.formatMessage(SubAgentsResources.runHistory),
                fieldName: SubAgentsColumnKey.runHistory,
                minWidth: 120,
                maxWidth: 150,
                isResizable: true,
            },
        ];
    }, [intl, openLogicAppDesigner]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.subAgents)}</div>
            <div style={styles.accessControlSettingsContainer}>
                <SubAgentsToolbar
                    onRefreshClick={handleRefresh}
                    onNewSubAgentClick={handleNewSubAgent}
                    isOperationInProgress={isOperationInProgress || isRefreshing}
                />
                <div data-is-scrollable="true">
                    <ShimmeredDetailsList
                        compact={true}
                        selectionMode={SelectionMode.none}
                        columns={columns}
                        constrainMode={ConstrainMode.horizontalConstrained}
                        items={subAgents || []}
                        layoutMode={DetailsListLayoutMode.justified}
                        enableShimmer={agentLoading || subAgentsLoading || isRefreshing}
                        checkboxVisibility={CheckboxVisibility.hidden}
                    />
                    {!subAgentsLoading && subAgents?.length === 0 && (
                        <div style={{ padding: '20px', textAlign: 'center', color: tokens.colorNeutralForeground3 }}>
                            {intl.formatMessage(SubAgentsResources.noSubAgents)}
                        </div>
                    )}
                </div>
            </div>

            <CreateSubAgentDialog
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                createSubAgent={createSubAgent}
                isOperationInProgress={isOperationInProgress}
                existingSubAgents={subAgents}
            />
        </>
    );
};

export default SubAgents;
