import { Button, Tooltip } from '@fluentui/react-components';
import { Branch16Regular, TaskListLtr20Regular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { IncidentManagementResources, SreAgentResources, ToDoPlanResources } from '../../Strings/SREAgentResources';
import { AgentContext, StreamingContext } from '../Contracts/Context';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ThreadActionsMenu from './ThreadActionsMenu';
import { isFinalStreamingMessage, parseThreadFromStreamingText } from './Utility';

const ThreadContentTitle = ({
    thread,
    deleteThread,
    hasToDoPlans,
    isToDoPlanOpen,
    openToDoPlan,
    closeToDoPlan,
    showTraceButton,
    toggleTraceVisibility,
    traceFocusRestorationRef,
}: {
    thread: Thread | null | undefined;
    deleteThread: (thread: Thread) => void;
    hasToDoPlans: boolean;
    isToDoPlanOpen: boolean;
    openToDoPlan: () => void;
    closeToDoPlan: () => void;
    showTraceButton: boolean;
    toggleTraceVisibility: () => void;
    traceFocusRestorationRef: React.RefObject<HTMLButtonElement>;
}) => {
    const intl = useIntl();
    const [latestThread, setLatestThread] = useState<Thread | null | undefined>(thread);
    const { activeThreadId, subscribeThreadTitleUpdate, subscribeThreadFavoriteUpdate } = useContext(AgentContext);
    const { subscribeThreadUpdateEvent, subscribeMessageUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const tooltip = useMemo(() => {
        return isToDoPlanOpen
            ? intl.formatMessage(ToDoPlanResources.todoPlanCloseTooltip)
            : intl.formatMessage(ToDoPlanResources.todoPlanOpenTooltip);
    }, [isToDoPlanOpen, intl]);

    const handleThreadDelete = useCallback(() => {
        if (thread) {
            deleteThread(thread);
        }
    }, [thread, deleteThread]);

    const updateLatestThread = async (threadId: string) => {
        const response = await threadClient.getThread(threadId);
        if (response.isSuccessful && response.content) {
            setLatestThread(response.content);
        }
    };

    useEffect(() => {
        const id = thread?.id || activeThreadId;

        const messageUpdateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            if (threadId && threadId === id && isFinalStreamingMessage(message)) {
                updateLatestThread(threadId);
            }
        };

        const threadCreateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            const text = message.contents?.[0]?.text || '';
            if (threadId && threadId === id) {
                try {
                    const thread = parseThreadFromStreamingText(text);
                    setLatestThread(thread);
                } catch {
                    updateLatestThread(threadId);
                }
            }
        };

        const threadTitleUpdateHandler = (threadId: string, newTitle: string) => {
            if (threadId === id) {
                setLatestThread(prev => {
                    if (prev) {
                        return { ...prev, title: newTitle };
                    }
                    return prev;
                });
            }
        };

        const threadFavoriteUpdateHandler = (threadId: string, isFavorite: boolean) => {
            if (threadId === id) {
                setLatestThread(prev => {
                    if (prev) {
                        return { ...prev, favorite: isFavorite };
                    }
                    return prev;
                });
            }
        };

        const unsubscribeMessageUpdateEvent = subscribeMessageUpdateEvent({
            handler: messageUpdateHandler,
        });

        const unsubscribeThreadUpdateEvent = subscribeThreadUpdateEvent(threadCreateHandler);

        const unsubscribeThreadTitleUpdate = subscribeThreadTitleUpdate(threadTitleUpdateHandler);
        const unsubscribeThreadFavoriteUpdate = subscribeThreadFavoriteUpdate(threadFavoriteUpdateHandler);

        return () => {
            unsubscribeMessageUpdateEvent();
            unsubscribeThreadUpdateEvent();
            unsubscribeThreadTitleUpdate();
            unsubscribeThreadFavoriteUpdate();
        };
    }, [thread?.id, activeThreadId, subscribeThreadUpdateEvent, subscribeMessageUpdateEvent, subscribeThreadTitleUpdate]);

    useEffect(() => {
        if (!latestThread?.id && activeThreadId) {
            updateLatestThread(activeThreadId);
        }
    }, [latestThread?.id, activeThreadId]);

    const threadId = latestThread?.id ?? null;

    return (
        <div className={ThreadContentStyles.titleContainer}>
            <ThreadTitleText title={latestThread?.title} />
            {latestThread && <ThreadActionsMenu thread={latestThread} handleThreadDelete={handleThreadDelete} />}
            <div style={{ display: 'flex', alignItems: 'center', marginLeft: 'auto' }}>
                {threadId && showTraceButton && (
                    <Button
                        ref={traceFocusRestorationRef}
                        icon={<Branch16Regular />}
                        style={{
                            fontWeight: 'normal',
                            fontSize: '12px',
                            lineHeight: '16px',
                            padding: '2px 8px 2px 4px',
                            margin: 'auto',
                            marginRight: '8px',
                        }}
                        onClick={toggleTraceVisibility}
                    >
                        {intl.formatMessage(IncidentManagementResources.viewTrace)}
                    </Button>
                )}
                {threadId && hasToDoPlans && (
                    <Tooltip content={tooltip} relationship="label">
                        <Button
                            aria-label={tooltip}
                            icon={<TaskListLtr20Regular />}
                            appearance={'subtle'}
                            shape="circular"
                            onClick={() => (isToDoPlanOpen ? closeToDoPlan() : openToDoPlan())}
                        />
                    </Tooltip>
                )}
            </div>
        </div>
    );
};

const ThreadTitleText = memo(({ title }: { title?: string }) => {
    return (
        <Text as="h2" wrap={false} block weight="semibold" size={400} className={ThreadContentStyles.title}>
            {title ?? <FormattedMessage {...SreAgentResources.newThread} />}
        </Text>
    );
});

export default memo(ThreadContentTitle);
