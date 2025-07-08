import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import { ThreadSource } from '../../Common/Contracts/Azure/SreAgent';
import {
    convertMessageToChatMessage,
    getIntervalBetweenLoading,
    isIncidentThreadCompleted,
    updateOldMessagesText,
} from '../Activities/Utility';
import { ChatMessage, MessageLoadingCounts } from '../Contracts/Activities';

export const useChatHistory = (
    threadId: string | null | undefined,
    threadSource: string | null | undefined,
    prepareForAddingChatHistory: () => void
) => {
    const [isIncidentInvestigationInProgress, setIsIncidentInvestigationInProgress] = useState<boolean>(
        threadSource === ThreadSource.incident
    );
    const [newestMessageTimestampInOldMessages, setNewestMessageTimestampInOldMessages] = useState<string | null>(null);
    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [chatHistory, setChatHistory] = useState<ChatMessage[][]>([]);

    const exponentialBackoffDepth = useRef(-1);
    const timeout = useRef<NodeJS.Timeout | undefined>(undefined);
    const loadOlderMessagesRef = useRef<(() => void) | null>(null);
    const isFetchingPreviousPage = useRef<boolean>(false);
    const hasPreviousPage = useRef<boolean>(true);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
    const messageClient = MessageClient.getInstance(sreAgentEndpoint);

    const timestampCutOffForFetchingLatestMessages = useMemo(() => {
        return chatHistory[chatHistory.length - 1]?.[0]?.timeStamp;
    }, [chatHistory]);

    const fetchPage = async (
        threadId: string | undefined | null,
        maxTimestamp: string | null | undefined
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
            !timestampCutOffForFetchingLatestMessages
        ) {
            return;
        }

        const interval = exponentialBackoffDepth.current < 0 ? 0 : getIntervalBetweenLoading(exponentialBackoffDepth.current);

        timeout.current = setTimeout(async () => {
            isFetchingPreviousPage.current = true;
            const newPage = await fetchPage(threadId, timestampCutOffForFetchingLatestMessages);
            if (!newPage) {
                exponentialBackoffDepth.current += 1;
            } else {
                if (newPage.length > 0) {
                    prepareForAddingChatHistory();
                    setChatHistory(prev => [newPage, ...prev]);
                }
                exponentialBackoffDepth.current = -1; // Reset on successful fetch
            }
            isFetchingPreviousPage.current = false;
        }, interval);
    }, [threadId, timestampCutOffForFetchingLatestMessages, isLoadingInitialChatHistory]);

    useEffect(() => {
        if (threadId) {
            const loadInitialPage = async () => {
                setIsLoadingInitialChatHistory(true);
                setChatHistory([]);
                const initialPage = await fetchPage(threadId, undefined);
                if (initialPage && initialPage.length > 0) {
                    prepareForAddingChatHistory();
                    setChatHistory([initialPage]);
                }
                setNewestMessageTimestampInOldMessages(initialPage?.[initialPage.length - 1]?.timeStamp || '');
                setIsLoadingInitialChatHistory(false);
            };

            loadInitialPage();
        } else {
            setNewestMessageTimestampInOldMessages('');
            setIsLoadingInitialChatHistory(false);
            setChatHistory([]);
            hasPreviousPage.current = false;
        }
    }, [threadId]);

    useEffect(() => {
        loadOlderMessagesRef.current = loadOlderMessages;
    }, [loadOlderMessages]);

    // If this is an incident thread, periodically refresh the latest 5 old messages to check for the progress
    useEffect(() => {
        let isSubscribed = true;
        let timeoutId: NodeJS.Timeout | undefined = undefined;

        if (isIncidentInvestigationInProgress && threadId && !isLoadingInitialChatHistory) {
            const refreshOldMessages = async () => {
                if (newestMessageTimestampInOldMessages) {
                    const [threadResponse, updatedOldMessagesResponse] = await Promise.all([
                        threadClient.getThread(threadId),
                        messageClient.getMessages(threadId, {
                            skip: 0,
                            top: 5,
                            descending: true,
                            maxTimestamp: newestMessageTimestampInOldMessages,
                            maxTimestampInclusive: true,
                        }),
                    ]);

                    const threadCompleted = threadResponse.isSuccessful && isIncidentThreadCompleted(threadResponse.content);
                    const updatedOldMessages = updatedOldMessagesResponse.content || [];

                    if (isSubscribed) {
                        if (threadCompleted) {
                            setIsIncidentInvestigationInProgress(false);
                        }
                        if (updatedOldMessagesResponse.isSuccessful && updatedOldMessages.length > 0) {
                            setChatHistory(prev => {
                                const lastPage = prev[prev.length - 1];
                                const updatedLastPage = updateOldMessagesText(
                                    lastPage,
                                    updatedOldMessages.map(convertMessageToChatMessage).reverse()
                                );

                                if (updatedLastPage && updatedLastPage !== lastPage) {
                                    return [...prev.slice(0, -1), updatedLastPage];
                                }
                                return prev;
                            });
                        }
                    }

                    timeoutId = setTimeout(refreshOldMessages, 10000);
                }
            };

            refreshOldMessages();
        }

        return () => {
            isSubscribed = false;
            clearTimeout(timeoutId);
        };
    }, [threadId, isLoadingInitialChatHistory, isIncidentInvestigationInProgress, newestMessageTimestampInOldMessages]);

    useEffect(() => {
        return () => {
            clearTimeout(timeout.current);
        };
    }, []);

    return {
        chatHistory,
        isLoadingInitialChatHistory,
        loadOlderMessagesRef,
        newestMessageTimestampInOldMessages,
    };
};
