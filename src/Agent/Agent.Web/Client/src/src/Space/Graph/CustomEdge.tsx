import { Edge, EdgeProps, BaseEdge, getBezierPath } from "@xyflow/react";
import { GraphContext, GraphEdge } from "../Contracts/Graph";
import { useContext } from "react";
import { useGraphEdgeStyles } from "../Styles/Graph.styles";
import CustomEdgeMarker from "./CustomEdgeMarker";
import { tokens } from "@fluentui/react-components";

export const CustomEdge = (props: EdgeProps<Edge<GraphEdge>>) => {
    const { id, sourceX, sourceY, targetX, targetY, sourcePosition, targetPosition, ...rest } = props;

    const { edgesToHightlight } = useContext(GraphContext);

    const { hightlightedEdge } = useGraphEdgeStyles();

    const [edgePath] = getBezierPath({
        sourceX,
        sourceY,
        sourcePosition,
        targetX,
        targetY,
        targetPosition
    });

    return <>
        <CustomEdgeMarker id={id} color={edgesToHightlight.includes(id) ? tokens.colorBrandForegroundLinkHover : undefined} />
        <BaseEdge
            {...rest}
            id={id}
            path={edgePath}
            markerEnd={`url(#${id})`}
            className={edgesToHightlight.includes(id) ? hightlightedEdge : undefined}
        />
    </>

};
