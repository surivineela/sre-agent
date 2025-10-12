import { mergeClasses } from '@fluentui/react-components';
import type { JSX } from 'react';
import { useCallback, useEffect, useRef, useState } from 'react';
import { useResizeHandleStyles } from './ResizeHandle.Styles';

interface IResizeHandleProps {
    disabled?: boolean;
    minWidth: number;
    maxWidth: number;
    initialWidth: number;
    onWidthChange: (width: number) => void;
    onDragStateChange?: (isDragging: boolean) => void;
}

export function ResizeHandle({
    disabled,
    minWidth,
    maxWidth,
    initialWidth,
    onWidthChange,
    onDragStateChange,
}: IResizeHandleProps): JSX.Element {
    const styles = useResizeHandleStyles();
    const [isDragging, setIsDragging] = useState(false);
    const [currentWidth, setCurrentWidth] = useState(initialWidth);

    const dragStartXRef = useRef<number>(0);
    const dragStartWidthRef = useRef<number>(0);

    const handleMouseMove = useCallback(
        (e: MouseEvent) => {
            if (!isDragging) {
                return;
            }

            const deltaX = e.clientX - dragStartXRef.current;
            const newWidth = Math.max(minWidth, Math.min(maxWidth, dragStartWidthRef.current + deltaX));
            setCurrentWidth(newWidth);
            onWidthChange(newWidth);
        },
        [isDragging, minWidth, maxWidth, onWidthChange]
    );

    const handleMouseUp = useCallback(() => {
        setIsDragging(false);
        onDragStateChange?.(false);
        document.body.style.setProperty('cursor', '');
        document.body.style.setProperty('user-select', '');
    }, [onDragStateChange]);

    const handleMouseDown = useCallback(
        (e: React.MouseEvent) => {
            if (disabled) {
                return;
            }

            setIsDragging(true);
            onDragStateChange?.(true);
            dragStartXRef.current = e.clientX;
            dragStartWidthRef.current = currentWidth;
            document.body.style.setProperty('cursor', 'col-resize');
            document.body.style.setProperty('user-select', 'none');
        },
        [disabled, currentWidth, onDragStateChange]
    );

    // Resize with left/right arrow keys
    const handleKeyDown = useCallback(
        (e: React.KeyboardEvent) => {
            if (disabled) {
                return;
            }
            switch (e.key) {
                case 'ArrowRight': {
                    e.preventDefault();
                    const newWidth = Math.min(maxWidth, currentWidth + 10);
                    setCurrentWidth(newWidth);
                    onWidthChange(newWidth);

                    break;
                }
                case 'ArrowLeft': {
                    e.preventDefault();
                    const newWidth = Math.max(minWidth, currentWidth - 10);
                    setCurrentWidth(newWidth);
                    onWidthChange(newWidth);

                    break;
                }
                default:
                // Do nothing
            }
        },
        [disabled, currentWidth, minWidth, maxWidth, onWidthChange]
    );

    useEffect(() => {
        if (isDragging) {
            document.addEventListener('mousemove', handleMouseMove);
            document.addEventListener('mouseup', handleMouseUp);
        }

        return () => {
            document.removeEventListener('mousemove', handleMouseMove);
            document.removeEventListener('mouseup', handleMouseUp);
        };
    }, [isDragging, handleMouseMove, handleMouseUp]);

    return (
        <div
            aria-disabled={disabled}
            aria-orientation="vertical"
            aria-valuemax={maxWidth}
            aria-valuemin={minWidth}
            aria-valuenow={currentWidth}
            className={mergeClasses(styles.resizeHandle, isDragging && 'dragging', disabled && 'disabled')}
            onKeyDown={handleKeyDown}
            onMouseDown={handleMouseDown}
            role="separator"
            tabIndex={disabled ? -1 : 0}
        />
    );
}
