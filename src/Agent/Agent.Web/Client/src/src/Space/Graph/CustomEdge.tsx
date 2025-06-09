import { tokens } from '@fluentui/react-components';
import { BaseEdge, Edge, EdgeProps, getBezierPath } from '@xyflow/react';
import { useContext, useMemo } from 'react';
import { FormattedMessage } from 'react-intl';
import { GraphContext, GraphEdge } from '../Contracts/Graph';
import { useGraphEdgeStyles } from '../Styles/Graph.styles';
import CustomEdgeMarker from './CustomEdgeMarker';
import { getFriendlyEdgeLabel } from './Utility';

export const CustomEdge = (props: EdgeProps<Edge<GraphEdge>>) => {
    const { id, label, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, ...rest } = props;

    const { edgesToHighlight } = useContext(GraphContext);

    const { highlightedEdge } = useGraphEdgeStyles();

    const [edgePath, labelX, labelY] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    const displayLabel = useMemo(() => {
        return label && typeof label === 'string' ? getFriendlyEdgeLabel(label) : undefined;
    }, [label]);

    return (
        <>
            <CustomEdgeMarker id={id} color={edgesToHighlight.includes(id) ? tokens.colorBrandForegroundLinkHover : undefined} />
            <BaseEdge
                label={displayLabel ? <FormattedMessage {...displayLabel} /> : undefined}
                {...rest}
                labelX={labelX}
                labelY={labelY}
                id={id}
                path={edgePath}
                markerEnd={`url(#${id})`}
                className={edgesToHighlight.includes(id) ? highlightedEdge : undefined}
            />
        </>
    );
};
