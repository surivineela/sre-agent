import { Dispatch, SetStateAction, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import { convertMessageToChatMessage, getIntervalBetweenLoading } from '../Activities/Utility';
import { ChatMessage, MessageLoadingCounts } from '../Contracts/Activities';

export const useChatHistory = (
    messages: ChatMessage[],
    setMessages: Dispatch<SetStateAction<ChatMessage[]>>,
    threadId: string | null | undefined,
    prepareForAddingChatHistory: () => void
) => {
    const [newestMessageTimestampInOldMessages, setNewestMessageTimestampInOldMessages] = useState<string | null>(null);
    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [chatHistoryLength, setChatHistoryLength] = useState<number>(0);

    const exponentialBackoffDepth = useRef(-1);
    const timeout = useRef<NodeJS.Timeout | undefined>(undefined);
    const loadOlderMessagesRef = useRef<(() => void) | null>(null);
    const isFetchingPreviousPage = useRef<boolean>(false);
    const hasPreviousPage = useRef<boolean>(true);
    const timestampCutOffForFetchingLatestMessagesRef = useRef<string | undefined | null>(null);
    timestampCutOffForFetchingLatestMessagesRef.current = messages[0]?.timeStamp;

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
                const page = (messagesResponse.content || []).map(convertMessageToChatMessage).reverse();

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
            !timestampCutOffForFetchingLatestMessagesRef.current
        ) {
            return;
        }

        const interval = exponentialBackoffDepth.current < 0 ? 0 : getIntervalBetweenLoading(exponentialBackoffDepth.current);

        timeout.current = setTimeout(async () => {
            isFetchingPreviousPage.current = true;
            const newPage = await fetchPage(threadId, timestampCutOffForFetchingLatestMessagesRef.current);
            if (!newPage) {
                exponentialBackoffDepth.current += 1;
            } else {
                if (newPage.length > 0) {
                    prepareForAddingChatHistory();
                    setMessages(prev => [...newPage, ...prev]);
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
                setChatHistoryLength(0);
                const initialPage = await fetchPage(threadId, undefined);
                if (initialPage && initialPage.length > 0) {
                    prepareForAddingChatHistory();
                    setMessages([...initialPage]);
                    setChatHistoryLength(initialPage.length);
                }
                setNewestMessageTimestampInOldMessages(initialPage?.[initialPage.length - 1]?.timeStamp || '');
                setIsLoadingInitialChatHistory(false);
            };

            loadInitialPage();
        } else {
            setNewestMessageTimestampInOldMessages('');
            setIsLoadingInitialChatHistory(false);
            setMessages([]);
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
