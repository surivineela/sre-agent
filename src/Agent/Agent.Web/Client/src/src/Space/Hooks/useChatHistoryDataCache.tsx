import { useInfiniteQuery, useQueryClient } from '@tanstack/react-query';
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

const ChatMessagesQueryIdPrefix = 'usechatboxv2-chat-messages';

export const useChatHistoryDataCache = (
    threadId: string | null | undefined,
    userDefinedThreadId: string | null | undefined,
    threadSource: string | null | undefined,
    prepareForAddingChatHistory: () => void
) => {
    const [isIncidentInvestigationInProgress, setIsIncidentInvestigationInProgress] = useState<boolean>(
        threadSource === ThreadSource.incident
    );
    const [newestMessageTimestampInOldMessages, setNewestMessageTimestampInOldMessages] = useState<string>('');

    const exponentialBackoffDepth = useRef(-1);
    const timeout = useRef<NodeJS.Timeout | undefined>(undefined);

    const loadOlderMessagesRef = useRef<(() => void) | null>(null);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
    const messageClient = MessageClient.getInstance(sreAgentEndpoint);

    const queryClient = useQueryClient();

    const { data, isLoading, isFetching, isFetchingPreviousPage, hasPreviousPage, fetchPreviousPage } = useInfiniteQuery({
        queryKey: [ChatMessagesQueryIdPrefix, threadId || userDefinedThreadId],
        enabled: !!threadId || !!userDefinedThreadId,
        queryFn: async ({ pageParam }) => {
            if (threadId) {
                const messagesResponse = await messageClient.getMessages(threadId, {
                    skip: 0,
                    top: MessageLoadingCounts.default,
                    descending: true,
                    maxTimestamp: pageParam || undefined,
                });

                if (messagesResponse.isSuccessful) {
                    return (messagesResponse.content || []).map(convertMessageToChatMessage).reverse();
                } else {
                    // This will trigger retry and prevent adding empty page to the cache.
                    throw new Error(`Failed to load messages for thread ${threadId}`);
                }
            } else {
                // Cache an empty array of chat history for new thread
                return [];
            }
        },
        getNextPageParam: lastPage => {
            return lastPage?.[lastPage.length - 1]?.timeStamp || undefined;
        },
        getPreviousPageParam: firstPage => {
            return firstPage?.[0]?.timeStamp || undefined;
        },
        initialPageParam: '',
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
        staleTime: Infinity,
        gcTime: Infinity,
        select: data => {
            prepareForAddingChatHistory();
            return data.pages.filter(page => page !== undefined && page.length > 0);
        },
    });

    const isLoadingInitialChatHistory = useMemo(() => {
        return !!threadId && isLoading;
    }, [threadId, isLoading]);

    const loadOlderMessages = useCallback(() => {
        if (isLoadingInitialChatHistory || isFetching || isFetchingPreviousPage || !hasPreviousPage) {
            return;
        }

        const interval = exponentialBackoffDepth.current < 0 ? 0 : getIntervalBetweenLoading(exponentialBackoffDepth.current);

        timeout.current = setTimeout(async () => {
            const result = await fetchPreviousPage();
            if (result.isError || result.isFetchPreviousPageError) {
                exponentialBackoffDepth.current += 1;
            } else {
                exponentialBackoffDepth.current = -1; // Reset on successful fetch
            }
        }, interval);
    }, [isLoadingInitialChatHistory, isFetching, isFetchingPreviousPage, hasPreviousPage, fetchPreviousPage]);

    useEffect(() => {
        loadOlderMessagesRef.current = loadOlderMessages;
    }, [loadOlderMessages]);

    useEffect(() => {
        const lastPage = data?.[data.length - 1];
        const newestMessage = lastPage?.[lastPage.length - 1];
        setNewestMessageTimestampInOldMessages(newestMessage?.timeStamp || '');
    }, [data]);

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
                            queryClient.setQueryData<ChatMessage[][] | undefined>([ChatMessagesQueryIdPrefix, threadId], oldData => {
                                if (oldData && oldData.length > 0) {
                                    const lastPage = oldData[oldData.length - 1];
                                    const updatedLastPage = updateOldMessagesText(
                                        lastPage,
                                        updatedOldMessages.map(convertMessageToChatMessage).reverse()
                                    );
                                    if (updatedLastPage === lastPage) {
                                        return oldData;
                                    } else if (updatedLastPage) {
                                        return [...oldData.slice(0, -1), updatedLastPage];
                                    }
                                }
                                return oldData;
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
        chatHistory: data,
        isLoadingInitialChatHistory,
        loadOlderMessagesRef,
    };
};
