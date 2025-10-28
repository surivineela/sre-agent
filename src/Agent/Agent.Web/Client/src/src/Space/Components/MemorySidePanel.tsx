import { Button, DrawerHeader, InlineDrawer, Subtitle2, useRestoreFocusSource } from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { memo, useCallback, useEffect, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { MemorySearchResult } from '../../Common/Contracts/DataPlane/Message';
import { MemorySearchCardResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { useMemorySidePanelStyles } from '../Styles/MemorySidePanel.styles';
import MemorySearchPanelContent from './MemorySearchPanelContent';

interface MemorySidePanelProps {
    memoryResult: MemorySearchResult | null;
    onClose: () => void;
}

const MemorySidePanel = ({ memoryResult, onClose }: MemorySidePanelProps) => {
    const styles = useMemorySidePanelStyles();
    const intl = useIntl();
    const restoreFocusSourceAttributes = useRestoreFocusSource();
    const sidebarRef = useRef<HTMLDivElement>(null);
    const animationFrame = useRef<number>(0);
    const [sideBarWidth, setSidebarWidth] = useState<number | null>(null);
    const [isResizing, setIsResizing] = useState(false);

    const startResizing = useCallback(() => setIsResizing(true), []);
    const stopResizing = useCallback(() => setIsResizing(false), []);

    const resize = useCallback(
        ({ clientX }: { clientX: number }) => {
            animationFrame.current = requestAnimationFrame(() => {
                if (isResizing && sidebarRef.current) {
                    const newSidebarWidth = sidebarRef.current.getBoundingClientRect().right - clientX;
                    setSidebarWidth(newSidebarWidth);
                }
            });
        },
        [isResizing]
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

    if (!memoryResult) return null;

    return (
        <InlineDrawer
            {...restoreFocusSourceAttributes}
            position="end"
            open={!!memoryResult}
            ref={sidebarRef}
            className={styles.root}
            style={{
                minWidth: '450px',
                width: sideBarWidth === null ? '40%' : `${sideBarWidth}px`,
            }}
        >
            <DrawerHeader>
                <div className={styles.header}>
                    <Subtitle2 className={styles.headerText}>{intl.formatMessage(MemorySearchCardResources.memorySearchResults)}</Subtitle2>
                    <Button
                        appearance="subtle"
                        icon={<Dismiss24Regular />}
                        onClick={onClose}
                        aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                        className={styles.headerButton}
                    />
                </div>
            </DrawerHeader>
            <div className={styles.content}>
                <MemorySearchPanelContent memoryResult={memoryResult} />
            </div>
            <div className={styles.resizer} onMouseDown={startResizing} />
        </InlineDrawer>
    );
};

export default memo(MemorySidePanel);
