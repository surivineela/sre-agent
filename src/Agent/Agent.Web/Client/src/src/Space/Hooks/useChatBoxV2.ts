import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { MessageRequestType, MessageResponseType, SREAgentUserId, StreamingMessage } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { PromptResources } from '../../Strings/SREAgentResources';
import {
    getIntervalBetweenLoading,
    getStreamingMessageText,
    getToolCallText,
    isChatMessageContentNonImageText,
    isChatMessageEmpty,
    isDefaultStreamingMessageType,
    isFinalStreamingMessage,
    isUserStreamingMessage,
    processChatMessageContents,
    shouldGroupWithPreviousMessageV2,
} from '../Activities/Utility';
import { ChatMessage, MessageTypingCharactersPer10Ms, MessageTypingSpeedInMilliseconds } from '../Contracts/Activities';
import { SignalRContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';
import { useChatHistoryDataCache } from './useChatHistoryDataCache';

const composeUserMessage = (userId: string, userDisplayName: string, message: string): ChatMessage => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: {
            role: 'User',
            userId: userId,
            displayName: userDisplayName,
        },
        contents: [{ text: message }],
    };
};

const composeDefaultStreamingMessage = (): ChatMessage => {
    return {
        id: Guid.newGuid(),
        timeStamp: new Date().toISOString(),
        author: {
            role: 'SREAgent',
            userId: SREAgentUserId,
            displayName: '',
        },
        contents: [],
    };
};

interface MessageCreateRequest {
    text: string;
    userId: string;
    displayName: string;
}

interface ThreadCreateRequest {
    startMessage: MessageCreateRequest;
}

export const useChatBoxV2 = (
    addThread: (threadId: string) => void,
    promoteThread: (threadId: string) => void,
    updateThreadLastReadTime: (threadId: string) => void,
    threadId?: string | null,
    threadSource?: string | null
) => {
    const intl = useIntl();
    const proxy = useContext(AzPortalContext);

    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [oldMessages, setOldMessages] = useState<ChatMessage[]>([]);
    const [newMessages, setNewMessages] = useState<ChatMessage[]>([]);
    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);

    const [streamingMessage, setStreamingMessage] = useState<ChatMessage | null>(null);
    const [isCancellingStreaming, setIsCancellingStreaming] = useState<boolean>(false);
    const [toolCallText, setToolCallText] = useState<string | null>(null);
    const [isAgentTyping, setIsAgentTyping] = useState<boolean>(false);
    const [isStreamingEmpty, setIsStreamingEmpty] = useState<boolean>(false);
    const messageChunkQueue = useRef<StreamingMessage[]>([]);
    const isTypingChars = useRef<boolean>(false);
    const typingCharIndex = useRef<number>(0);
    const typingCharsTimeout = useRef<NodeJS.Timeout | undefined>(undefined);

    const [downButtonState, setDownButtonState] = useState<{ visible: boolean; flash: boolean }>({ visible: false, flash: false });

    const { sendMessage, subscribeSignalR, unsubscribeSignalR } = useContext(SignalRContext);

    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const currentScrollTop = useRef<number>(0);
    const currentScrollHeight = useRef<number>(0);
    const oldMessagesToBeAdded = useRef<boolean>(false);
    const streamingMessageRef = useRef<ChatMessage | null>(null);
    const currentThreadIdRef = useRef<string>(threadId || '');
    const isNewThreadAdded = useRef<boolean>(false);
    // pass userDefinedThreadId to thread create for matching the thread id from the stream message
    const userDefinedThreadIdRef = useRef<string>(Guid.newGuid());
    const messagesRef = useRef<ChatMessage[]>([]);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const { oldMessagesQueryData, isLoadingInitialChatHistory, loadOlderMessages, hasMoreDataToLoad, isFetchingOlderMessages } =
        useChatHistoryDataCache(threadId, threadSource);

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
            loadOlderMessages();
        }

        const isAtBottom = isChatAtBottom();
        setDownButtonState({ visible: !isAtBottom, flash: isAgentTyping });
    }, 300);

    const onScroll = () => {
        const prevScrollTop = currentScrollTop.current;
        currentScrollTop.current = messagesDivRef.current?.scrollTop || 0;

        handleScroll(currentScrollTop.current < prevScrollTop);
    };

    const onClickDownButton = () => {
        scrollToBottom(false);
        setDownButtonState({ visible: false, flash: false });
    };

    const finishStreaming = () => {
        setIsAgentTyping(false);
        setIsCancellingStreaming(false);
        setToolCallText(null);
        messageChunkQueue.current = [];
        isTypingChars.current = false;
        typingCharIndex.current = 0;
        clearTimeout(typingCharsTimeout.current);
    };

    /**
     * @param newMessages messages in descending order by timeStamp
     */
    const handleNewMessages = (newMessages: ChatMessage[]) => {
        oldMessagesToBeAdded.current = false;
        setNewMessages(prev => [...prev, ...newMessages]);
    };

    const createThread = (threadId: string, threadCreateRequest: ThreadCreateRequest) => {
        sendMessage(MessageRequestType.CreateThread, threadId, threadCreateRequest, false);
    };

    const createMessage = (threadId: string, messageCreateRequest: MessageCreateRequest) => {
        sendMessage(MessageRequestType.CreateMessage, threadId, messageCreateRequest, false);
    };

    const cancelStreaming = useCallback(() => {
        setIsCancellingStreaming(true);
    }, []);

    useEffect(() => {
        if (isCancellingStreaming && currentThreadId) {
            sendMessage(MessageRequestType.CancelThread, currentThreadId);
        }
    }, [isCancellingStreaming, currentThreadId, sendMessage]);

    const getGroupedChatMessages = useCallback((messageId: string): ChatMessage[] => {
        const currentMessageIndex = messagesRef.current.findIndex(msg => msg.id === messageId);

        if (currentMessageIndex < 0 || currentMessageIndex >= messagesRef.current.length) {
            return [];
        }

        const currentMessage = messagesRef.current[currentMessageIndex];
        const groupedMessages: ChatMessage[] = [currentMessage];

        for (let i = currentMessageIndex - 1; i >= 0; i--) {
            const previousMessage = messagesRef.current[i];
            if (shouldGroupWithPreviousMessageV2(currentMessage, previousMessage)) {
                groupedMessages.unshift(previousMessage);
            } else {
                break;
            }
        }

        return groupedMessages;
    }, []);

    const sendMessageHandler = useCallback(
        async (message: string) => {
            const currentStreamingMessage = streamingMessageRef.current;
            setStreamingMessage(null);
            const messagesToAdd: ChatMessage[] = [];
            if (currentStreamingMessage && !isChatMessageEmpty(currentStreamingMessage)) {
                messagesToAdd.push({ ...currentStreamingMessage });
            }
            const userMessage = composeUserMessage(userId, displayName, message);
            messagesToAdd.push(userMessage);

            handleNewMessages(messagesToAdd);
            setStreamingMessage(composeDefaultStreamingMessage());
            setIsAgentTyping(true);
            setIsStreamingEmpty(true);

            try {
                const messageRequest: MessageCreateRequest = {
                    text: message,
                    userId,
                    displayName,
                };
                //ToDo: Handle errors of sendMessage, createThread and pollResponses
                if (currentThreadId) {
                    // Issue a request to create a new message in the current thread
                    createMessage(currentThreadId, messageRequest);
                    // Keep it for now for testing purpose. Will remove it once the streaming is not behind the feature flag
                    console.log(`New message sent in thread: ${currentThreadId}. Message: ${message}.`);
                } else {
                    // Issue a request to create a new thread
                    userDefinedThreadIdRef.current = Guid.newGuid();
                    createThread(userDefinedThreadIdRef.current, {
                        startMessage: messageRequest,
                    });
                    // Keep it for now for testing purpose. Will remove it once the streaming is not behind the feature flag
                    console.log(`New thread is created. Message: ${message}.`);
                }
            } catch (e) {
                proxy.log({
                    logLevel: 'verbose',
                    action: 'sendMessage',
                    actionModifier: 'error',
                    data: `Failed to send message: ${e}`,
                });
            }
        },
        [userId, displayName, currentThreadId, proxy.log]
    );

    useEffect(() => {
        streamingMessageRef.current = streamingMessage;
    }, [streamingMessage]);

    useEffect(() => {
        let isSubscribed = true;

        const handleMessageTyping = () => {
            if (messageChunkQueue.current.length === 0) {
                isTypingChars.current = false;
                return;
            }

            const handleCompletedMessageChunk = (messageChunk: StreamingMessage) => {
                messageChunkQueue.current.shift();
                if (isFinalStreamingMessage(messageChunk)) {
                    finishStreaming();
                } else {
                    handleMessageTyping();
                }
            };

            isTypingChars.current = true;
            typingCharIndex.current = 0;
            const currentMessageChunk = messageChunkQueue.current[0];
            const currentMessageText = getStreamingMessageText(currentMessageChunk);
            const currentToolCallText = getToolCallText(currentMessageChunk);

            setToolCallText(isCancellingStreaming ? null : currentToolCallText);

            if (currentMessageText && !isCancellingStreaming) {
                if (isDefaultStreamingMessageType(currentMessageChunk)) {
                    const typeChar = () => {
                        if (typingCharIndex.current < currentMessageText.length && !isCancellingStreaming) {
                            const charIndex = typingCharIndex.current;
                            typingCharIndex.current += MessageTypingCharactersPer10Ms;
                            const newText = currentMessageText.slice(
                                charIndex,
                                Math.min(charIndex + MessageTypingCharactersPer10Ms, currentMessageText.length)
                            );

                            setStreamingMessage(prev => {
                                const newStreamingMessage = prev ? { ...prev } : composeDefaultStreamingMessage();
                                const latestContent = newStreamingMessage.contents[newStreamingMessage.contents.length - 1];

                                if (charIndex !== 0 && latestContent && isChatMessageContentNonImageText(latestContent)) {
                                    // Append text to the lastest text messsage when
                                    const updatedLatestContent = {
                                        ...latestContent,
                                        text: latestContent.text + newText,
                                    };
                                    return {
                                        ...newStreamingMessage,
                                        contents: [...newStreamingMessage.contents.slice(0, -1), updatedLatestContent],
                                    };
                                } else {
                                    // Start a new content
                                    return {
                                        ...newStreamingMessage,
                                        contents: [
                                            ...newStreamingMessage.contents,
                                            {
                                                text: newText,
                                            },
                                        ],
                                    };
                                }
                            });
                            typingCharsTimeout.current = setTimeout(() => typeChar(), MessageTypingSpeedInMilliseconds);
                        } else {
                            handleCompletedMessageChunk(currentMessageChunk);
                        }
                    };
                    typeChar();
                } else {
                    setStreamingMessage(prev => {
                        const newStreamingMessage = prev ? { ...prev } : composeDefaultStreamingMessage();
                        return {
                            ...newStreamingMessage,
                            contents: processChatMessageContents(newStreamingMessage.contents, currentMessageChunk),
                        };
                    });
                    handleCompletedMessageChunk(currentMessageChunk);
                }
            } else {
                handleCompletedMessageChunk(currentMessageChunk);
            }
        };

        const handleUserMessageChunk = (messageResponseType: MessageResponseType, streamingMessage: StreamingMessage) => {
            const { additionalProperties } = streamingMessage;
            const { threadId: threadIdFromStream } = additionalProperties || {};

            if (currentThreadIdRef.current) {
                promoteThread(currentThreadIdRef.current);
            } else if (
                threadIdFromStream &&
                equals(messageResponseType, MessageResponseType.ThreadUpdate, AntUxStringComparison.IgnoreCase)
            ) {
                setCurrentThreadId(threadIdFromStream);
                if (!isNewThreadAdded.current) {
                    addThread(threadIdFromStream);
                    isNewThreadAdded.current = true;
                }
            }

            if (isFinalStreamingMessage(streamingMessage)) {
                finishStreaming();
            }
        };

        const handleAgentMessageChunk = (messageChunk: StreamingMessage) => {
            messageChunkQueue.current.push(messageChunk);

            if (!isTypingChars.current) {
                handleMessageTyping();
            }
        };

        const handleMessageChunk = (messageResponseType: MessageResponseType, streamData?: StreamingMessage) => {
            if (streamData) {
                // Keep it for now for testing purpose. Will remove it once the streaming is not behind the feature flag
                console.log(
                    messageResponseType,
                    'Role: ',
                    streamData.role,
                    'Text: ',
                    streamData.contents?.[0]?.text,
                    'Text type: ',
                    streamData.additionalProperties?.streamMessageType,
                    'Tool call',
                    streamData.contents?.[0]?.name,
                    'isCancelled',
                    streamData.additionalProperties?.isCancelled,
                    'Finish Reason: ',
                    streamData.finishReason
                );

                if (isSubscribed) {
                    if (isUserStreamingMessage(streamData)) {
                        handleUserMessageChunk(messageResponseType, streamData);
                    } else {
                        setIsStreamingEmpty(false);
                        handleAgentMessageChunk(streamData);
                    }
                }
            }
        };

        const threadUpdateCallback = (streamData?: StreamingMessage) => {
            handleMessageChunk(MessageResponseType.ThreadUpdate, streamData);
        };

        const messageUpdateCallback = (streamData?: StreamingMessage) => {
            handleMessageChunk(MessageResponseType.MessageUpdate, streamData);
        };

        subscribeSignalR(MessageResponseType.ThreadUpdate, threadUpdateCallback);
        subscribeSignalR(MessageResponseType.MessageUpdate, messageUpdateCallback);

        return () => {
            isSubscribed = false;
            unsubscribeSignalR(MessageResponseType.ThreadUpdate, threadUpdateCallback);
            unsubscribeSignalR(MessageResponseType.MessageUpdate, messageUpdateCallback);
        };
    }, [subscribeSignalR, unsubscribeSignalR, addThread, promoteThread, isCancellingStreaming]);

    useEffect(() => {
        if (threadId) {
            updateThreadLastReadTime(threadId);
        }
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

        if (isIntersecting && hasMoreDataToLoad && !isFetchingOlderMessages) {
            let exponentialBackoffDepth = -1;

            const loadOldMessages = async () => {
                const result = await loadOlderMessages();

                exponentialBackoffDepth = result?.isLoadingError || result?.isFetchNextPageError ? exponentialBackoffDepth + 1 : -1;
                const interval = getIntervalBetweenLoading(exponentialBackoffDepth);

                timeoutId = setTimeout(loadOldMessages, interval);
            };

            loadOldMessages();
        }

        return () => {
            clearTimeout(timeoutId);
        };
    }, [hasMoreDataToLoad, isFetchingOlderMessages, isIntersecting, loadOlderMessages]);

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
        const result: ChatMessage[] = [];
        const seenTexts = new Set<string>();

        for (let i = messages.length - 1; i >= 0 && result.length < 3; i--) {
            const msg = messages[i];
            const text = msg.contents[0]?.text;
            if (text && msg.author.role !== 'SREAgent' && !seenTexts.has(text)) {
                result.push(msg);
                seenTexts.add(text);
            }
        }
        return result.map(message => {
            return message.contents[0].text || '';
        });
    }, [messages]);

    useEffect(() => {
        messagesRef.current = messages;
    }, [messages]);

    useEffect(() => {
        // When new messages are added or the streaming message is initialized or set to null, scroll to the bottom
        if (!oldMessagesToBeAdded.current) {
            scrollToBottom(true);
        }
    }, [messages.length, !!streamingMessage]);

    useEffect(() => {
        if (streamingMessage && !isChatMessageEmpty(streamingMessage)) {
            if (!isChatAtBottom()) {
                setDownButtonState({ visible: true, flash: isAgentTyping });
            } else {
                setDownButtonState({ visible: false, flash: false });
                scrollToBottom(true);
            }
        }
    }, [streamingMessage, isAgentTyping]);

    useEffect(() => {
        currentThreadIdRef.current = currentThreadId || '';
    }, [currentThreadId]);

    // Record the last read time and cache all the old messages when the component is unmounted
    useEffect(() => {
        return () => {
            if (currentThreadIdRef.current) {
                updateThreadLastReadTime(currentThreadIdRef.current);
            }
        };
    }, []);

    useEffect(() => {
        const data = [...(oldMessagesQueryData?.pages?.flat() || []).filter(message => !!message)].reverse();

        if (data.length > 0) {
            oldMessagesToBeAdded.current = true;
            currentScrollHeight.current = messagesDivRef.current?.scrollHeight || 0;
            setOldMessages(data);
        }
    }, [oldMessagesQueryData]);

    useEffect(() => {
        setMessages([...oldMessages, ...newMessages]);
    }, [oldMessages, newMessages]);

    return {
        messages,
        isLoadingInitialChatHistory,
        isAgentTyping,
        isStreamingEmpty,
        streamingMessage,
        toolCallText,
        isCancellingStreaming,
        sendMessage: sendMessageHandler,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelStreaming,
        prompts,
        messagePromptsUsed,
        onScroll,
        downButtonState,
        onClickDownButton,
        getGroupedChatMessages,
    };
};
