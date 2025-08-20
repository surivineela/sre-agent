import { Spinner } from '@fluentui/react-components';
import { useTheme } from '@fluentui/react/lib/Theme';
import { Controls, OnEdgesChange, OnNodesChange, ReactFlow } from '@xyflow/react';
import { memo } from 'react';
import { TreeNodeType } from '../../../Common/Contracts/DataPlane/AgentTask';
import ChildrenEdge from '../../Components/AgentTask/ChildrenEdge';
import ConclusionNode from '../../Components/AgentTask/ConclusionNode';
import GroupNode from '../../Components/AgentTask/GroupNode';
import HypothesisNode from '../../Components/AgentTask/HypothesisNode';
import HypothesisRootGroupNode from '../../Components/AgentTask/HypothesisRootGroupNode';
import InitialInvestigationNode from '../../Components/AgentTask/InitialInvestigationNode';
import ParentEdge from '../../Components/AgentTask/ParentEdge';
import { GraphFlowEdge, GraphFlowNode, IAgentTaskGraphProps, InvestigationGraphFlowEdgeType } from '../../Contracts/Activities';
import { AgentTaskGraphContext } from '../../Contracts/Context';
import { useAgentTaskGraphFlow } from '../../Hooks/useAgentTaskGraphFlow';
import AgentTaskDetailsPanel from './AgentTaskDetailsPanel';

import '@xyflow/react/dist/style.css';

const AgentTaskGraph = (props: IAgentTaskGraphProps) => {
    const {
        renderKey,
        containerRef,
        selectNode,
        selectedNodeId,
        selectedNode,
        isDetailsPanelOpen,
        closeDetailsPanel,
        ...agentTaskGraphFlowProps
    } = useAgentTaskGraphFlow(props);

    return (
        <AgentTaskGraphContext.Provider value={{ selectNode, selectedNodeId }}>
            {props.isLoading ? (
                <Spinner size="large" style={{ marginTop: '300px' }} />
            ) : (
                <div ref={containerRef} key={renderKey} style={{ width: '100%', height: '100%' }}>
                    <AgentTaskGraphFlow {...agentTaskGraphFlowProps} />
                </div>
            )}
            <AgentTaskDetailsPanel isOpen={isDetailsPanelOpen} onClose={closeDetailsPanel} node={selectedNode} />
        </AgentTaskGraphContext.Provider>
    );
};

const AgentTaskGraphFlow = ({
    nodes,
    edges,
    onNodesChange,
    onEdgesChange,
}: {
    nodes: GraphFlowNode[];
    edges: GraphFlowEdge[];
    onNodesChange: OnNodesChange<GraphFlowNode>;
    onEdgesChange: OnEdgesChange<GraphFlowEdge>;
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
            fitViewOptions={{ padding: 50 }}
        >
            <Controls style={{ position: 'absolute', bottom: 50, left: 10 }} />
        </ReactFlow>
    );
};

export default memo(AgentTaskGraph);
