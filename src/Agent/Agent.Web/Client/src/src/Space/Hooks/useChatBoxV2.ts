import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import { Message, SREAgentUserId, StreamingMessage } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { PromptResources } from '../../Strings/SREAgentResources';
import { getIntervalBetweenLoading, processOldMessages } from '../Activities/Utility';
import { MessageLoadingCounts } from '../Contracts/Activities';
import { WebSocketContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';

const composeUserMessage = (userId: string, userDisplayName: string, message: string): Message => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: {
            role: 'User',
            userId: userId,
            displayName: userDisplayName,
        },
        text: message,
    };
};

const composeDefaultStreamingMessage = (): Message => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: {
            role: 'SREAgent',
            userId: SREAgentUserId,
            displayName: '',
        },
        text: '',
    };
};

export const useChatBoxV2 = (
    addThread: (threadId: string) => void,
    promoteThread: (threadId: string) => void,
    threadId?: string | null,
    _?: string | null
) => {
    const intl = useIntl();

    const [messages, setMessages] = useState<Message[]>([]);
    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [noChatHistoryLeftToLoad, setNoChatHistoryLeftToLoad] = useState<boolean>(false);
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);

    const [streamingMessage, setStreamingMessage] = useState<Message | null>(null);
    const [streamId, setStreamId] = useState<string>('');
    const [isAgentTyping, setIsAgentTyping] = useState<boolean>(false);

    const [showDownButton, setShowDownButton] = useState(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { sendMessage, addMessageListener } = useContext(WebSocketContext);

    const isMounted = useRef(true);
    const isPreviousOldMessagesLoadingCompleted = useRef(true);
    const oldestMessageRef = useRef<Message>();
    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const loadOldChatHistoryCallId = useRef<number>(0);
    const currentScrollTop = useRef<number>(0);
    const currentScrollHeight = useRef<number>(0);
    const oldMessagesToBeAdded = useRef<boolean>(false);
    const streamingMessageRef = useRef<Message | null>(null);
    const currentThreadIdRef = useRef<string | null>(threadId || null);

    const messageClient = MessageClient.getInstance(sreAgentEndpoint);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const isNewAndCleanThread = useMemo(
        () => !isLoadingInitialChatHistory && !currentThreadId && messages.length === 0,
        [isLoadingInitialChatHistory, currentThreadId, messages]
    );

    const scrollToBottom = (smooth: boolean) =>
        messagesDivRef.current?.scrollTo({ top: messagesDivRef.current.scrollHeight, behavior: smooth ? 'smooth' : undefined });

    const isChatAtBottom = () =>
        messagesDivRef.current &&
        messagesDivRef.current.scrollHeight - messagesDivRef.current.offsetHeight - messagesDivRef.current.scrollTop <= 2;

    const handleScroll = debounce((isScrollingToTop: boolean) => {
        if (isScrollingToTop) {
            loadOldChatHistory();
        }

        const isAtBottom = isChatAtBottom();
        setShowDownButton(!isAtBottom);
    }, 300);

    const onScroll = () => {
        const prevScrollTop = currentScrollTop.current;
        currentScrollTop.current = messagesDivRef.current?.scrollTop || 0;

        handleScroll(currentScrollTop.current < prevScrollTop);
    };

    const onClickDownButton = () => {
        scrollToBottom(false);
        setShowDownButton(false);
    };

    const cancelStreaming = useCallback(() => {
        setStreamId(Guid.newGuid());
        setIsAgentTyping(false);
        setStreamingMessage(prev => {
            if (prev) {
                return {
                    ...prev,
                    toolCallText: '',
                };
            }
            return prev;
        });
    }, []);

    /**
     * @param newMessages messages in descending order by timeStamp
     */
    const handleNewMessages = (newMessages: Message[]) => {
        oldMessagesToBeAdded.current = false;
        setMessages(prev => [...prev, ...newMessages]);
    };

    const handleOldMessages = (oldMessages: Message[], isInitialMessages: boolean) => {
        oldMessagesToBeAdded.current = true;
        currentScrollHeight.current = messagesDivRef.current?.scrollHeight || 0;
        if (isInitialMessages) {
            setMessages(processOldMessages([], oldMessages));
        } else {
            setMessages(prev => processOldMessages(prev, oldMessages));
        }
    };

    const sendMessageHandler = useCallback(
        async (message: string) => {
            const newStreamId = Guid.newGuid();
            setStreamId(newStreamId);

            const currentStreamingMessage = streamingMessageRef.current;
            setStreamingMessage(null);
            const messagesToAdd: Message[] = [];
            if (currentStreamingMessage?.text) {
                messagesToAdd.push({ ...currentStreamingMessage });
            }
            const userMessage = composeUserMessage(userId, displayName, message);
            messagesToAdd.push(userMessage);

            handleNewMessages(messagesToAdd);
            setStreamingMessage(composeDefaultStreamingMessage());
            setIsAgentTyping(true);

            try {
                //ToDo: Handle errors of sendMessage, createThread and pollResponses
                if (currentThreadId) {
                    const content = {
                        text: message,
                        role: 'User',
                        displayName: displayName,
                        userId: userId,
                    };
                    const contentString = JSON.stringify(content);
                    const requestBody = {
                        streamId: newStreamId,
                        threadId: currentThreadId,
                        role: 'user',
                        messageType: 'CreateMessage',
                        content: contentString,
                    };

                    const data = JSON.stringify(requestBody);
                    sendMessage(data);
                    console.log(data);
                } else {
                    // issue a request to create a new thread
                    const content = {
                        startMessage: {
                            text: message,
                            displayName: displayName,
                            userId: userId,
                        },
                    };
                    const contentString = JSON.stringify(content);
                    const requestBody = {
                        streamId: newStreamId,
                        role: 'user',
                        messageType: 'CreateThread',
                        content: contentString,
                        source: 'Conversation',
                    };

                    const data = JSON.stringify(requestBody);
                    sendMessage(data);
                    console.log(data);
                }
            } catch {
                //Handle error if it is not abort error
            }
        },
        [currentThreadId, userId, displayName, sendMessage]
    );

    const loadOldChatHistory = useCallback(async (): Promise<boolean | undefined> => {
        if (currentThreadId && oldestMessageRef.current && isPreviousOldMessagesLoadingCompleted.current && !noChatHistoryLeftToLoad) {
            isPreviousOldMessagesLoadingCompleted.current = false;
            const callId = loadOldChatHistoryCallId.current;

            const currentMessagesResponse = await messageClient.getMessages(currentThreadId, {
                skip: 0,
                top: MessageLoadingCounts.active,
                descending: true,
                maxTimestamp: oldestMessageRef.current.timeStamp,
            });

            if (callId === loadOldChatHistoryCallId.current) {
                const currentMessages = currentMessagesResponse.content || [];
                handleOldMessages(currentMessages, false);
                if (currentMessagesResponse.isSuccessful && currentMessages.length < MessageLoadingCounts.active) {
                    setNoChatHistoryLeftToLoad(true);
                }
                isPreviousOldMessagesLoadingCompleted.current = true;
                return currentMessagesResponse.isSuccessful;
            } else {
                isPreviousOldMessagesLoadingCompleted.current = true;
                return undefined;
            }
        }
    }, [currentThreadId, noChatHistoryLeftToLoad]);

    useEffect(() => {
        loadOldChatHistoryCallId.current += 1;
    }, [currentThreadId, noChatHistoryLeftToLoad]);

    useEffect(() => {
        streamingMessageRef.current = streamingMessage;
    }, [streamingMessage]);

    useEffect(() => {
        let isSubscribed = true;

        const streamHandler = (e: MessageEvent<any>) => {
            try {
                const data = JSON.parse(e.data) as StreamingMessage;
                const message = data?.Contents?.[0];

                console.log(data);

                const id = data?.AdditionalProperties?.messageId;
                const threadIdFromStream = data?.AdditionalProperties?.threadId;
                const currentStreamId = data?.AdditionalProperties?.streamId;
                const role = data?.Role;
                const shouldStopStreaming =
                    equals(data.FinishReason || '', 'stop', AntUxStringComparison.IgnoreCase) ||
                    equals(data.FinishReason || '', 'length', AntUxStringComparison.IgnoreCase);
                const createdAt = data?.CreatedAt ?? new Date().toISOString();
                const isUserMessage = equals(role || '', 'user', AntUxStringComparison.IgnoreCase);

                if (streamId === currentStreamId && isSubscribed) {
                    if (isUserMessage) {
                        if (currentThreadIdRef.current) {
                            promoteThread(currentThreadIdRef.current);
                        } else {
                            setCurrentThreadId(prev => {
                                if (prev || !threadIdFromStream) {
                                    return prev;
                                }

                                return threadIdFromStream;
                            });
                            if (threadIdFromStream) {
                                addThread(threadIdFromStream);
                            }
                        }
                    } else {
                        setStreamingMessage(prev => {
                            const prevText = prev?.text || '';
                            const newText = message?.Text || '';
                            const updatedText = prevText + newText;
                            const isToolCall = equals(message?.$type ?? '', 'functionCall', AntUxStringComparison.IgnoreCase);
                            const AdditionalProperties = message?.AdditionalProperties;
                            const toolCallDescription = isToolCall
                                ? AdditionalProperties?.userDescription || AdditionalProperties?.functionCallDescription || ''
                                : '';

                            const toolCallText = toolCallDescription || (updatedText ? '' : 'Analyzing...');

                            const updatedStreamingMessage: Message = {
                                id: id ?? prev?.id ?? '',
                                timeStamp: createdAt,
                                text: updatedText,
                                toolCallText,
                                author: {
                                    role: 'SREAgent',
                                    userId: SREAgentUserId,
                                    displayName: '',
                                },
                            };

                            return updatedStreamingMessage;
                        });
                    }

                    if (shouldStopStreaming) {
                        setIsAgentTyping(false);
                        setStreamingMessage(prev => {
                            if (prev) {
                                return {
                                    ...prev,
                                    toolCallText: '',
                                };
                            }
                            return prev;
                        });
                    }
                }
            } catch (error) {
                //log error
            }
        };

        addMessageListener(streamHandler);

        return () => {
            isSubscribed = false;
        };
    }, [addMessageListener, streamId, addThread, promoteThread]);

    // Load the latest 20 chat message history
    useEffect(() => {
        let isSubscribed = true;

        const loadLatest20ChatHistory = async () => {
            setStreamingMessage(null);
            if (threadId) {
                isPreviousOldMessagesLoadingCompleted.current = false;
                const messagesResponse = await messageClient.getMessages(threadId, {
                    skip: 0,
                    top: MessageLoadingCounts.default,
                    descending: true,
                });

                const messages = messagesResponse.content || [];

                if (isSubscribed) {
                    handleOldMessages(messages, true);
                    setIsLoadingInitialChatHistory(false);

                    // The threshold depends on the number of the messages this query is intended to return.
                    // if the top parameter for calling getMessages, the threshold should be changed accordingly
                    if (messagesResponse.isSuccessful && messages.length < MessageLoadingCounts.default) {
                        setNoChatHistoryLeftToLoad(true);
                    }
                }
                isPreviousOldMessagesLoadingCompleted.current = true;
            } else {
                setIsLoadingInitialChatHistory(false);
                setNoChatHistoryLeftToLoad(true);
            }
        };

        loadLatest20ChatHistory();

        return () => {
            isSubscribed = false;
        };
    }, [threadId]);

    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            setIsIntersecting(entry.isIntersecting);
        });
        if (observer && intersectionObserverRef.current && !isLoadingInitialChatHistory) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [isLoadingInitialChatHistory]);

    useEffect(() => {
        let timeoutId: NodeJS.Timeout | undefined = undefined;

        if (isIntersecting && !noChatHistoryLeftToLoad) {
            let exponentialBackoffDepth = -1;

            const loadOldMessages = async () => {
                const isSuccessful = await loadOldChatHistory();

                exponentialBackoffDepth = isSuccessful === false ? exponentialBackoffDepth + 1 : -1;
                const interval = getIntervalBetweenLoading(exponentialBackoffDepth);

                timeoutId = setTimeout(loadOldChatHistory, interval);
            };

            loadOldMessages();
        }

        return () => {
            clearTimeout(timeoutId);
        };
    }, [loadOldChatHistory, noChatHistoryLeftToLoad, isIntersecting]);

    // When old messages are added at the top of the chat, this useLayoutEffect will calculate the new scroll top
    // to make sure the chat does not scroll to top before the next paint
    useLayoutEffect(() => {
        let timeoutId: number | undefined = undefined;
        if (messagesDivRef.current && oldMessagesToBeAdded.current) {
            const prevScrollHeight = currentScrollHeight.current;
            const prevScrollTop = currentScrollTop.current;

            timeoutId = requestAnimationFrame(() => {
                if (messagesDivRef.current) {
                    const scrollHeight = messagesDivRef.current.scrollHeight;
                    messagesDivRef.current.scrollTop = prevScrollTop + scrollHeight - prevScrollHeight;
                }
            });
        }

        return () => {
            if (timeoutId !== undefined) {
                cancelAnimationFrame(timeoutId);
            }
        };
    }, [messages.length]);

    const prompts = useMemo(
        () => [
            intl.formatMessage(PromptResources.bestPracticesPrompt),
            intl.formatMessage(PromptResources.notWorkingPrompt),
            intl.formatMessage(PromptResources.availabilityPrompt),
        ],
        [intl]
    );

    const messagePromptsUsed = useMemo(() => {
        const result: Message[] = [];
        const seenTexts = new Set<string>();

        for (let i = messages.length - 1; i >= 0 && result.length < 3; i--) {
            const msg = messages[i];
            if (msg.author.role !== 'SREAgent' && !seenTexts.has(msg.text)) {
                result.unshift(msg);
                seenTexts.add(msg.text);
            }
        }
        return result.map(message => {
            return message.text;
        });
    }, [messages]);

    useEffect(() => {
        // When new messages are added or the streaming message is initialized or set to null, scroll to the bottom
        if (!oldMessagesToBeAdded.current) {
            scrollToBottom(true);
        }
    }, [messages.length, !!streamingMessage]);

    const newMessage = useMemo(() => {
        return streamingMessage?.text || '';
    }, [streamingMessage]);

    useEffect(() => {
        if (newMessage && newMessage.length > 0 && !isChatAtBottom()) {
            setShowDownButton(true);
        }
    }, [newMessage]);

    useEffect(() => {
        oldestMessageRef.current = messages[0];
    }, [messages]);

    useEffect(() => {
        isMounted.current = true;

        return () => {
            isMounted.current = false;
        };
    }, []);

    return {
        messages,
        isLoadingInitialChatHistory,
        noChatHistoryLeftToLoad,
        isAgentTyping,
        streamingMessage,
        sendMessage: sendMessageHandler,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelStreaming,
        prompts,
        messagePromptsUsed,
        onScroll,
        showDownButton,
        onClickDownButton,
        loadOldChatHistory,
    };
};
