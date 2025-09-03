import { Ref, useCallback, useContext, useEffect, useImperativeHandle, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { Thread, ThreadSource } from '../../Common/Contracts/DataPlane/Thread';
import { isFinalStreamingMessage, parseThreadFromStreamingText } from '../Activities/Utility';
import { ThreadMenuHandle } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { InputForThreadListWithFavoriteList, useThreadListWithFavoriteList } from './useThreadListWithFavoriteList';

export const useThreadsMenu = (ref: Ref<ThreadMenuHandle>, excludedSources?: ThreadSource[]) => {
    const { subscribeThreadUpdateEvent, subscribeMessageUpdateEvent } = useContext(StreamingContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const [showUnreadOnly, setShowUnreadOnly] = useState<boolean>(false);
    const [isFavoriteThreadListHidden, setIsFavoriteThreadListHidden] = useState<boolean>(false);
    const [isRegularThreadListHidden, setIsRegularThreadListHidden] = useState<boolean>(false);

    const filter: InputForThreadListWithFavoriteList = useMemo(
        () => ({
            includedSources: undefined,
            excludedSources,
            unreadOnly: showUnreadOnly,
            searchText: undefined,
        }),
        [excludedSources, showUnreadOnly]
    );

    const { removeThread, removeUnreadThreadId, onThreadModifiedTimestampUpdated, ...rest } = useThreadListWithFavoriteList(
        isFavoriteThreadListHidden,
        isRegularThreadListHidden,
        undefined,
        filter,
        'modifiedTimestamp'
    );

    const updateThreadLastReadTime = useCallback(
        async (threadId: string) => {
            const response = await threadClient.updateThreadLastReadTime(threadId);

            if (response.isSuccessful) {
                removeUnreadThreadId(threadId);
            }
        },
        [removeUnreadThreadId]
    );

    const removeThreadFromList = useCallback(
        (threadId: string) => {
            removeThread(threadId);
        },
        [removeThread]
    );

    const getThread = async (threadId: string): Promise<Thread | undefined> => {
        const response = await threadClient.getThread(threadId);
        if (response.isSuccessful && response.content) {
            return response.content;
        }
        return undefined;
    };

    useImperativeHandle(ref, () => ({
        removeThreadFromList: (threadId: string) => removeThreadFromList(threadId),
        updateThreadLastReadTime: (threadId: string) => updateThreadLastReadTime(threadId),
    }));

    useEffect(() => {
        const messageUpdateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            if (threadId && isFinalStreamingMessage(message)) {
                const updatedThread = await getThread(threadId);
                if (updatedThread) {
                    onThreadModifiedTimestampUpdated(updatedThread);
                }
            }
        };

        const threadCreateHandler = async (message: StreamingMessage) => {
            const threadId = message.additionalProperties?.threadId;
            const text = message.contents?.[0]?.text || '';
            if (threadId) {
                try {
                    const thread = parseThreadFromStreamingText(text);
                    onThreadModifiedTimestampUpdated(thread);
                } catch {
                    const updatedThread = await getThread(threadId);
                    if (updatedThread) {
                        onThreadModifiedTimestampUpdated(updatedThread);
                    }
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
    }, [subscribeThreadUpdateEvent, subscribeMessageUpdateEvent]);

    return {
        showUnreadOnly,
        setShowUnreadOnly,
        isFavoriteThreadListHidden,
        setIsFavoriteThreadListHidden,
        isRegularThreadListHidden,
        setIsRegularThreadListHidden,
        ...rest,
    };
};
