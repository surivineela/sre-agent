import { Spinner } from '@fluentui/react-components';
import { useTheme } from '@fluentui/react/lib/Theme';
import { Controls, ReactFlow, useReactFlow } from '@xyflow/react';
import { memo } from 'react';
import { TreeNodeType } from '../../Common/Contracts/DataPlane/AgentTask';
import AgentTaskGraphFlowHypothesisNode from '../Components/AgentTaskGraphFlowHypothesisNode';
import AgentTaskGraphFlowPhaseNode from '../Components/AgentTaskGraphFlowPhaseNode';
import { IAgentTaskGraphProps } from '../Contracts/Activities';
import { useAgentTaskGraphFlow } from '../Hooks/useAgentTaskGraphFlow';

import '@xyflow/react/dist/style.css';

const AgentTaskGraph = (props: IAgentTaskGraphProps) => {
    return props.isLoading ? <Spinner size="large" style={{ marginTop: '20%' }} /> : <AgentTaskGraphFlow {...props} />;
};

const AgentTaskGraphFlow = (props: IAgentTaskGraphProps) => {
    const { nodes, edges, onNodesChange, onEdgesChange } = useAgentTaskGraphFlow(props);

    const theme = useTheme();
    const { fitView } = useReactFlow();

    return (
        <ReactFlow
            style={{ width: '100%', height: '100%', position: 'relative' }}
            nodes={nodes}
            edges={edges}
            onNodesChange={onNodesChange}
            onEdgesChange={onEdgesChange}
            onInit={() => {
                setTimeout(() => fitView({ minZoom: 0.5, maxZoom: 1.2, padding: 50, duration: 100, interpolate: 'smooth' }), 200);
            }}
            attributionPosition="bottom-left"
            minZoom={0.3}
            maxZoom={1.5}
            nodeTypes={{
                [`${TreeNodeType.Phase}`]: AgentTaskGraphFlowPhaseNode,
                [`${TreeNodeType.Hypothesis}`]: AgentTaskGraphFlowHypothesisNode,
            }}
            proOptions={{ hideAttribution: true }}
            colorMode={theme.isInverted ? 'dark' : 'light'}
        >
            <Controls style={{ position: 'absolute', bottom: 50, left: 10 }} />
        </ReactFlow>
    );
};

export default memo(AgentTaskGraph);
