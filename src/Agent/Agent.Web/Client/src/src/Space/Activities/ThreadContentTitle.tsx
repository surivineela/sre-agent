import { Button, Tooltip, tokens } from '@fluentui/react-components';
import { TaskListLtr20Regular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react-text';
import { memo, useCallback, useContext, useEffect, useState } from 'react';
import { FormattedMessage } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { AgentContext, StreamingContext } from '../Contracts/Context';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ThreadActionsMenu from './ThreadActionsMenu';
import { isFinalStreamingMessage, parseThreadFromStreamingText } from './Utility';

const ThreadContentTitle = ({
    thread,
    deleteThread,
    hasExistingPlans,
}: {
    thread: Thread | null | undefined;
    deleteThread: (thread: Thread) => void;
    hasExistingPlans: boolean;
}) => {
    const [latestThread, setLatestThread] = useState<Thread | null | undefined>(thread);
    const { activeThreadId } = useContext(AgentContext);
    const { subscribeThreadUpdateEvent, subscribeMessageUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

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

        const unsubscribeMessageUpdateEvent = subscribeMessageUpdateEvent({
            handler: messageUpdateHandler,
        });

        const unsubscribeThreadUpdateEvent = subscribeThreadUpdateEvent(threadCreateHandler);

        return () => {
            unsubscribeMessageUpdateEvent();
            unsubscribeThreadUpdateEvent();
        };
    }, [thread?.id, activeThreadId, subscribeThreadUpdateEvent, subscribeMessageUpdateEvent]);

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
            <div style={{ display: 'flex', alignItems: 'center', gap: '8px', marginLeft: 'auto' }}>
                {threadId && hasExistingPlans && <HeaderTodoToggleButton />}
            </div>
        </div>
    );
};

const HeaderTodoToggleButton = () => {
    const [isOpen, setIsOpen] = useState(false);

    useEffect(() => {
        const onState = (e: Event) => {
            const ce = e as CustomEvent<{ open: boolean }>;
            setIsOpen(!!ce.detail?.open);
        };
        window.addEventListener('todo-plan-state', onState as EventListener);
        return () => window.removeEventListener('todo-plan-state', onState as EventListener);
    }, []);

    const tooltip = isOpen ? 'Close Todo Plans' : 'Open Todo Plans';

    return (
        <Tooltip content={tooltip} relationship="label">
            <Button
                aria-label={tooltip}
                aria-pressed={isOpen}
                icon={<TaskListLtr20Regular />}
                appearance={'subtle'}
                shape="circular"
                onClick={() => window.dispatchEvent(new Event('toggle-todo-plan'))}
                style={{ color: tokens.colorNeutralForeground2 }}
            />
        </Tooltip>
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
