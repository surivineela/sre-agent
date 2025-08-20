import { BaseEdge, EdgeProps, getBezierPath } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { GraphFlowEdge } from '../../Contracts/Activities';
import { AgentTaskContext } from '../../Contracts/Context';
import CustomEdgeMarker from '../../Graph/CustomEdgeMarker';
import { getHypothesisNodeThemeColor } from './Utility';

const ChildrenEdge = (props: EdgeProps<GraphFlowEdge>) => {
    const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data } = props;

    const { getNodeStatus } = useContext(AgentTaskContext);

    const [edgePath] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    const edgeColor = useMemo(() => {
        if (data && data.targetId) {
            const status = getNodeStatus(data.targetId);

            return getHypothesisNodeThemeColor(status);
        }
    }, [data?.targetId]);

    return (
        <>
            <CustomEdgeMarker id={id} color={edgeColor} size={'20'} />
            <BaseEdge
                id={id}
                path={edgePath}
                markerEnd={`url(#${id})`}
                markerStart={undefined}
                style={edgeColor ? { stroke: edgeColor } : undefined}
            />
        </>
    );
};

export default memo(ChildrenEdge);
