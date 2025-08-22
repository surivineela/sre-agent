import { DrawerBody, Spinner } from '@fluentui/react-components';
import { useTheme } from '@fluentui/react/lib/Theme';
import { Controls, OnEdgesChange, OnNodesChange, ReactFlow } from '@xyflow/react';
import { forwardRef, memo, useImperativeHandle } from 'react';
import { TreeNodeType } from '../../../Common/Contracts/DataPlane/AgentTask';
import ChildrenEdge from '../../Components/AgentTask/ChildrenEdge';
import ConclusionNode from '../../Components/AgentTask/ConclusionNode';
import GroupNode from '../../Components/AgentTask/GroupNode';
import HypothesisNode from '../../Components/AgentTask/HypothesisNode';
import HypothesisRootGroupNode from '../../Components/AgentTask/HypothesisRootGroupNode';
import InitialInvestigationNode from '../../Components/AgentTask/InitialInvestigationNode';
import ParentEdge from '../../Components/AgentTask/ParentEdge';
import {
    AgentTaskGraphHandle,
    GraphFlowEdge,
    GraphFlowNode,
    IAgentTaskGraphProps,
    InvestigationGraphFlowEdgeType,
} from '../../Contracts/Activities';
import { AgentTaskGraphContext } from '../../Contracts/Context';
import { useAgentTaskGraphFlow } from '../../Hooks/useAgentTaskGraphFlow';
import AgentTaskDetailsPanel from './AgentTaskDetailsPanel';

import '@xyflow/react/dist/style.css';

const AgentTaskGraph = forwardRef<AgentTaskGraphHandle, IAgentTaskGraphProps>((props, ref) => {
    const {
        renderKey,
        centerGraph,
        selectNode,
        selectedNodeId,
        selectedNode,
        isDetailsPanelOpen,
        closeDetailsPanel,
        reactFlowWrapperRef,
        ...agentTaskGraphFlowProps
    } = useAgentTaskGraphFlow(props);

    useImperativeHandle(ref, () => ({
        centerGraph,
    }));

    return (
        <AgentTaskGraphContext.Provider value={{ selectNode, selectedNodeId }}>
            {props.isLoading ? (
                <DrawerBody>
                    <Spinner size="large" style={{ marginTop: '300px' }} />
                </DrawerBody>
            ) : (
                <div ref={reactFlowWrapperRef} key={renderKey} style={{ width: '100%', height: '100%' }}>
                    <AgentTaskGraphFlow {...agentTaskGraphFlowProps} />
                </div>
            )}
            <AgentTaskDetailsPanel isOpen={isDetailsPanelOpen} onClose={closeDetailsPanel} node={selectedNode} />
        </AgentTaskGraphContext.Provider>
    );
});

const AgentTaskGraphFlow = ({
    nodes,
    edges,
    onNodesChange,
    onEdgesChange,
    minZoom,
}: {
    nodes: GraphFlowNode[];
    edges: GraphFlowEdge[];
    onNodesChange: OnNodesChange<GraphFlowNode>;
    onEdgesChange: OnEdgesChange<GraphFlowEdge>;
    minZoom?: number;
}) => {
    const theme = useTheme();

    return (
        <ReactFlow
            style={{ width: '100%', height: '100%', position: 'relative' }}
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            attributionPosition="bottom-left"
            nodeTypes={{
                [TreeNodeType.HypothesisRootGroup]: HypothesisRootGroupNode,
                [TreeNodeType.NodeGroup]: GroupNode,
                [TreeNodeType.InitialInvestigation]: InitialInvestigationNode,
                [TreeNodeType.Hypothesis]: HypothesisNode,
                [TreeNodeType.Conclusion]: ConclusionNode,
            }}
            edgeTypes={{
                [InvestigationGraphFlowEdgeType.Parents]: ParentEdge,
                [InvestigationGraphFlowEdgeType.Children]: ChildrenEdge,
            }}
            nodesDraggable={false}
            proOptions={{ hideAttribution: true }}
            colorMode={theme.isInverted ? 'dark' : 'light'}
            fitView
            minZoom={minZoom}
            fitViewOptions={{ padding: 50 }}
        >
            <Controls />
        </ReactFlow>
    );
};

export default memo(AgentTaskGraph);
