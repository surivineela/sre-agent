import { Spinner } from '@fluentui/react-components';
import { useTheme } from '@fluentui/react/lib/Theme';
import { Controls, OnEdgesChange, OnNodesChange, ReactFlow } from '@xyflow/react';
import { memo } from 'react';
import { TreeNodeType } from '../../Common/Contracts/DataPlane/AgentTask';
import AgentTaskGraphFlowConclusionNode from '../Components/AgentTask/AgentTaskGraphFlowConclusionNode';
import AgentTaskGraphFlowGroupNode from '../Components/AgentTask/AgentTaskGraphFlowGroupNode';
import AgentTaskGraphFlowHypothesisNode from '../Components/AgentTask/AgentTaskGraphFlowHypothesisNode';
import AgentTaskGraphFlowInitialInvestigationNode from '../Components/AgentTask/AgentTaskGraphFlowInitialInvestigationNode';
import { GraphFlowEdge, GraphFlowNode, IAgentTaskGraphProps } from '../Contracts/Activities';
import { useAgentTaskGraphFlow } from '../Hooks/useAgentTaskGraphFlow';

import '@xyflow/react/dist/style.css';

const AgentTaskGraph = (props: IAgentTaskGraphProps) => {
    const { renderKey, containerRef, ...agentTaskGraphFlowProps } = useAgentTaskGraphFlow(props);

    return props.isLoading ? (
        <Spinner size="large" style={{ marginTop: '300px' }} />
    ) : (
        <div ref={containerRef} key={renderKey} style={{ width: '100%', height: '100%' }}>
            <AgentTaskGraphFlow {...agentTaskGraphFlowProps} />;
        </div>
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
                [TreeNodeType.Group]: AgentTaskGraphFlowGroupNode,
                [TreeNodeType.InitialInvestigation]: AgentTaskGraphFlowInitialInvestigationNode,
                [TreeNodeType.Hypothesis]: AgentTaskGraphFlowHypothesisNode,
                [TreeNodeType.Conclusion]: AgentTaskGraphFlowConclusionNode,
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
