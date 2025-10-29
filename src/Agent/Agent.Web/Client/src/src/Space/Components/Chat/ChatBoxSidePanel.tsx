import { InlineDrawer, useRestoreFocusSource } from '@fluentui/react-components';
import { CSSProperties, memo, ReactNode, useCallback, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { GenericErrorResources } from '../../../Strings/SREAgentResources';
import { ChatBoxSidePanelStyleProps, getChatBoxSidePanelStyles } from '../../Styles/Activities.styles';

export interface IChatBoxSidePanelProps {
    inlineDrawerStyles?: CSSProperties;
    defaultSidePanelWidth?: string;
    open: boolean;
    children?: ReactNode | ReactNode[];
    onResize?: () => void;
    stylesForClassNames?: ChatBoxSidePanelStyleProps;
    sidePanelWidth: number | null;
    setSidePanelWidth: (width: number | null) => void;
}

const ChatBoxSidePanel = ({
    inlineDrawerStyles,
    stylesForClassNames,
    open,
    onResize,
    sidePanelWidth,
    setSidePanelWidth,
    defaultSidePanelWidth,
    children,
}: IChatBoxSidePanelProps) => {
    const chatBoxStyles = useMemo(() => getChatBoxSidePanelStyles(stylesForClassNames), [stylesForClassNames]);

    const restoreFocusSourceAttributes = useRestoreFocusSource();
    const intl = useIntl();

    const animationFrame = useRef<number>(0);
    const sidebarRef = useRef<HTMLDivElement>(null);

    const [isResizing, setIsResizing] = useState(false);

    const startResizing = useCallback(() => setIsResizing(true), []);
    const stopResizing = useCallback(() => setIsResizing(false), []);

    const resize = useCallback(
        ({ clientX }: { clientX: number }) => {
            animationFrame.current = requestAnimationFrame(() => {
                if (isResizing && sidebarRef.current) {
                    const newSidebarWidth = sidebarRef.current.getBoundingClientRect().right - clientX;
                    setSidePanelWidth(newSidebarWidth);
                    onResize?.();
                }
            });
        },
        [isResizing, onResize]
    );

    useEffect(() => {
        window.addEventListener('mousemove', resize);
        window.addEventListener('mouseup', stopResizing);

        return () => {
            cancelAnimationFrame(animationFrame.current);
            window.removeEventListener('mousemove', resize);
            window.removeEventListener('mouseup', stopResizing);
        };
    }, [resize, stopResizing]);

    return (
        <InlineDrawer
            {...restoreFocusSourceAttributes}
            position="end"
            open={open}
            ref={sidebarRef}
            className={chatBoxStyles.root}
            style={{
                width: sidePanelWidth === null ? (defaultSidePanelWidth ?? '60%') : `${sidePanelWidth}px`,
                maxWidth: 'calc(100% - 400px)',
                transition: 'width 0.1s ease, min-width 0.1s ease',
                ...inlineDrawerStyles,
            }}
        >
            {children}
            <div
                className={chatBoxStyles.resizer}
                onMouseDown={startResizing}
                aria-label={intl.formatMessage(GenericErrorResources.resizeDrawer)}
                role="separator"
                aria-orientation="vertical"
            />
        </InlineDrawer>
    );
};

export default memo(ChatBoxSidePanel);
