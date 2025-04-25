import { useTheme } from '@fluentui/react';
import { Spinner } from '@fluentui/react-components';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider } from '@xyflow/react';
import { memo } from 'react';
import { CUSTOM_EDGE_TYPE, GRAPH_CARD_TYPE, GraphContext } from '../Contracts/Graph';
import { useGraph } from '../Hooks/useGraph';
import { useGraphStyles } from '../Styles/Graph.styles';
import { CustomEdge } from './CustomEdge';
import { GraphCard } from './GraphCard';
import Panel from './Panel';
import ResourceSelector from './ResourceSelector';

import '@xyflow/react/dist/style.css';

interface IGraphProps {
    transferDataToActivities: (threadId?: string | null) => void;
}

const Graph = (props: IGraphProps) => {
    return (
        <ReactFlowProvider>
            <GraphContent {...props} />
        </ReactFlowProvider>
    );
};

const GraphContent = (props: IGraphProps) => {
    const {
        nodes,
        edges,
        isLoading,
        onNodesChange,
        onEdgesChange,
        openPanel,
        closePanel,
        isPanelOpen,
        selectedNode,
        onAppGroupUpdate,
        hoverNode,
        unHoverNode,
        nodesToHightlight,
        edgesToHightlight,
    } = useGraph();

    const { root, reactFlow, spinner } = useGraphStyles();

    const theme = useTheme();

    return (
        <GraphContext.Provider
            value={{
                openPanel,
                closePanel,
                isPanelOpen,
                selectedNode,
                hoverNode,
                unHoverNode,
                nodesToHightlight,
                edgesToHightlight,
            }}
        >
            <div className={root}>
                <ResourceSelector onAppGroupUpdate={onAppGroupUpdate} />
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
                        >
                            <Controls />
                            <MiniMap />
                        </ReactFlow>
                    )}
                </div>
                <Panel {...props} />
            </div>
        </GraphContext.Provider>
    );
};

export default memo(Graph);
