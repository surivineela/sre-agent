import { Dispatch, SetStateAction, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import { Guid } from '../../Common/Helpers/Guid';
import { getIntervalBetweenLoading, updateChatMessageGroups } from '../Activities/Utility';
import { ChatMessage, ChatMessageGroup, MessageLoadingCounts } from '../Contracts/Activities';

export const useChatHistory = (
    setMessageGroups: Dispatch<SetStateAction<ChatMessageGroup[]>>,
    threadId: string | null | undefined,
    prepareForAddingChatHistory: () => void,
    scrollToBottom: (smooth: boolean) => void,
    setStreamingMessageGroup: Dispatch<SetStateAction<ChatMessageGroup | null | undefined>>,
    setIsAgentTyping: Dispatch<React.SetStateAction<boolean | undefined>>,
    setIsWaitingForStreamingMessages: Dispatch<React.SetStateAction<boolean | undefined>>,
    hasExistingStreamingMessage: boolean
) => {
    const [newestMessageTimestampInOldMessages, setNewestMessageTimestampInOldMessages] = useState<string | null>(null);
    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [chatHistoryChangeTrigger, setChatHistoryChangeTrigger] = useState<string | null>(null);

    const exponentialBackoffDepth = useRef(-1);
    const timeout = useRef<NodeJS.Timeout | undefined>(undefined);
    const loadOlderMessagesRef = useRef<(() => Promise<void>) | null>(null);
    const isFetchingPreviousPage = useRef<boolean>(false);
    const hasPreviousPage = useRef<boolean>(true);
    const timestampCutOffForFetchingOlderMessagesRef = useRef<string | undefined | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const messageClient = MessageClient.getInstance(sreAgentEndpoint);

    const fetchPage = async (
        threadId: string | undefined | null,
        maxTimestamp: string | undefined | null
    ): Promise<ChatMessage[] | undefined> => {
        if (threadId) {
            const messagesResponse = await messageClient.getMessages(threadId, {
                skip: 0,
                top: MessageLoadingCounts.default,
                descending: true,
                maxTimestamp: maxTimestamp || undefined,
            });

            if (messagesResponse.isSuccessful) {
                const page = messagesResponse.content || [];

                if (page.length < MessageLoadingCounts.default) {
                    hasPreviousPage.current = false;
                }

                return page;
            } else {
                return undefined;
            }
        }
    };

    const loadOlderMessages = useCallback(async () => {
        if (
            isLoadingInitialChatHistory ||
            isFetchingPreviousPage.current ||
            !hasPreviousPage.current ||
            !timestampCutOffForFetchingOlderMessagesRef.current
        ) {
            return;
        }

        const interval = exponentialBackoffDepth.current < 0 ? 0 : getIntervalBetweenLoading(exponentialBackoffDepth.current);

        timeout.current = setTimeout(async () => {
            isFetchingPreviousPage.current = true;
            const newPage = await fetchPage(threadId, timestampCutOffForFetchingOlderMessagesRef.current);
            if (!newPage) {
                exponentialBackoffDepth.current += 1;
            } else {
                if (newPage.length > 0) {
                    prepareForAddingChatHistory();
                    setMessageGroups(prev => updateChatMessageGroups(newPage, prev));
                    if (newPage.length > 0) {
                        timestampCutOffForFetchingOlderMessagesRef.current = newPage[newPage.length - 1].timeStamp;
                    }
                    setChatHistoryChangeTrigger(Guid.newGuid());
                }
                exponentialBackoffDepth.current = -1; // Reset on successful fetch
            }
            isFetchingPreviousPage.current = false;
        }, interval);
    }, [threadId, isLoadingInitialChatHistory]);

    useEffect(() => {
        if (threadId) {
            const loadInitialPage = async () => {
                setIsLoadingInitialChatHistory(true);
                setMessageGroups([]);
                timestampCutOffForFetchingOlderMessagesRef.current = null;
                setChatHistoryChangeTrigger(null);

                const initialPage = await fetchPage(threadId, undefined);
                const initialChatMessageGroup = updateChatMessageGroups(initialPage || [], []);

                if (initialChatMessageGroup.length > 0) {
                    const lastMessageGroup = initialChatMessageGroup[initialChatMessageGroup.length - 1];
                    const initialChatHistoryGroups = [];
                    // If the last message in the last chat message group is incomplete,
                    // set the last chat message group as a streaming message.
                    if (
                        (lastMessageGroup.agentMessages.length > 0 &&
                            lastMessageGroup.agentMessages[lastMessageGroup.agentMessages.length - 1].isComplete === false) ||
                        hasExistingStreamingMessage
                    ) {
                        setStreamingMessageGroup({ ...lastMessageGroup });
                        setIsAgentTyping(true);
                        setIsWaitingForStreamingMessages(true);
                        initialChatHistoryGroups.push(...initialChatMessageGroup.slice(0, initialChatMessageGroup.length - 1));
                    } else {
                        initialChatHistoryGroups.push(...initialChatMessageGroup);
                    }

                    prepareForAddingChatHistory();
                    setMessageGroups(initialChatHistoryGroups);
                    timestampCutOffForFetchingOlderMessagesRef.current = initialPage?.[initialPage.length - 1]?.timeStamp;
                }

                setNewestMessageTimestampInOldMessages(initialPage?.[0]?.timeStamp || '');
                setIsLoadingInitialChatHistory(false);

                requestAnimationFrame(() => scrollToBottom(false));
            };

            loadInitialPage();
        } else {
            setNewestMessageTimestampInOldMessages('');
            setIsLoadingInitialChatHistory(false);
            setMessageGroups([]);
            timestampCutOffForFetchingOlderMessagesRef.current = null;
            setChatHistoryChangeTrigger(null);
            hasPreviousPage.current = false;
        }
    }, [threadId, hasExistingStreamingMessage]);

    useEffect(() => {
        loadOlderMessagesRef.current = loadOlderMessages;
    }, [loadOlderMessages]);

    useEffect(() => {
        return () => {
            clearTimeout(timeout.current);
        };
    }, []);

    return {
        chatHistoryChangeTrigger,
        isLoadingInitialChatHistory,
        loadOlderMessagesRef,
        newestMessageTimestampInOldMessages,
    };
};
