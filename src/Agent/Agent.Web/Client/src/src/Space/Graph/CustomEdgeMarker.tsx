import { memo } from "react";
import { DEFAULT_MARKER_COLOR } from "./Constants";

const CustomArrowMarker = ({ id, color }: { id: string, color?: string }) => (
    <defs>
        <marker
            id={id}
            viewBox="-5 -5 10 10"
            refX="0"
            refY="0"
            markerWidth="30"
            markerHeight="30"
            orient="auto-start-reverse"
        >
            <path d="M -5,-5 L 0,0 L -5,5 z" fill={color || DEFAULT_MARKER_COLOR} />
        </marker>
    </defs>
);

export default memo(CustomArrowMarker);