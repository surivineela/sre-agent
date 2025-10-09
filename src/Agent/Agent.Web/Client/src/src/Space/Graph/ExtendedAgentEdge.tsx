import { tokens } from '@fluentui/react-components';
import { BaseEdge, Edge, EdgeProps, getBezierPath } from '@xyflow/react';
import { useContext, useMemo } from 'react';
import { ExtendedAgentGraphContext, ExtendedAgentGraphEdge, ExtendedAgentRelationType } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentEdgeStyles } from '../Styles/ExtendedAgentGraph.styles';
import CustomEdgeMarker from './CustomEdgeMarker';

export const ExtendedAgentEdge = (props: EdgeProps<Edge<ExtendedAgentGraphEdge>>) => {
    const { id, label, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, data, ...rest } = props;

    const { edgesToHighlight } = useContext(ExtendedAgentGraphContext);

    const { highlightedEdge, usesToolEdge, systemToolEdge, connectorEdge, agentAsToolEdge, handoffEdge } = useExtendedAgentEdgeStyles();

    const [edgePath, labelX, labelY] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition,
    });

    const edgeStyleClass = useMemo(() => {
        if (!data?.relationType) return undefined;

        switch (data.relationType) {
            case ExtendedAgentRelationType.UsesTool:
                return usesToolEdge;
            case ExtendedAgentRelationType.UsesSystemTool:
                return systemToolEdge;
            case ExtendedAgentRelationType.ToolUsesConnector:
                return connectorEdge;
            case ExtendedAgentRelationType.AgentAsTool:
                return agentAsToolEdge;
            case ExtendedAgentRelationType.HandoffTo:
                return handoffEdge;
            default:
                return undefined;
        }
    }, [data?.relationType, usesToolEdge, systemToolEdge, connectorEdge, agentAsToolEdge, handoffEdge]);

    const edgeColor = useMemo(() => {
        if (edgesToHighlight.includes(id)) {
            return tokens.colorBrandForegroundLinkHover;
        }

        switch (data?.relationType) {
            case ExtendedAgentRelationType.UsesTool:
                return tokens.colorPaletteBlueForeground2;
            case ExtendedAgentRelationType.UsesSystemTool:
                return tokens.colorPaletteGoldForeground2;
            case ExtendedAgentRelationType.ToolUsesConnector:
                return tokens.colorPaletteGreenForeground2;
            case ExtendedAgentRelationType.AgentAsTool:
                return tokens.colorPalettePurpleForeground2;
            case ExtendedAgentRelationType.HandoffTo:
                return tokens.colorPaletteDarkOrangeForeground2;
            default:
                return undefined;
        }
    }, [data?.relationType, edgesToHighlight, id]);

    const markerSize = edgesToHighlight.includes(id) ? 14 : 10;

    return (
        <>
            <CustomEdgeMarker id={id} color={edgeColor} size={markerSize} />
            <BaseEdge
                label={label}
                {...rest}
                labelX={labelX}
                labelY={labelY}
                id={id}
                path={edgePath}
                markerEnd={`url(#${id})`}
                className={edgesToHighlight.includes(id) ? highlightedEdge : edgeStyleClass}
            />
        </>
    );
};
