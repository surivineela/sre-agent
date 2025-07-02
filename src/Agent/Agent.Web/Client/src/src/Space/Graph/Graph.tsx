import { useTheme } from '@fluentui/react';
import { MessageBar, MessageBarBody, Spinner } from '@fluentui/react-components';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider } from '@xyflow/react';
import { memo, useContext } from 'react';
import { CUSTOM_EDGE_TYPE, GRAPH_CARD_TYPE, GraphContext } from '../Contracts/Graph';
import { useGraph } from '../Hooks/useGraph';
import { useGraphStyles } from '../Styles/Graph.styles';
import { CustomEdge } from './CustomEdge';
import { GraphCard } from './GraphCard';
import ResourceInfo from './ResourceInfo';
import ResourceSelector from './ResourceSelector';

import '@xyflow/react/dist/style.css';
import { useIntl } from 'react-intl';
import { KnowledgeGraphBuildStatusContext } from '../../Common/Providers/KnowledgeGraphBuildStatusProvider';
import { ActivitiesResources } from '../../Strings/SREAgentResources';

const Graph = () => {
    return (
        <ReactFlowProvider>
            <GraphContent />
        </ReactFlowProvider>
    );
};

const GraphContent = () => {
    const {
        nodes,
        edges,
        isLoading,
        onNodesChange,
        onEdgesChange,
        selectedNode,
        hoveredNodeId,
        onAppGroupUpdate,
        hoverNode,
        unHoverNode,
        nodesToHighlight,
        edgesToHighlight,
        setSelectedNode,
        selectedAppGroupId,
    } = useGraph();

    const { hasChatPermissions } = useContext(KnowledgeGraphBuildStatusContext);

    const { root, reactFlow, spinner, messageBar } = useGraphStyles();

    const intl = useIntl();

    const theme = useTheme();

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
            <div className={root}>
                <ResourceSelector onAppGroupUpdate={onAppGroupUpdate} />
                <div className={reactFlow}>
                    {hasChatPermissions ? (
                        <>
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
                                >
                                    <Controls />
                                    <MiniMap />
                                </ReactFlow>
                            )}
                        </>
                    ) : (
                        <MessageBar intent="warning" className={messageBar}>
                            <MessageBarBody>{intl.formatMessage(ActivitiesResources.insufficientChatPermissions)}</MessageBarBody>
                        </MessageBar>
                    )}
                </div>
                <ResourceInfo />
            </div>
        </GraphContext.Provider>
    );
};

export default memo(Graph);
