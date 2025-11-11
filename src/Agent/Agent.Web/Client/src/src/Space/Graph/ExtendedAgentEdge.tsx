import { tokens } from '@fluentui/react-components';
import { BaseEdge, Edge, EdgeProps, getBezierPath } from '@xyflow/react';
import { FC, useContext, useMemo } from 'react';
import { ExtendedAgentGraphContext, ExtendedAgentGraphEdge } from '../Contracts/ExtendedAgentGraph';
import CustomEdgeMarker from './CustomEdgeMarker';

export const ExtendedAgentEdge: FC<EdgeProps<Edge<ExtendedAgentGraphEdge>>> = ({
    id,
    sourceX,
    sourceY,
    targetX,
    targetY,
    sourcePosition,
    targetPosition,
    data,
    ...rest
}) => {
    const { edgesToHighlight } = useContext(ExtendedAgentGraphContext);
    const { sourceType, targetType } = useMemo(() => {
        if (!data) {
            return { sourceType: undefined, targetType: undefined };
        }
        return data;
    }, [data]);

    const color = useMemo(() => {
        if (edgesToHighlight.includes(id)) {
            return tokens.colorBrandForegroundLinkHover;
        }
        return tokens.colorNeutralStroke1;
    }, [edgesToHighlight, id]);

    const [edgePath, labelX, labelY] = useMemo(() => {
        return getBezierPath({
            sourceX: sourceType === 'agent' ? sourceX - 40 : sourceX,
            sourceY,
            sourcePosition,
            targetX: targetType === 'agent' ? targetX + 40 : targetX,
            targetY,
            targetPosition,
        });
    }, [sourceX, sourceY, sourcePosition, targetX, targetY, targetPosition]);

    const markerSize = edgesToHighlight.includes(id) ? 10 : 10;

    return (
        <>
            <CustomEdgeMarker id={id} color={color} size={markerSize} />
            <BaseEdge
                {...rest}
                labelX={labelX}
                labelY={labelY}
                id={id}
                path={edgePath}
                markerEnd={`url(#${id})`}
                style={{ stroke: color }}
            />
        </>
    );
};
