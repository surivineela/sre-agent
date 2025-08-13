import { Spinner } from '@fluentui/react-components';
import { useTheme } from '@fluentui/react/lib/Theme';
import { Controls, ReactFlow } from '@xyflow/react';
import { memo } from 'react';
import { TreeNodeType } from '../../Common/Contracts/DataPlane/AgentTask';
import AgentTaskGraphFlowGroupNode from '../Components/AgentTaskGraphFlowGroupNode';
import AgentTaskGraphFlowHypothesisNode from '../Components/AgentTaskGraphFlowHypothesisNode';
import AgentTaskGraphFlowPhaseNode from '../Components/AgentTaskGraphFlowPhaseNode';
import { IAgentTaskGraphProps } from '../Contracts/Activities';
import { useAgentTaskGraphFlow } from '../Hooks/useAgentTaskGraphFlow';

import '@xyflow/react/dist/style.css';

const AgentTaskGraph = (props: IAgentTaskGraphProps) => {
    return props.isLoading ? <Spinner size="large" style={{ marginTop: '30%' }} /> : <AgentTaskGraphFlow {...props} />;
};

const AgentTaskGraphFlow = (props: IAgentTaskGraphProps) => {
    const { nodes, edges, onNodesChange, onEdgesChange, centerGraph } = useAgentTaskGraphFlow(props);

    const theme = useTheme();

    return (
        <ReactFlow
            style={{ width: '100%', height: '100%', position: 'relative' }}
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onInit={() => centerGraph(true)}
            attributionPosition="bottom-left"
            nodeTypes={{
                [TreeNodeType.Group]: AgentTaskGraphFlowGroupNode,
                [TreeNodeType.Phase]: AgentTaskGraphFlowPhaseNode,
                [TreeNodeType.Hypothesis]: AgentTaskGraphFlowHypothesisNode,
            }}
            minZoom={0.5}
            maxZoom={1.5}
            fitView
            fitViewOptions={{
                padding: 50,
                duration: 50,
                interpolate: 'smooth',
            }}
            proOptions={{ hideAttribution: true }}
            colorMode={theme.isInverted ? 'dark' : 'light'}
        >
            <Controls style={{ position: 'absolute', bottom: 50, left: 10 }} />
        </ReactFlow>
    );
};

export default memo(AgentTaskGraph);
