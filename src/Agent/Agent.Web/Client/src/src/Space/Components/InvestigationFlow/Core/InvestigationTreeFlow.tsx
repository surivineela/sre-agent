import { useTheme } from '@fluentui/react';
import { makeStyles, Text } from '@fluentui/react-components';
import { CircleRegular } from '@fluentui/react-icons';
import { Controls, MiniMap, ReactFlow, ReactFlowProvider } from '@xyflow/react';
import React, { useContext, useEffect, useMemo, useState } from 'react';
import { InvestigationTreeContext } from '../../../Contexts/InvestigationTreeContext';
import { useInvestigationFlow } from '../../../Hooks/InvestigationFlow/useInvestigationFlow';
import { HypothesisNode } from '../../InvestigationFlow/HypothesisNode';
import { InvestigationEdge } from '../../InvestigationFlow/InvestigationEdge';
import { PhaseNode } from '../../InvestigationFlow/PhaseNode';

import '@xyflow/react/dist/style.css';

const useFlowStyles = makeStyles({
    container: {
        width: '100%',
        height: '100vh', // Full viewport height
        backgroundColor: '#ffffff', // Solid white background
    },
    emptyState: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '48px 24px',
        textAlign: 'center',
        height: '100vh', // Full viewport height for empty state too
    },
});

// Node types for React Flow
const nodeTypes = {
    phase: PhaseNode as any,
    hypothesis: HypothesisNode as any,
};

// Edge types for React Flow
const edgeTypes = {
    investigation: InvestigationEdge as any,
};

interface InvestigationFlowContentProps {
    onShowDetails?: (title: string, description: string, nodeType: 'phase' | 'hypothesis', steps?: any[]) => void;
}

const InvestigationFlowContent: React.FC<InvestigationFlowContentProps> = ({ onShowDetails }) => {
    const { treeState } = useContext(InvestigationTreeContext);

    // Provide a fallback onShowDetails function if none is provided or if it's empty
    const effectiveOnShowDetails = useMemo(() => {
        if (!onShowDetails || onShowDetails.toString() === '()=>{}') {
            return (title: string, description: string, nodeType: 'phase' | 'hypothesis', steps?: any[]) => {
                // Create a detailed alert with all the information
                const stepsText =
                    steps && steps.length > 0
                        ? `\n\nSteps (${steps.length}):\n${steps.map((step, i) => `${i + 1}. ${step.title || step.name || step}`).join('\n')}`
                        : '\n\nNo steps available';

                alert(`NODE DETAILS:\n\nTitle: ${title}\n\nType: ${nodeType}\n\nDescription:\n${description}${stepsText}`);
            };
        }
        return onShowDetails;
    }, [onShowDetails]);

    const classes = useFlowStyles();
    const theme = useTheme();

    const { nodes, edges, onNodesChange, onEdgesChange } = useInvestigationFlow(treeState.rootNodes || [], effectiveOnShowDetails);

    const [showingNodeDetail, setShowingNodeDetail] = useState(false);

    // Calculate the viewport fit options (kept for manual recenter button if needed)
    const fitViewOptions = useMemo(
        () => ({
            padding: 50,
            minZoom: 0.3,
            maxZoom: 1.5,
        }),
        []
    );

    // Debug logging
    useEffect(() => {}, [nodes, edges]);

    // Show empty state if no nodes
    if (!treeState.isVisible || !nodes || nodes.length === 0) {
        return (
            <div className={classes.emptyState}>
                <CircleRegular fontSize={48} color={theme.palette.neutralSecondary} />
                <Text size={500} style={{ marginTop: '16px', color: theme.palette.neutralSecondary }}>
                    No investigation data available
                </Text>
            </div>
        );
    }

    return (
        <div className={classes.container}>
            <ReactFlow
                nodes={nodes}
                edges={edges}
                onNodesChange={onNodesChange}
                onEdgesChange={onEdgesChange}
                nodeTypes={nodeTypes}
                edgeTypes={edgeTypes}
                fitViewOptions={fitViewOptions}
                attributionPosition="bottom-left"
                minZoom={0.3}
                maxZoom={1.5}
                defaultViewport={{ x: 0, y: 0, zoom: 0.8 }}
                defaultEdgeOptions={{
                    style: { strokeWidth: 2 },
                    type: 'investigation',
                }}
                connectionLineStyle={{ strokeWidth: 2 }}
                onInit={() => {
                    console.log('🎯 ReactFlow initialized');
                    // Remove automatic fitView to prevent constant recentering
                    // Manual recenter button is available if needed
                }}
                onNodeClick={(_, node) => {
                    if (node.data?.onShowDetails) {
                        setShowingNodeDetail(true);
                        node.data.onShowDetails(node.data.title, node.data.description, node.data.nodeType, node.data.steps);
                    }
                }}
                onPaneClick={() => {
                    if (showingNodeDetail) {
                        setShowingNodeDetail(false);
                    }
                }}
                proOptions={{
                    hideAttribution: true,
                }}
            >
                <Controls position="top-left" />
                <MiniMap
                    nodeStrokeWidth={3}
                    nodeColor={node => {
                        if (node.type === 'phase') {
                            return theme.palette.themePrimary;
                        }
                        return theme.palette.neutralSecondary;
                    }}
                    position="bottom-right"
                    pannable
                    zoomable
                />
            </ReactFlow>
        </div>
    );
};

export const InvestigationTreeFlow: React.FC<InvestigationFlowContentProps> = ({ onShowDetails }) => {
    return (
        <ReactFlowProvider>
            <InvestigationFlowContent onShowDetails={onShowDetails} />
        </ReactFlowProvider>
    );
};
