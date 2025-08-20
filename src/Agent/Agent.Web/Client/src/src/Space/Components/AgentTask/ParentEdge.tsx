import { BaseEdge, EdgeProps, getBezierPath } from '@xyflow/react';
import { memo } from 'react';
import { GraphFlowEdge } from '../../Contracts/Activities';
import CustomEdgeMarker from '../../Graph/CustomEdgeMarker';

const ParentEdge = (props: EdgeProps<GraphFlowEdge>) => {
    const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data } = props;

    const nodeTitleOffset = 20; // The offset of the title to the node

    const [edgePath] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY: targetY - (data?.fromInitialInvestigation ? 0 : nodeTitleOffset),
        targetPosition,
    });

    return (
        <>
            <CustomEdgeMarker id={id} size={'20'} />
            <BaseEdge id={id} path={edgePath} markerEnd={`url(#${id})`} />
        </>
    );
};

export default memo(ParentEdge);
