import { tokens } from '@fluentui/react-components';
import { Children, useCallback, useRef, useState } from 'react';

export interface ResizableChildProps {
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
}

export type ResizableChild = (props: ResizableChildProps) => React.ReactNode;

export type ResizableProps = {
    position?: 'left' | 'right';
    initialWidth: string;
    minWidthPixels: number;
    minWidthPercent?: number;
    maxWidthPixels?: number;
    maxWidthPercent?: number;
    collapsedWidthPixels?: number;
    collapsed?: boolean;
    setCollapsed: (collapsed: boolean) => void;
    children?: ((props: ResizableChildProps) => React.ReactNode) | React.ReactNode;
    style?: React.CSSProperties;
    handleStyle?: React.CSSProperties;
};

const isFunction = (obj: any): obj is ResizableChild => {
    return typeof obj === 'function';
};

const isEmptyChildren = (children: any): boolean => {
    return Children.count(children) === 0;
};

const getMaxWidth = (maxWidthPixels?: number, maxWidthPercent?: number, containerWidth?: number): number | undefined => {
    // if both maxWidthPixels and maxWidthPercent are provided, use the smaller of the two
    if (maxWidthPixels && maxWidthPercent && containerWidth) {
        return Math.min(maxWidthPixels, (maxWidthPercent / 100) * containerWidth);
    }

    // if only one of them is provided, use that one
    if (maxWidthPixels) {
        return maxWidthPixels;
    }

    if (maxWidthPercent && containerWidth) {
        return (maxWidthPercent / 100) * containerWidth;
    }

    // if none are provided, return undefined
    return undefined;
};

const getMinWidth = (minWidthPixels?: number, minWidthPercent?: number, containerWidth?: number): number | undefined => {
    // if both minWidthPixels and minWidthPercent are provided, use the larger of the two
    if (minWidthPixels && minWidthPercent && containerWidth) {
        return Math.max(minWidthPixels, (minWidthPercent / 100) * containerWidth);
    }

    // if only one of them is provided, use that one
    if (minWidthPixels) {
        return minWidthPixels;
    }

    if (minWidthPercent && containerWidth) {
        return (minWidthPercent / 100) * containerWidth;
    }

    // if none are provided, return undefined
    return undefined;
};

const getClientX = (event: React.PointerEvent | PointerEvent | React.TouchEvent | TouchEvent) => {
    if (event.type === 'touchstart') {
        return (event as TouchEvent).touches[0].clientX;
    }
    return (event as PointerEvent).clientX;
};

const releasePointerCapture = (event: React.PointerEvent | PointerEvent | React.TouchEvent | TouchEvent) => {
    // Check if the event is a PointerEvent
    if ('pointerId' in event) {
        // Cast event.target to HTMLElement to access releasePointerCapture
        const target = event.target as HTMLElement;
        if (target.releasePointerCapture) {
            target.releasePointerCapture(event.pointerId);
        }
    }
};

export const Resizable = ({
    position = 'left',
    initialWidth,
    maxWidthPercent,
    maxWidthPixels,
    minWidthPercent,
    minWidthPixels,
    collapsedWidthPixels,
    collapsed,
    setCollapsed,
    children,
    style,
    handleStyle,
}: ResizableProps) => {
    const resizableRef = useRef<HTMLDivElement>(null);
    const [width, setWidth] = useState(initialWidth);
    const [isResizing, setIsResizing] = useState(false);
    const [isHovering, setIsHovering] = useState(false);

    const onPointerEnter = (enterEvent: React.PointerEvent | React.TouchEvent) => {
        releasePointerCapture(enterEvent);
        setIsHovering(true);
    };

    const onPointerLeave = (leaveEvent: React.PointerEvent | React.TouchEvent) => {
        releasePointerCapture(leaveEvent);
        setIsHovering(false);
    };

    const onPointerDown = useCallback(
        (downEvent: React.PointerEvent | React.TouchEvent) => {
            setIsResizing(true);
            downEvent.preventDefault(); // Prevent text selection
            releasePointerCapture(downEvent);

            const startX = getClientX(downEvent);
            const startWidth = resizableRef.current?.offsetWidth || 0;
            const containerWidth = resizableRef.current?.parentElement?.offsetWidth;

            const minWidth = getMinWidth(minWidthPixels, minWidthPercent, containerWidth);
            const maxWidth = getMaxWidth(maxWidthPixels, maxWidthPercent, containerWidth);

            const handlePointerMove = (moveEvent: PointerEvent | TouchEvent) => {
                releasePointerCapture(moveEvent);
                const endX = getClientX(moveEvent);
                const changeDirection = position === 'left' ? 1 : -1;

                let newWidth = startWidth + changeDirection * (endX - startX);

                if (maxWidth) {
                    newWidth = Math.min(newWidth, maxWidth);
                }

                if (minWidth) {
                    newWidth = Math.max(newWidth, minWidth);
                }

                setWidth(`${Math.max(newWidth, minWidthPixels)}px`);
            };

            const handlePointerUp = (upEvent: PointerEvent | TouchEvent) => {
                releasePointerCapture(upEvent);
                setIsResizing(false);
                document.removeEventListener('pointermove', handlePointerMove);
                document.removeEventListener('pointerup', handlePointerUp);
                document.removeEventListener('pointerleave', handlePointerLeave);
            };

            const handlePointerLeave = (leaveEvent: PointerEvent | TouchEvent) => {
                releasePointerCapture(leaveEvent);
                setIsResizing(false);
                document.removeEventListener('pointermove', handlePointerMove);
                document.removeEventListener('pointerup', handlePointerUp);
                document.removeEventListener('pointerleave', handlePointerLeave);
            };

            document.addEventListener('pointermove', handlePointerMove);
            document.addEventListener('pointerup', handlePointerUp);
            document.addEventListener('pointerleave', handlePointerLeave);

            return () => {
                document.removeEventListener('pointermove', handlePointerMove);
                document.removeEventListener('pointerup', handlePointerUp);
                document.removeEventListener('pointerleave', handlePointerLeave);
            };
        },
        [position, maxWidthPercent, maxWidthPixels, minWidthPercent, minWidthPixels]
    );

    return (
        <div
            ref={resizableRef}
            style={{
                flex: `0 0 ${collapsed ? `${collapsedWidthPixels || 0}px` : width}`,
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'flex-start',
                alignItems: 'stretch',
                gap: '10px',
                maxWidth: maxWidthPercent ? `${maxWidthPercent}%` : undefined,
                minWidth: minWidthPercent ? `${minWidthPercent}%` : undefined,
                position: 'relative',
                height: '100%',
                ...style,
            }}
        >
            {children
                ? isFunction(children)
                    ? children({ collapsed, setCollapsed })
                    : !isEmptyChildren(children)
                      ? Children.only(children)
                      : null
                : null}
            {!collapsed && (
                <div
                    style={{
                        width: '2px',
                        cursor: 'ew-resize',
                        position: 'absolute',
                        top: 0,
                        right: position === 'left' ? 0 : undefined,
                        bottom: 0,
                        left: position === 'right' ? 0 : undefined,
                        backgroundColor: isHovering || isResizing ? tokens.colorNeutralStroke2 : 'transparent',
                        ...handleStyle,
                    }}
                    onPointerDown={onPointerDown}
                    onPointerEnter={onPointerEnter}
                    onPointerLeave={onPointerLeave}
                />
            )}
        </div>
    );
};
