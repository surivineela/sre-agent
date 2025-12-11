import { useTheme } from '@fluentui/react';
import { RadioGroup, Spinner, webDarkTheme, webLightTheme } from '@fluentui/react-components';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider } from '@xyflow/react';
import { memo, useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { GraphResources } from '../../Strings/SREAgentResources';
import { CUSTOM_EDGE_TYPE, GRAPH_CARD_TYPE, GraphContext } from '../Contracts/Graph';
import { useGraph } from '../Hooks/useGraph';
import { useGraphStyles } from '../Styles/Graph.styles';
import { CustomEdge } from './CustomEdge';
import { GraphCard } from './GraphCard';
import ResourceInfo from './ResourceInfo';
import ResourceSelector from './ResourceSelector';

import { CopilotProvider, CopilotTheme, tokens } from '@fluentui-copilot/react-copilot';
import '@xyflow/react/dist/style.css';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import NoAccessError from '../../Common/Components/NoAccessError';
import { PermissionActions } from '../../Common/Contracts/Azure/Permission';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
import { Radio } from '../Components/Common/Radio';
import GraphGridView from './GraphGridView';

const Graph = () => {
    return (
        <ReactFlowProvider>
            <GraphContent />
        </ReactFlowProvider>
    );
};

export enum GraphView {
    Grid = 'grid',
    Visual = 'visual',
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
    } = useGraph();

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);
    const { canReadGraph } = useUserPermissions();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const { visualRoot, reactFlow, spinner, container, radioGroupContainer } = useGraphStyles();
    const intl = useIntl();

    const theme = useTheme();
    const { resourceId } = useContext(EnvironmentContext);

    const [currentView, setCurrentView] = useState<GraphView>(GraphView.Visual);

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

    return (
        <GraphContext.Provider
            value={{
                selectedNode,
                setSelectedNode,
                hoveredNodeId,
                hoverNode,
                unHoverNode,
                nodesToHighlight,
                edgesToHighlight,
                selectedAppGroupId,
            }}
        >
            <CopilotProvider
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    height: 'calc(100vh - 60px)',
                    padding: '10px ',
                    borderTop: '1px solid rgba(204, 204, 204, 0.8)',
                    backgroundColor: tokens.colorNeutralBackground3,
                    gap: '0.25rem',
                }}
                {...CopilotTheme}
                mode={'canvas'}
                theme={theme.isInverted ? webDarkTheme : webLightTheme}
            >
                {hasChatPermissions && canReadGraph ? (
                    <>
                        <div className={radioGroupContainer}>
                            <RadioGroup
                                value={currentView}
                                onChange={(_, data) => onChangeViewType(data.value as GraphView)}
                                layout="horizontal"
                            >
                                <Radio value={GraphView.Visual} label={intl.formatMessage(GraphResources.visualView)} />
                                <Radio value={GraphView.Grid} label={intl.formatMessage(GraphResources.gridView)} />
                            </RadioGroup>
                        </div>
                        <div className={container}>
                            <div className={visualRoot}>
                                {currentView === GraphView.Grid ? (
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
                                                style={{ display: currentView === GraphView.Visual ? 'block' : 'none' }}
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
                            {currentView === GraphView.Visual && <ResourceInfo />}
                        </div>
                    </>
                ) : (
                    <NoAccessError requiredPermission={PermissionActions.AgentGraphRead} resourceId={resourceId || 'unknown'} />
                )}
            </CopilotProvider>
        </GraphContext.Provider>
    );
};

export default memo(Graph);
