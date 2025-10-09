import { memo } from 'react';
import { DEFAULT_MARKER_COLOR } from '../Contracts/Graph';

type CustomArrowMarkerProps = {
    id: string;
    color?: string;
    size?: number | string;
};

const CustomArrowMarker = ({ id, color, size }: CustomArrowMarkerProps) => (
    <defs>
        <marker
            id={id}
            viewBox="-5 -5 10 10"
            refX="0"
            refY="0"
            markerWidth={size ?? 30}
            markerHeight={size ?? 30}
            orient="auto-start-reverse"
        >
            <path d="M -5,-5 L 0,0 L -5,5 z" fill={color || DEFAULT_MARKER_COLOR} />
        </marker>
    </defs>
);

export default memo(CustomArrowMarker);
