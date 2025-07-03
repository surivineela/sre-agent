import cloneDeep from 'lodash/cloneDeep';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { MessageRequestType, StreamingMessage } from '../../Common/Contracts/Azure/Streaming';
import { Guid } from '../../Common/Helpers/Guid';
import { PromptResources } from '../../Strings/SREAgentResources';
import {
    composeDefaultStreamingMessage,
    composeUserMessage,
    constructUserMessageFromStreamingMessage,
    convertStreamingMessagesToChatMessages,
    getStreamingMessageText,
    getToolCallText,
    isAgentMessage,
    isChatMessageContentNonImageText,
    isChatMessageEmpty,
    isDefaultStreamingMessageType,
    isFinalStreamingMessage,
    isUserStreamingMessage,
    processChatMessageContents,
    shouldGroupWithPreviousMessageV2,
} from '../Activities/Utility';
import { ChatMessage, MessageTypingCharactersPer10Ms, MessageTypingSpeedInMilliseconds } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';
import { useChatHistoryDataCache } from './useChatHistoryDataCache';

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
    const { sendMessage, subscribeChatStreaming, deleteStreamingMessages: deleteExistingStreamingMessages } = useContext(StreamingContext);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [allMessages, setAllMessages] = useState<ChatMessage[]>([]);
    const [chatMessagesFromExistingStreamingMessages, setChatMessagesFromExistingStreamingMessages] = useState<ChatMessage[]>([]);
    const [newMessages, setNewMessages] = useState<ChatMessage[]>([]);
    const [existingStreamingMessages, setExistingStreamingMessages] = useState<StreamingMessage[]>([]);
    const [ignoreExistingStreamingMessages, setIgnoreExistingStreamingMessages] = useState<boolean>(false);

    const [temporaryUserMessage, setTemporaryUserMessage] = useState<ChatMessage | null>(null);
    const [streamingMessage, setStreamingMessage] = useState<ChatMessage | null>(null);
    const [isCancellingStreaming, setIsCancellingStreaming] = useState<boolean>(false);
    const [toolCallText, setToolCallText] = useState<string | null>(null);
    const [isAgentTyping, setIsAgentTyping] = useState<boolean>();
    const [isWaitingForStreamingMessages, setIsWaitingForStreamingMessages] = useState<boolean>();

    const [downButtonState, setDownButtonState] = useState<{ visible: boolean; flash: boolean }>({ visible: false, flash: false });

    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const currentScrollTop = useRef<number>(0);
    const currentScrollHeight = useRef<number>(0);
    const streamingMessageRef = useRef<ChatMessage | null>(null);
    const currentThreadIdRef = useRef<string>(threadId || '');
    const isNewThreadAdded = useRef<boolean>(false);
    // pass userDefinedThreadId to thread create for matching the thread id from the stream message
    const userDefinedThreadIdRef = useRef<string>(Guid.newGuid());

    const messageChunkQueue = useRef<StreamingMessage[]>([]);
    const isTypingChars = useRef<boolean>(false);
    const typingCharIndex = useRef<number>(0);
    const typingCharsTimeout = useRef<NodeJS.Timeout | undefined>(undefined);
    const isCancellingStreamingRef = useRef<boolean>(false);
    const addThreadRef = useRef(addThread);
    const promoteThreadRef = useRef(promoteThread);

    const prepareForAddingChatHistory = () => {
        currentScrollHeight.current = messagesDivRef.current?.scrollHeight || 0;
    };

    const { chatHistory, isLoadingInitialChatHistory, loadOlderMessagesRef } = useChatHistoryDataCache(
        threadId,
        userDefinedThreadIdRef.current,
        threadSource,
        prepareForAddingChatHistory
    );

    useEffect(() => {
        // If isLoadingInitialChatHistory is true, that means chat history data cache is not available.
        // In this case, we should delete and ignore the existing streaming messages for this thread to avoid potential conflicts as some
        // of the existing streaming messages might be already recorded in database that will be returned in the chat history
        if (isLoadingInitialChatHistory) {
            deleteExistingStreamingMessages(currentThreadIdRef.current || userDefinedThreadIdRef.current);
            setIgnoreExistingStreamingMessages(true);
        }
    }, [isLoadingInitialChatHistory, deleteExistingStreamingMessages]);

    // Handling existing streaming messages when landing on the thread chat box
    useEffect(() => {
        const { messages, lastChatMessageFinished, latestToolCall } = convertStreamingMessagesToChatMessages(
            ignoreExistingStreamingMessages,
            existingStreamingMessages,
            userId,
            displayName
        );

        if (messages.length > 0) {
            const isLastChatMessageAgentMessage = isAgentMessage(messages[messages.length - 1]);
            if (isLastChatMessageAgentMessage && lastChatMessageFinished) {
                setChatMessagesFromExistingStreamingMessages(messages);
            } else {
                // If the last message is a user message, that means there are pending streaming messages. In this case, we create an empty streaming message
                if (!isLastChatMessageAgentMessage) {
                    setChatMessagesFromExistingStreamingMessages(messages);
                    setStreamingMessage(prev => {
                        // in case the latest streaming message is being handled before the existing streaming messages are loaded, we ignore creating a default streaming message
                        return prev ? prev : composeDefaultStreamingMessage();
                    });
                }
                // If the last message is an agent message, and not finished, we treat the last message as the streaming message, set isAgentTyping to true and set latest tool call text if any
                else {
                    setChatMessagesFromExistingStreamingMessages(messages.slice(0, -1));
                    const currentExistingStreamingMessage = messages[messages.length - 1];
                    setStreamingMessage(prev => {
                        if (prev) {
                            // In case the latest streaming message is being handled before the existing streaming messages are loaded, we merge the contents
                            return {
                                ...prev,
                                id: prev.id || currentExistingStreamingMessage.id,
                                timeStamp: prev.timeStamp || currentExistingStreamingMessage.timeStamp,
                                contents: [...currentExistingStreamingMessage.contents, ...prev.contents],
                            };
                        } else {
                            return { ...currentExistingStreamingMessage };
                        }
                    });
                    // Set toolCallText if it has not been set yet
                    setToolCallText(prev => prev ?? latestToolCall);
                }
                // Set isAgentTyping and isWaitingForStreamingMessages to true if they have not been set yet
                setIsAgentTyping(prev => (prev === undefined ? true : prev));
                setIsWaitingForStreamingMessages(prev => (prev === undefined ? true : prev));
            }
        }
    }, [existingStreamingMessages, ignoreExistingStreamingMessages, userId, displayName]);

    const isNewAndCleanThread = useMemo(
        () =>
            !isLoadingInitialChatHistory &&
            !currentThreadId &&
            (chatHistory?.length ?? 0) === 0 &&
            chatMessagesFromExistingStreamingMessages.length === 0 &&
            newMessages.length === 0 &&
            !streamingMessage,
        [
            isLoadingInitialChatHistory,
            currentThreadId,
            chatHistory,
            chatMessagesFromExistingStreamingMessages,
            newMessages,
            streamingMessage,
        ]
    );

    const scrollToBottom = (smooth: boolean) =>
        messagesDivRef.current?.scrollTo({ top: messagesDivRef.current.scrollHeight, behavior: smooth ? 'smooth' : undefined });

    const isChatAtBottom = () =>
        messagesDivRef.current &&
        messagesDivRef.current.scrollHeight - messagesDivRef.current.offsetHeight - messagesDivRef.current.scrollTop <= 2;

    const handleScroll = debounce((isScrollingToTop: boolean) => {
        if (isScrollingToTop) {
            loadOlderMessagesRef.current?.();
        }

        const isAtBottom = isChatAtBottom();
        setDownButtonState({ visible: !isAtBottom, flash: !!isAgentTyping });
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
        setIsWaitingForStreamingMessages(false);
        setToolCallText(null);
        setIsCancellingStreaming(false);

        messageChunkQueue.current = [];
        isTypingChars.current = false;
        typingCharIndex.current = 0;

        clearTimeout(typingCharsTimeout.current);
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

    const getGroupedChatMessages = useCallback(
        (message: ChatMessage, isStreamingMessage?: boolean): ChatMessage[] => {
            // If the message is streaming message, the position should be the last index of the messagesRef plus one
            const currentMessageIndex = isStreamingMessage ? allMessages.length : allMessages.findIndex(msg => msg.id === message.id);

            const groupedMessages: ChatMessage[] = [message];
            for (let i = currentMessageIndex - 1; i >= 0; i--) {
                const previousMessage = allMessages[i];
                if (shouldGroupWithPreviousMessageV2(message, previousMessage)) {
                    groupedMessages.unshift(previousMessage);
                } else {
                    break;
                }
            }

            return groupedMessages;
        },
        [allMessages]
    );

    const sendMessageHandler = useCallback(
        async (message: string) => {
            const currentStreamingMessage = streamingMessageRef.current;
            setStreamingMessage(null);

            if (currentStreamingMessage && !isChatMessageEmpty(currentStreamingMessage)) {
                setNewMessages(prev => [...prev, cloneDeep(currentStreamingMessage)]);
            }

            setTemporaryUserMessage(composeUserMessage(userId, displayName, message));
            setStreamingMessage(composeDefaultStreamingMessage());
            setIsAgentTyping(true);
            setIsWaitingForStreamingMessages(true);
            setToolCallText(null);

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
                setIsWaitingForStreamingMessages(true);
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
            const threadIdFromStream = currentMessageChunk.additionalProperties?.threadId;

            if (isUserStreamingMessage(currentMessageChunk)) {
                if (currentThreadIdRef.current) {
                    promoteThreadRef.current(currentThreadIdRef.current);
                } else if (threadIdFromStream) {
                    setCurrentThreadId(threadIdFromStream);
                    if (!isNewThreadAdded.current) {
                        addThreadRef.current(threadIdFromStream);
                        isNewThreadAdded.current = true;
                    }
                }

                const userMessage = constructUserMessageFromStreamingMessage(currentMessageChunk);
                setTemporaryUserMessage(null);
                setNewMessages(prev => [...prev, userMessage]);

                handleCompletedMessageChunk(currentMessageChunk);
            } else {
                setToolCallText(isCancellingStreamingRef.current ? null : currentToolCallText);
                setIsWaitingForStreamingMessages(false);

                if (currentMessageText && !isCancellingStreamingRef.current) {
                    if (isDefaultStreamingMessageType(currentMessageChunk)) {
                        const typeChar = () => {
                            if (typingCharIndex.current < currentMessageText.length && !isCancellingStreamingRef.current) {
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
                                        // Append text to the lastest text messsage
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
            }
        };

        const queueMessageChunk = (messageChunk?: StreamingMessage) => {
            if (messageChunk && isSubscribed) {
                messageChunkQueue.current.push(messageChunk);

                if (!isTypingChars.current) {
                    handleMessageTyping();
                }
            }
        };

        const existingStreamingMessagesHandler = (streamingMessages: StreamingMessage[] | null | undefined) => {
            if (streamingMessages && streamingMessages.length > 0) {
                setExistingStreamingMessages(streamingMessages);
            }
        };

        const threadUpdateHandler = (messageChunk?: StreamingMessage) => {
            queueMessageChunk(messageChunk);
        };

        const messageUpdateHandler = (messageChunk?: StreamingMessage) => {
            queueMessageChunk(messageChunk);
        };

        const unsubscribeChatStreaming = subscribeChatStreaming(
            currentThreadIdRef.current || userDefinedThreadIdRef.current,
            existingStreamingMessagesHandler,
            messageUpdateHandler,
            threadUpdateHandler
        );

        return () => {
            isSubscribed = false;
            unsubscribeChatStreaming();
        };
        // Ask owner to review if you need to add any dependencies here as the retriggering will cause potential message duplications due to calling existingStreamingMessagesHandler
    }, [subscribeChatStreaming]);

    useEffect(() => {
        if (threadId) {
            updateThreadLastReadTime(threadId);
        }
    }, [threadId]);

    useEffect(() => {
        const observer = new IntersectionObserver((entries: IntersectionObserverEntry[]) => {
            const entry = entries[0];
            if (entry.isIntersecting) {
                loadOlderMessagesRef.current?.();
            }
        });
        if (observer && intersectionObserverRef.current && !isLoadingInitialChatHistory) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
        };
    }, [isLoadingInitialChatHistory]);

    // When old messages are added at the top of the chat history, useLayoutEffect will calculate the new scroll top
    // to make sure the chat does not scroll to top before the next paint
    useLayoutEffect(() => {
        let timeoutId: number | undefined = undefined;
        if (messagesDivRef.current && (chatHistory?.length || 0) > 0) {
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
    }, [chatHistory?.length]);

    useEffect(() => {
        scrollToBottom(false);
    }, [chatMessagesFromExistingStreamingMessages]);

    useEffect(() => {
        // When new messages are added or the streaming message is initialized, scroll to the bottom
        if (!!temporaryUserMessage && !!streamingMessage) {
            scrollToBottom(true);
        }
    }, [temporaryUserMessage, !!streamingMessage]);

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

        for (let i = allMessages.length - 1; i >= 0 && result.length < 3; i--) {
            const msg = allMessages[i];
            const text = msg.contents[0]?.text;
            if (text && msg.author.role !== 'SREAgent' && !seenTexts.has(text)) {
                result.push(msg);
                seenTexts.add(text);
            }
        }
        return result.map(message => {
            return message.contents[0].text || '';
        });
    }, [allMessages]);

    useEffect(() => {
        if (streamingMessage && !isChatMessageEmpty(streamingMessage)) {
            if (!isChatAtBottom()) {
                setDownButtonState({ visible: true, flash: !!isAgentTyping });
            } else {
                setDownButtonState({ visible: false, flash: false });
                scrollToBottom(true);
            }
        }
    }, [streamingMessage, isAgentTyping]);

    useEffect(() => {
        isCancellingStreamingRef.current = isCancellingStreaming;
    }, [isCancellingStreaming]);

    useEffect(() => {
        currentThreadIdRef.current = currentThreadId || '';
    }, [currentThreadId]);

    useEffect(() => {
        setAllMessages([...(chatHistory?.flat() || []), ...chatMessagesFromExistingStreamingMessages, ...newMessages]);
    }, [chatHistory, chatMessagesFromExistingStreamingMessages, newMessages]);

    // Record the last read time and cache all the old messages when the component is unmounted
    useEffect(() => {
        return () => {
            if (currentThreadIdRef.current) {
                updateThreadLastReadTime(currentThreadIdRef.current);
            }
        };
    }, []);

    return {
        chatHistory,
        chatMessagesFromExistingStreamingMessages,
        newMessages,
        isLoading: isLoadingInitialChatHistory,
        isAgentTyping,
        isWaitingForStreamingMessages,
        temporaryUserMessage,
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
