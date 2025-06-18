import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import { ThreadClient } from '../../Common/Clients/ThreadClient';
import {
    Message,
    MessageRequestType,
    MessageResponseType,
    SREAgentUserId,
    StreamingMessage,
    ThreadSource,
} from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { PromptResources } from '../../Strings/SREAgentResources';
import {
    getIntervalBetweenLoading,
    getStreamingMessageText,
    isDefaultStreamingMessageType,
    isFinalStreamingMessage,
    isIncidentThreadCompleted,
    processOldMessages,
    processStreamingMessage,
    updateOldMessagesText,
} from '../Activities/Utility';
import { MessageLoadingCounts, MessageTypingCharactersPer10Ms, MessageTypingSpeedInMilliseconds } from '../Contracts/Activities';
import { SignalRContext } from '../Contracts/Context';
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

    const [messages, setMessages] = useState<Message[]>([]);
    const [oldMessages, setOldMessages] = useState<Message[]>([]);
    const [newMessages, setNewMessages] = useState<Message[]>([]);
    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [noChatHistoryLeftToLoad, setNoChatHistoryLeftToLoad] = useState<boolean>(false);
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const [isIncidentInvestigationInProgress, setIsIncidentInvestigationInProgress] = useState<boolean>(
        threadSource === ThreadSource.incident
    );

    const [streamingMessage, setStreamingMessage] = useState<Message | null>(null);
    const [stopReceivingStreamingMessages, setStopReceivingStreamingMessages] = useState<boolean>(false);
    const [isAgentTyping, setIsAgentTyping] = useState<boolean>(false);
    const messageChunkQueue = useRef<StreamingMessage[]>([]);
    const isTypingChars = useRef<boolean>(false);
    const typingCharIndex = useRef<number>(0);
    const typingCharsTimeout = useRef<NodeJS.Timeout | undefined>(undefined);

    const [downButtonState, setDownButtonState] = useState<{ visible: boolean; flash: boolean }>({ visible: false, flash: false });

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { sendMessage, onMessage } = useContext(SignalRContext);

    const isPreviousOldMessagesLoadingCompleted = useRef(true);
    const oldestMessageRef = useRef<Message>();
    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const loadOldChatHistoryCallId = useRef<number>(0);
    const currentScrollTop = useRef<number>(0);
    const currentScrollHeight = useRef<number>(0);
    const oldMessagesToBeAdded = useRef<boolean>(false);
    const streamingMessageRef = useRef<Message | null>(null);
    const currentThreadIdRef = useRef<string>(threadId || '');
    const isNewThreadAdded = useRef<boolean>(false);

    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);
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
        messageChunkQueue.current = [];
        isTypingChars.current = false;
        typingCharIndex.current = 0;
        clearTimeout(typingCharsTimeout.current);
    };

    const cancelStreaming = useCallback(() => {
        //ToDo: Cancel the streaming message by sending a cancellation token (API is not ready yet)
        setStopReceivingStreamingMessages(true);
        finishStreaming();
    }, []);

    /**
     * @param newMessages messages in descending order by timeStamp
     */
    const handleNewMessages = (newMessages: Message[]) => {
        oldMessagesToBeAdded.current = false;
        setNewMessages(prev => [...prev, ...newMessages]);
    };

    const handleOldMessages = (oldMessages: Message[], isInitialMessages: boolean) => {
        oldMessagesToBeAdded.current = true;
        currentScrollHeight.current = messagesDivRef.current?.scrollHeight || 0;
        if (isInitialMessages) {
            setOldMessages(processOldMessages([], oldMessages));
        } else {
            setOldMessages(prev => processOldMessages(prev, oldMessages));
        }
    };

    const createThread = (threadCreateRequest: ThreadCreateRequest) => {
        sendMessage(MessageRequestType.CreateThread, threadCreateRequest, false);
    };

    const createMessage = (threadId: string, messageCreateRequest: MessageCreateRequest) => {
        sendMessage(MessageRequestType.CreateMessage, threadId, messageCreateRequest, false);
    };

    const sendMessageHandler = useCallback(
        async (message: string) => {
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
            setStopReceivingStreamingMessages(false);

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
                    console.log(`New message sent in thread: ${currentThreadId}. Message: ${message}.`);
                } else {
                    // Issue a request to create a new thread
                    createThread({
                        startMessage: messageRequest,
                    });
                    console.log(`New thread created with message: ${message}.`);
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

        const handleMessageTyping = () => {
            if (messageChunkQueue.current.length === 0) {
                isTypingChars.current = false;
                return;
            }

            isTypingChars.current = true;
            typingCharIndex.current = 0;
            const currentMessageChunk = messageChunkQueue.current[0];
            const currentMessageText = getStreamingMessageText(currentMessageChunk);

            if (currentMessageText && isDefaultStreamingMessageType(currentMessageChunk)) {
                // Update the streaming message with the current chunk properties except the text
                setStreamingMessage(prev => processStreamingMessage(prev, currentMessageChunk, true));
                // Update the streaming message's text by appending a character every 50ms
                const typeChar = () => {
                    if (typingCharIndex.current < currentMessageText.length) {
                        const charIndex = typingCharIndex.current;
                        typingCharIndex.current += MessageTypingCharactersPer10Ms;
                        setStreamingMessage(prev => {
                            if (prev) {
                                return {
                                    ...prev,
                                    text:
                                        prev.text +
                                        currentMessageText.slice(
                                            charIndex,
                                            Math.min(charIndex + MessageTypingCharactersPer10Ms, currentMessageText.length)
                                        ),
                                };
                            }
                            return prev;
                        });
                        typingCharsTimeout.current = setTimeout(typeChar, MessageTypingSpeedInMilliseconds);
                    } else {
                        // Finished typing the current message chunk
                        messageChunkQueue.current.shift();
                        if (isFinalStreamingMessage(currentMessageChunk)) {
                            finishStreaming();
                        } else {
                            handleMessageTyping();
                        }
                    }
                };
                typeChar();
            } else {
                setStreamingMessage(prev => processStreamingMessage(prev, currentMessageChunk));
                messageChunkQueue.current.shift();
                if (isFinalStreamingMessage(currentMessageChunk)) {
                    finishStreaming();
                } else {
                    handleMessageTyping();
                }
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
                console.log(messageResponseType + ' received: ', streamData);

                const isUserMessage = equals(streamData.role || '', 'user', AntUxStringComparison.IgnoreCase);

                if (isSubscribed && !stopReceivingStreamingMessages) {
                    if (isUserMessage) {
                        handleUserMessageChunk(messageResponseType, streamData);
                    } else {
                        handleAgentMessageChunk(streamData);
                    }
                }
            }
        };

        onMessage(MessageResponseType.ThreadUpdate, (streamData?: StreamingMessage) => {
            handleMessageChunk(MessageResponseType.ThreadUpdate, streamData);
        });

        onMessage(MessageResponseType.MessageUpdate, (streamData?: StreamingMessage) => {
            handleMessageChunk(MessageResponseType.MessageUpdate, streamData);
        });

        return () => {
            isSubscribed = false;
        };
    }, [onMessage, addThread, promoteThread, stopReceivingStreamingMessages]);

    // Load the latest 20 chat message history
    useEffect(() => {
        let isSubscribed = true;

        const loadLatest20ChatHistory = async () => {
            setStreamingMessage(null);
            if (threadId) {
                isPreviousOldMessagesLoadingCompleted.current = false;
                updateThreadLastReadTime(threadId);
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

    const newestMessageTimestampInOldMessages = useMemo(() => {
        return oldMessages[oldMessages.length - 1]?.timeStamp || '';
    }, [oldMessages]);

    // If this is an incident thread, periodically refresh the latest 10 old messages to check for the progress
    useEffect(() => {
        let isSubscribed = true;
        let timeoutId: NodeJS.Timeout | undefined = undefined;

        if (isIncidentInvestigationInProgress && currentThreadId && !isLoadingInitialChatHistory) {
            const refreshOldMessages = async () => {
                if (newestMessageTimestampInOldMessages) {
                    const [threadResponse, updatedOldMessagesResponse] = await Promise.all([
                        threadClient.getThread(currentThreadId),
                        messageClient.getMessages(currentThreadId, {
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
                            setOldMessages(prev => updateOldMessagesText(prev, updatedOldMessages));
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
    }, [currentThreadId, isLoadingInitialChatHistory, isIncidentInvestigationInProgress, newestMessageTimestampInOldMessages]);

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
                result.push(msg);
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
        if (newMessage && newMessage.length > 0) {
            if (!isChatAtBottom()) {
                setDownButtonState({ visible: true, flash: isAgentTyping });
            } else {
                setDownButtonState({ visible: false, flash: false });
                scrollToBottom(true);
            }
        }
    }, [newMessage, isAgentTyping]);

    useEffect(() => {
        oldestMessageRef.current = oldMessages[0];
    }, [oldMessages]);

    useEffect(() => {
        currentThreadIdRef.current = currentThreadId || '';
    }, [currentThreadId]);

    // Record the last read time when the component is unmounted
    useEffect(() => {
        return () => {
            if (currentThreadIdRef.current) {
                updateThreadLastReadTime(currentThreadIdRef.current);
            }
        };
    }, []);

    useEffect(() => {
        setMessages([...oldMessages, ...newMessages]);
    }, [oldMessages, newMessages]);

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
        downButtonState,
        onClickDownButton,
        loadOldChatHistory,
    };
};
