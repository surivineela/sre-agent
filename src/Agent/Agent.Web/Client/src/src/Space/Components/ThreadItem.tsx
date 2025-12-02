import { Text } from '@fluentui/react-text';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { forwardRef, memo, useCallback, useMemo, useState } from 'react';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import ThreadActionsMenu from '../Activities/ThreadActionsMenu';
import { useThreadMenuStyle } from '../Styles/Activities.styles';
import { useActionsStatusBarStyles } from '../Styles/Incident.styles';
import Fade from './Fade';

interface IThreadItemProps {
    thread: Thread;
    selectThread: (thread: Thread | null) => void;
    deleteThread?: (thread: Thread) => void;
    isActive: boolean;
    isThreadUnread: boolean;
    favorite: boolean;
}

const ThreadItem = forwardRef<HTMLDivElement, IThreadItemProps>(({ thread, selectThread, deleteThread, isActive, isThreadUnread }, ref) => {
    const ThreadMenuStyles = useThreadMenuStyle();
    const styles = useActionsStatusBarStyles();
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const [isHovered, setIsHovered] = useState(false);

    const makeTextBold = useMemo(() => {
        return isThreadUnread && !isActive;
    }, [isThreadUnread, isActive]);

    const onSelectThread = useCallback(() => {
        if (isActive) return;

        selectThread(thread);
        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'selectThread',
            targetFriendlyName: 'Select thread',
            valueObjectName: thread.id,
            valueObjectFriendlyName: thread.id,
            metadata: {
                threadId: thread.id,
                threadType: thread.source ?? 'unknown',
            },
        });
    }, [logAmplitudeControlEvent, thread, isActive, selectThread]);

    const onConfirmDeleteThread = useCallback(() => {
        if (!deleteThread) return;

        deleteThread(thread);
        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: 'confirmDeleteThread',
            targetFriendlyName: 'Confirm delete thread',
            valueObjectName: thread.id,
            valueObjectFriendlyName: thread.id,
        });
    }, [logAmplitudeControlEvent, thread, deleteThread]);

    return (
        <>
            <div
                ref={ref}
                onClick={() => onSelectThread()}
                onKeyDown={e => {
                    if (e.key.toLowerCase() === 'enter') {
                        // Ensure that the event is only triggered when pressing Enter on the container itself, not on its children
                        if (e.target === e.currentTarget) {
                            onSelectThread();
                        }
                        e.stopPropagation();
                    }
                }}
                onMouseEnter={() => setIsHovered(true)}
                onMouseLeave={() => setIsHovered(false)}
                onFocus={() => setIsHovered(true)}
                onBlur={() => setIsHovered(false)}
                id={thread.id}
                data-testid={thread.id}
                tabIndex={0}
                className={mergeStyles(
                    ThreadMenuStyles.threadItem,
                    isActive ? ThreadMenuStyles.activeThreadItem : undefined,
                    isHovered && !isActive ? ThreadMenuStyles.hoveredThreadItem : undefined
                )}
            >
                {isActive && <div className={ThreadMenuStyles.borderIndicator} />}
                <div className={ThreadMenuStyles.content}>
                    <Text className={styles.title} size={300} wrap={false} block weight={makeTextBold ? 'bold' : 'regular'}>
                        {thread.title}
                    </Text>
                </div>
                <Fade visible={isHovered} appear={true} unmountOnExit={true}>
                    <div onClick={e => e.stopPropagation()}>
                        <ThreadActionsMenu thread={thread} handleThreadDelete={() => onConfirmDeleteThread()} />
                    </div>
                </Fade>
            </div>
        </>
    );
});

export default memo(ThreadItem);
