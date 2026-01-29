import { useTheme } from '@fluentui/react';
import { mergeClasses, MessageBar, MessageBarBody, RadioGroup, Spinner } from '@fluentui/react-components';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider } from '@xyflow/react';
import { memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import NoAccessError from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { GraphResources } from '../../Strings/SREAgentResources';
import { CopilotRadio } from '../Components/Common/CopilotRadio';
import { CUSTOM_EDGE_TYPE, GRAPH_CARD_TYPE, GraphContext } from '../Contracts/Graph';
import { useGraph } from '../Hooks/useGraph';
import { useCommonStyles } from '../Styles/Common.styles';
import { useGraphStyles } from '../Styles/Graph.styles';
import { CustomEdge } from './CustomEdge';
import { GraphCard } from './GraphCard';
import GraphGridView from './GraphGridView';
import ResourceInfo from './ResourceInfo';
import ResourceSelector from './ResourceSelector';

import '@xyflow/react/dist/style.css';

const Graph = () => {
    return (
        <ReactFlowProvider>
            <GraphContent />
        </ReactFlowProvider>
    );
};

export enum GraphView {
    Table = 'table',
    Canvas = 'canvas',
}

const GraphContent = () => {
    const {
        nodes,
        edges,
        isLoading,
        onNodesChange,
        onEdgesChange,
        selectedNode,
        hoveredNodeId,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        setSelectedNode,
        selectedAppGroupId,
        subscriptions,
        filteredAppGroups,
        selectedSubscription,
        selectedRscType,
        selectedAppGroup,
        isSubscriptionLoading,
        isAppGroupLoading,
        resourceTypeFilterOptions,
        onSelectSubscription,
        onSelectRscType,
        onSelectAppGroupDropdown,
        allKey,
        resources,
        resourceGroups,
        appGroups,
        onLoadAppGroupResources,
        refreshGraph,
        refreshCurrentAppGroup,
    } = useGraph();

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { canReadGraph } = useUserPermissions();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const { visualRoot, reactFlow, spinner, container, radioGroupContainer } = useGraphStyles();
    const commonStyles = useCommonStyles();

    const intl = useIntl();

    const theme = useTheme();
    const { resourceId } = useContext(EnvironmentContext);

    const [currentView, setCurrentView] = useState<GraphView>(GraphView.Canvas);

    const onChangeViewType = useCallback(
        (view: GraphView) => {
            setCurrentView(view);

            logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'graphViewRadioButton',
                targetFriendlyName: 'Graph view',
                valueObjectName: view,
                valueObjectFriendlyName: view,
            });
        },
        [logAmplitudeControlEvent]
    );

    const graphContextValue = useMemo(
        () => ({
            selectedNode,
            setSelectedNode,
            hoveredNodeId,
            hoverNode,
            unHoverNode,
            nodesToHighlight,
            edgesToHighlight,
            selectedAppGroupId,
            refreshGraph,
            refreshCurrentAppGroup,
        }),
        [
            selectedNode,
            setSelectedNode,
            hoveredNodeId,
            hoverNode,
            unHoverNode,
            nodesToHighlight,
            edgesToHighlight,
            selectedAppGroupId,
            refreshGraph,
            refreshCurrentAppGroup,
        ]
    );

    return (
        <GraphContext.Provider value={graphContextValue}>
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '0.25rem',
                    height: '100%',
                }}
            >
                {hasChatPermissions && canReadGraph ? (
                    !isSubscriptionLoading && subscriptions.length === 0 ? (
                        <MessageBar intent="warning" style={{ maxWidth: 500 }}>
                            <MessageBarBody>{intl.formatMessage(GraphResources.noSubscriptionsFound)}</MessageBarBody>
                        </MessageBar>
                    ) : (
                        <>
                            <div className={mergeClasses(radioGroupContainer, commonStyles.contentHeader)}>
                                <RadioGroup
                                    value={currentView}
                                    onChange={(_, data) => onChangeViewType(data.value as GraphView)}
                                    layout="horizontal"
                                >
                                    <CopilotRadio value={GraphView.Canvas} label={intl.formatMessage(GraphResources.canvasView)} />
                                    <CopilotRadio value={GraphView.Table} label={intl.formatMessage(GraphResources.tableView)} />
                                </RadioGroup>
                            </div>
                            <div className={mergeClasses(container, commonStyles.contentRootBorderAndBackground)}>
                                <div className={visualRoot}>
                                    {currentView === GraphView.Table ? (
                                        <GraphGridView
                                            resources={resources}
                                            selectedAppGroup={selectedAppGroup}
                                            resourceGroups={resourceGroups}
                                            appGroups={appGroups}
                                            onLoadAppGroupResources={onLoadAppGroupResources}
                                        />
                                    ) : (
                                        <div className={reactFlow}>
                                            {isLoading ? (
                                                <Spinner size={'large'} className={spinner} />
                                            ) : (
                                                <ReactFlow
                                                    fitView
                                                    nodeTypes={{ [GRAPH_CARD_TYPE]: GraphCard }}
                                                    edgeTypes={{ [CUSTOM_EDGE_TYPE]: CustomEdge }}
                                                    nodes={nodes}
                                                    edges={edges}
                                                    onNodesChange={onNodesChange}
                                                    onEdgesChange={onEdgesChange}
                                                    proOptions={{ hideAttribution: true }}
                                                    colorMode={theme.isInverted ? 'dark' : 'light'}
                                                    style={{ display: currentView === GraphView.Canvas ? 'block' : 'none' }}
                                                >
                                                    <Controls />
                                                    <MiniMap />
                                                    <ResourceSelector
                                                        subscriptions={subscriptions}
                                                        filteredAppGroups={filteredAppGroups}
                                                        selectedSubscription={selectedSubscription}
                                                        selectedRscType={selectedRscType}
                                                        selectedAppGroup={selectedAppGroup}
                                                        isSubscriptionLoading={isSubscriptionLoading}
                                                        isAppGroupLoading={isAppGroupLoading}
                                                        resourceTypeFilterOptions={resourceTypeFilterOptions}
                                                        onSelectSubscription={onSelectSubscription}
                                                        onSelectRscType={onSelectRscType}
                                                        onSelectAppGroupDropdown={onSelectAppGroupDropdown}
                                                        allKey={allKey}
                                                    />
                                                </ReactFlow>
                                            )}
                                        </div>
                                    )}
                                </div>
                                {currentView === GraphView.Canvas && <ResourceInfo />}
                            </div>
                        </>
                    )
                ) : (
                    <NoAccessError requiredPermission={PermissionActions.AgentGraphRead} resourceId={resourceId || 'unknown'} />
                )}
            </div>
        </GraphContext.Provider>
    );
};

export default memo(Graph);
