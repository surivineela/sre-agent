import { Dispatch, SetStateAction, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import {
    convertMessageToChatMessage,
    getIntervalBetweenLoading,
    isTrajectoryInsightMessageType,
    shouldGroupWithPreviousMessage,
} from '../Activities/Utility';
import { ChatMessage, MessageLoadingCounts } from '../Contracts/Activities';

export const useChatHistory = (
    setMessages: Dispatch<SetStateAction<ChatMessage[]>>,
    threadId: string | null | undefined,
    prepareForAddingChatHistory: () => void,
    setStreamingMessage: Dispatch<SetStateAction<ChatMessage | null | undefined>>,
    setIsAgentTyping: Dispatch<React.SetStateAction<boolean | undefined>>,
    setIsWaitingForStreamingMessages: Dispatch<React.SetStateAction<boolean | undefined>>
) => {
    const [newestMessageTimestampInOldMessages, setNewestMessageTimestampInOldMessages] = useState<string | null>(null);
    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [chatHistoryLength, setChatHistoryLength] = useState<number>(0);

    const exponentialBackoffDepth = useRef(-1);
    const timeout = useRef<NodeJS.Timeout | undefined>(undefined);
    const loadOlderMessagesRef = useRef<(() => void) | null>(null);
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
                const page = (messagesResponse.content || [])
                    .map(convertMessageToChatMessage)
                    .filter(msg => !isTrajectoryInsightMessageType(msg as any)) // Filter out Session Insights from chat history
                    .reverse();

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
                    setMessages(prev => [...newPage, ...prev]);
                    if (newPage.length > 0) {
                        timestampCutOffForFetchingOlderMessagesRef.current = newPage[0].timeStamp;
                    }
                    setChatHistoryLength(prev => prev + newPage.length);
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
                setMessages([]);
                timestampCutOffForFetchingOlderMessagesRef.current = null;
                setChatHistoryLength(0);

                const initialPage = await fetchPage(threadId, undefined);

                if (initialPage && initialPage.length > 0) {
                    const lastMessage = initialPage?.[initialPage.length - 1];
                    const initialChatHistory = [];
                    // If the last message is incomplete, group it with the previous agent messages to construct
                    // a streaming message.
                    if (lastMessage && lastMessage.contents.length > 0 && lastMessage.contents[0].isComplete === false) {
                        let startIndex = initialPage.length - 2;
                        while (startIndex >= 0) {
                            if (shouldGroupWithPreviousMessage(lastMessage, initialPage[startIndex])) {
                                startIndex--;
                            } else {
                                break;
                            }
                        }

                        const currentStreamingMessages = initialPage.slice(startIndex + 1);
                        const streamingMessage: ChatMessage = {
                            ...currentStreamingMessages[0],
                            contents: currentStreamingMessages.flatMap(msg => msg.contents),
                        };
                        setStreamingMessage(streamingMessage);
                        setIsAgentTyping(true);
                        setIsWaitingForStreamingMessages(true);
                        initialChatHistory.push(...initialPage.slice(0, startIndex + 1));
                    } else {
                        initialChatHistory.push(...initialPage);
                    }

                    prepareForAddingChatHistory();
                    setMessages([...initialChatHistory]);
                    setChatHistoryLength(initialChatHistory.length);
                    timestampCutOffForFetchingOlderMessagesRef.current = initialPage[0].timeStamp;
                }
                setNewestMessageTimestampInOldMessages(initialPage?.[initialPage.length - 1]?.timeStamp || '');
                setIsLoadingInitialChatHistory(false);
            };

            loadInitialPage();
        } else {
            setNewestMessageTimestampInOldMessages('');
            setIsLoadingInitialChatHistory(false);
            setMessages([]);
            timestampCutOffForFetchingOlderMessagesRef.current = null;
            setChatHistoryLength(0);
            hasPreviousPage.current = false;
        }
    }, [threadId]);

    useEffect(() => {
        loadOlderMessagesRef.current = loadOlderMessages;
    }, [loadOlderMessages]);

    useEffect(() => {
        return () => {
            clearTimeout(timeout.current);
        };
    }, []);

    return {
        chatHistoryLength,
        isLoadingInitialChatHistory,
        loadOlderMessagesRef,
        newestMessageTimestampInOldMessages,
    };
};
