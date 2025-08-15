import { tokens } from '@fluentui/tokens';
import { BaseEdge, EdgeProps, getBezierPath } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { HypothesisStatus, InvestigationStatusCommon, TaskProgressStatus } from '../../../Common/Contracts/DataPlane/AgentTask';
import { GraphFlowEdge } from '../../Contracts/Activities';
import { AgentTaskContext } from '../../Contracts/Context';
import CustomEdgeMarker from '../../Graph/CustomEdgeMarker';

const AgentTaskGraphFlowEdge = (props: EdgeProps<GraphFlowEdge>) => {
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

    const edgeStyle = useMemo(() => {
        if (data && data.targetId) {
            const status = getNodeStatus(data.targetId);

            switch (status) {
                case InvestigationStatusCommon.Complete:
                case TaskProgressStatus.Completed:
                case HypothesisStatus.Validated:
                    return {
                        stroke: tokens.colorStatusSuccessForeground3,
                        strokeWidth: 2,
                    };
                case TaskProgressStatus.Failed:
                case HypothesisStatus.Invalidated:
                    return {
                        stroke: tokens.colorStatusDangerForeground3,
                        strokeWidth: 2,
                    };
            }
        }
    }, [data?.targetId]);

    return (
        <>
            <CustomEdgeMarker id={id} />
            <BaseEdge
                id={id}
                path={edgePath}
                markerEnd={`url(#${id})`}
                markerStart={undefined}
                style={edgeStyle ? { ...edgeStyle } : undefined}
            />
        </>
    );
};

export default memo(AgentTaskGraphFlowEdge);
