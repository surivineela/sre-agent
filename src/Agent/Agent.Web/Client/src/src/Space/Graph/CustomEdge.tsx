import { tokens } from '@fluentui/react-components';
import { BaseEdge, Edge, EdgeLabelRenderer, EdgeProps, getBezierPath } from '@xyflow/react';
import { useContext } from 'react';
import { GraphContext, GraphEdge } from '../Contracts/Graph';
import { useGraphEdgeStyles } from '../Styles/Graph.styles';
import CustomEdgeMarker from './CustomEdgeMarker';

export const CustomEdge = (props: EdgeProps<Edge<GraphEdge>>) => {
    const { id, label, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, ...rest } = props;

    const { edgesToHightlight } = useContext(GraphContext);

    const { hightlightedEdge } = useGraphEdgeStyles();

    const [edgePath, labelX, labelY] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    return (
        <>
            <CustomEdgeMarker id={id} color={edgesToHightlight.includes(id) ? tokens.colorBrandForegroundLinkHover : undefined} />
            <BaseEdge
                {...rest}
                id={id}
                path={edgePath}
                markerEnd={`url(#${id})`}
                className={edgesToHightlight.includes(id) ? hightlightedEdge : undefined}
            />
            <EdgeLabelRenderer>
                <div
                    style={{
                        position: 'absolute',
                        transform: `translate(-50%, -50%) translate(${labelX}px, ${labelY}px)`,
                        pointerEvents: 'all',
                    }}
                >
                    {label}
                </div>
            </EdgeLabelRenderer>
        </>
    );
};
