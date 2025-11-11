import { memo } from 'react';
import { DEFAULT_MARKER_COLOR } from '../Contracts/Graph';

type CustomArrowMarkerProps = {
    id: string;
    color?: string;
    size?: number | string;
};

const CustomArrowMarker = ({ id, color, size }: CustomArrowMarkerProps) => (
    <defs>
        <marker id={id} refX="8" refY="5" markerWidth={size || 10} markerHeight={size || 10} orient="auto-start-reverse">
            <path d="M2,8 L8,5 L2,2" stroke={color || DEFAULT_MARKER_COLOR} strokeWidth="1" fill="none" />
        </marker>
    </defs>
);

export default memo(CustomArrowMarker);
