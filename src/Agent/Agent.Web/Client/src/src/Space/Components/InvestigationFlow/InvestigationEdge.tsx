import { tokens } from '@fluentui/react-components';
import { BaseEdge, Edge, EdgeProps, getBezierPath, MarkerType } from '@xyflow/react';
import React from 'react';

interface InvestigationEdgeData extends Edge<Record<string, unknown>, string | undefined> {
    label?: string;
    edgeType: 'phase-to-phase' | 'phase-to-hypothesis' | 'hypothesis-to-hypothesis';
}

export const InvestigationEdge: React.FC<EdgeProps<InvestigationEdgeData>> = ({
    id,
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    data,
    // Filter out React Flow specific props that shouldn't go to DOM - we don't use them
}) => {
    const [edgePath] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    // Different styling based on edge type - all gray
    const getEdgeStyle = () => {
        switch (data?.edgeType) {
            case 'phase-to-phase':
                return {
                    stroke: tokens.colorNeutralStroke1,
                    strokeWidth: 4,
                };
            case 'phase-to-hypothesis':
                return {
                    stroke: tokens.colorNeutralStroke1,
                    strokeWidth: 3,
                };
            case 'hypothesis-to-hypothesis':
                return {
                    stroke: tokens.colorNeutralStroke2,
                    strokeWidth: 3,
                };
            default:
                return {
                    stroke: tokens.colorNeutralStroke1,
                    strokeWidth: 3,
                };
        }
    };

    return (
        <BaseEdge
            id={id}
            path={edgePath}
            markerEnd={MarkerType.ArrowClosed}
            markerStart={undefined}
            style={{
                ...getEdgeStyle(),
                strokeDasharray: '0',
                filter: 'drop-shadow(0px 1px 2px rgba(0, 0, 0, 0.1))',
            }}
            // Only pass through safe props, not the React Flow internal props
        />
    );
};

export type { InvestigationEdgeData };
