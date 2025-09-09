import cloneDeep from 'lodash/cloneDeep';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { AgentTaskMetaData } from '../../Common/Contracts/DataPlane/AgentTask';
import { Approval, AzCliExecution, KubectlExecution } from '../../Common/Contracts/DataPlane/Message';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { MessageCreateRequest } from '../../Common/Providers/StreamingProvider';
import { PromptResources } from '../../Strings/SREAgentResources';
import {
    composeDefaultAgentMessage,
    composeUserMessage,
    constructUserMessageFromStreamingMessage,
    getDefaultDeepInvestigationStatusChatMessage,
    getSpecialMessageContentFromStreamingMessage,
    getStreamingMessageText,
    getToolCallText,
    isChatMessageContentNonImageText,
    isChatMessageEmpty,
    isDefaultStreamingMessageType,
    isFinalStreamingMessage,
    isImageStreamingMessageType,
    isPendingState,
    isUpdatedSpecialStreamingMessage,
    isUserStreamingMessage,
    processApprovalStreamingMessageStatus,
    shouldGroupWithPreviousMessage,
} from '../Activities/Utility';
import { ChatMessage, ChatMessageContent, MessageTypingCharactersPer10Ms, MessageTypingSpeedInMilliseconds } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';
import { useChatHistory } from './useChatHistory';

export const useChatBox = (
    addThread: (threadId: string) => void,
    updateThreadLastReadTime: (threadId: string) => void,
    threadId?: string | null,
    threadSource?: string | null
) => {
    const intl = useIntl();

    const proxy = useContext(AzPortalContext);
    const { startMessageStreamingOnNewThread, startMessageStreamingOnExistingThread, cancelMessageStreaming, subscribeMessageUpdateEvent } =
        useContext(StreamingContext);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [allMessages, setAllMessages] = useState<ChatMessage[]>([]);
    const [newMessages, setNewMessages] = useState<ChatMessage[]>([]);

    const [temporaryUserMessage, setTemporaryUserMessage] = useState<ChatMessage | null>(null);
    const [streamingMessage, setStreamingMessage] = useState<ChatMessage | null | undefined>();
    const [isCancellingStreaming, setIsCancellingStreaming] = useState<boolean>(false);
    const [toolCallText, setToolCallText] = useState<string | null | undefined>();
    const [isAgentTyping, setIsAgentTyping] = useState<boolean | undefined>();
    const [isWaitingForStreamingMessages, setIsWaitingForStreamingMessages] = useState<boolean | undefined>();

    const [downButtonState, setDownButtonState] = useState<{ visible: boolean; flash: boolean }>({ visible: false, flash: false });
    const [isDeepInvestigationButtonEnabled, setIsDeepInvestigationButtonEnabled] = useState<boolean>(false);
    const [isDeepInvestigationTurnedOn, setIsDeepInvestigationTurnedOn] = useState<boolean>(false);

    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const currentScrollTop = useRef<number>(0);
    const currentScrollHeight = useRef<number>(0);
    const streamingMessageRef = useRef<ChatMessage | null | undefined>();
    const currentThreadIdRef = useRef<string>(threadId || '');
    const isNewThreadAdded = useRef<boolean>(false);
    // pass userDefinedThreadId to thread create for matching the thread id from the stream message
    const userDefinedThreadIdRef = useRef<string>(Guid.newGuid());
    const streamingMessageTimestampFilterRef = useRef<string | null>(null);

    const messageChunkQueue = useRef<StreamingMessage[]>([]);
    const isTypingChars = useRef<boolean>(false);
    const typingCharIndex = useRef<number>(0);
    const typingCharsTimeout = useRef<NodeJS.Timeout | undefined>(undefined);
    const isCancellingStreamingRef = useRef<boolean>(false);
    const addThreadRef = useRef(addThread);
    const responseEndLoggedRef = useRef<boolean>(false);
    const isDeepInvestigationTurnedOnRef = useRef<boolean>(isDeepInvestigationTurnedOn);

    const { chatHistory, isLoadingInitialChatHistory, loadOlderMessagesRef, newestMessageTimestampInOldMessages } = useChatHistory(
        threadId,
        threadSource,
        () => {
            currentScrollHeight.current = messagesDivRef.current?.scrollHeight || 0;
        }
    );

    const isNewAndCleanThread = useMemo(
        () =>
            !isLoadingInitialChatHistory &&
            !currentThreadId &&
            (chatHistory?.length ?? 0) === 0 &&
            newMessages.length === 0 &&
            !streamingMessage,
        [isLoadingInitialChatHistory, currentThreadId, chatHistory, newMessages, streamingMessage]
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

    const onClickDeepInvestigationButton = useCallback(() => {
        const currentValue = isDeepInvestigationTurnedOnRef.current;
        isDeepInvestigationTurnedOnRef.current = !currentValue;
        setIsDeepInvestigationTurnedOn(!currentValue);

        pushCurrentStreamingMessageToNewMessages();
        setNewMessages(prev => [...prev, getDefaultDeepInvestigationStatusChatMessage(!currentValue)]);

        requestAnimationFrame(() => {
            scrollToBottom(false);
        });
    }, []);

    const finishStreaming = () => {
        setIsAgentTyping(false);
        setIsWaitingForStreamingMessages(false);
        setToolCallText(null);
        setIsCancellingStreaming(false);

        messageChunkQueue.current = [];
        isTypingChars.current = false;
        typingCharIndex.current = 0;
        responseEndLoggedRef.current = false;

        clearTimeout(typingCharsTimeout.current);
    };

    const cancelStreaming = useCallback(() => {
        setIsCancellingStreaming(true);
        if (!responseEndLoggedRef.current) {
            proxy.logAmplitudeOperationEvent({
                targetType: 'update',
                targetAction: 'cancel',
                targetName: 'agentResponse',
                targetFriendlyName: 'Agent response',
            });
            responseEndLoggedRef.current = true;
        }
    }, [proxy]);

    useEffect(() => {
        if (isCancellingStreaming && currentThreadId) {
            cancelMessageStreaming(currentThreadId);
        }
    }, [isCancellingStreaming, currentThreadId, cancelMessageStreaming]);

    const getGroupedChatMessages = useCallback(
        (message: ChatMessage, isStreamingMessage?: boolean): ChatMessage[] => {
            // Treat streaming messages as the latest message
            const currentMessageIndex = isStreamingMessage ? allMessages.length : allMessages.findIndex(msg => msg.id === message.id);

            const groupedMessages: ChatMessage[] = [message];
            for (let i = currentMessageIndex - 1; i >= 0; i--) {
                const previousMessage = allMessages[i];
                if (shouldGroupWithPreviousMessage(message, previousMessage)) {
                    groupedMessages.unshift(previousMessage);
                } else {
                    break;
                }
            }

            return groupedMessages;
        },
        [allMessages]
    );

    const pushCurrentStreamingMessageToNewMessages = () => {
        const currentStreamingMessage = streamingMessageRef.current;
        setStreamingMessage(null);

        if (currentStreamingMessage && !isChatMessageEmpty(currentStreamingMessage)) {
            setNewMessages(prev => [...prev, cloneDeep(currentStreamingMessage)]);
        }
    };

    const sendMessageHandler = useCallback(
        async (message: string) => {
            pushCurrentStreamingMessageToNewMessages();

            setTemporaryUserMessage(composeUserMessage(userId, displayName, message));
            setStreamingMessage(composeDefaultAgentMessage());
            setIsAgentTyping(true);
            setIsWaitingForStreamingMessages(true);
            setToolCallText(null);

            try {
                const messageRequest: MessageCreateRequest = {
                    text: message,
                    userId,
                    displayName,
                    conversationModifier: isDeepInvestigationTurnedOnRef.current ? 'DeepInvestigation' : undefined,
                };

                //ToDo: Handle errors of sendMessage, createThread and pollResponses
                if (currentThreadId) {
                    // Issue a request to create a new message in the current thread
                    startMessageStreamingOnExistingThread(currentThreadId, messageRequest);
                } else {
                    // Issue a request to create a new thread
                    startMessageStreamingOnNewThread(userDefinedThreadIdRef.current, {
                        startMessage: messageRequest,
                    });
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

    const getMessageIndexInStreamingMessage = (streamingMessage: ChatMessage | undefined | null, specialMessageId: string | undefined) => {
        const result = streamingMessage?.contents.findIndex(content => {
            return (
                specialMessageId &&
                (content.approval?.id === specialMessageId ||
                    content.azCliExecution?.id === specialMessageId ||
                    content.kubectlExecution?.id === specialMessageId)
            );
        });
        return result === -1 ? undefined : result;
    };

    const getMessageIndexInNewMessages = (newMessages: ChatMessage[], specialMessageId: string | undefined) => {
        for (let i = newMessages.length - 1; i >= 0; i--) {
            for (let j = newMessages[i].contents.length - 1; j >= 0; j--) {
                const content = newMessages[i].contents[j];
                if (
                    specialMessageId &&
                    (content.approval?.id === specialMessageId ||
                        content.azCliExecution?.id === specialMessageId ||
                        content.kubectlExecution?.id === specialMessageId)
                ) {
                    return [i, j];
                }
            }
        }
        return undefined;
    };

    const updateSpecialMessageInStreamingMessage = useCallback(
        (specialMessageProperties: { approval?: Approval; azCliExecution?: AzCliExecution; kubectlExecution?: KubectlExecution }) => {
            const { approval, azCliExecution, kubectlExecution } = specialMessageProperties;

            setStreamingMessage(prev => {
                if (!prev) return prev;
                const specialMessageId = approval?.id || azCliExecution?.id || kubectlExecution?.id;
                const index = getMessageIndexInStreamingMessage(prev, specialMessageId);
                if (index !== undefined) {
                    prev.contents[index] = {
                        ...prev.contents[index],
                        approval,
                        azCliExecution,
                        kubectlExecution,
                    };
                    return cloneDeep(prev);
                } else {
                    return prev;
                }
            });
        },
        []
    );

    const processChatMessageContents = (streamingMessage: StreamingMessage) => {
        const messageContent = streamingMessage.contents?.[0];

        let approval = getSpecialMessageContentFromStreamingMessage<Approval>(streamingMessage, 'approval');
        const azCliExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'azcli');
        const kubectlExecution = getSpecialMessageContentFromStreamingMessage<AzCliExecution>(streamingMessage, 'kubectl');
        const agentTaskInfo = getSpecialMessageContentFromStreamingMessage<AgentTaskMetaData>(streamingMessage, 'deepinvestigation');
        const text = messageContent?.text && !approval && !azCliExecution && !kubectlExecution ? messageContent.text : '';
        const isImage = isImageStreamingMessageType(streamingMessage);

        if (approval) {
            approval = {
                ...approval,
                status: processApprovalStreamingMessageStatus(approval.status),
            };
        }

        const chatMessageContent: ChatMessageContent = {
            text,
            isImage,
            approval,
            azCliExecution,
            kubectlExecution,
            agentTaskInfo,
            isDailyReport: false,
        };

        const specialMessage = chatMessageContent.approval || chatMessageContent.azCliExecution || chatMessageContent.kubectlExecution;
        const specialMessageId = specialMessage?.id;
        const isSpecialMessageInInitialState = isPendingState(specialMessage?.status);

        if (!specialMessage || isSpecialMessageInInitialState) {
            setStreamingMessage(prev => {
                const newStreamingMessage = prev ? { ...prev } : composeDefaultAgentMessage();
                return {
                    ...newStreamingMessage,
                    contents: [...newStreamingMessage.contents, chatMessageContent],
                };
            });
            return;
        }

        setStreamingMessage(prev => {
            const newStreamingMessage = prev ? { ...prev } : composeDefaultAgentMessage();
            const index = getMessageIndexInStreamingMessage(newStreamingMessage, specialMessageId);
            if (index !== undefined) {
                newStreamingMessage.contents[index] = chatMessageContent;
                return cloneDeep(newStreamingMessage);
            } else {
                return prev;
            }
        });

        setNewMessages(prev => {
            const newMessages = [...prev];
            const index = getMessageIndexInNewMessages(newMessages, specialMessageId);
            if (index !== undefined) {
                const [messageIndex, contentIndex] = index;
                newMessages[messageIndex].contents[contentIndex] = chatMessageContent;
                return cloneDeep(newMessages);
            } else {
                return prev;
            }
        });
    };

    const handleMessageTyping = () => {
        if (messageChunkQueue.current.length === 0) {
            isTypingChars.current = false;
            setIsWaitingForStreamingMessages(true);
            return;
        }

        const handleCompletedMessageChunk = (messageChunk: StreamingMessage) => {
            messageChunkQueue.current.shift();
            if (isFinalStreamingMessage(messageChunk)) {
                if (!responseEndLoggedRef.current) {
                    proxy.logAmplitudeOperationEvent({
                        targetType: 'update',
                        targetAction: isCancellingStreamingRef.current ? 'cancel' : 'success',
                        targetName: 'agentResponse',
                        targetFriendlyName: 'Agent response',
                    });
                    responseEndLoggedRef.current = true;
                }
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
        const timestamp = currentMessageChunk.createdAt;

        if (
            timestamp &&
            streamingMessageTimestampFilterRef.current &&
            getSafeDateTime(timestamp).getTime() <= getSafeDateTime(streamingMessageTimestampFilterRef.current).getTime()
        ) {
            handleCompletedMessageChunk(currentMessageChunk);
            return;
        }

        if (isUserStreamingMessage(currentMessageChunk)) {
            if (!currentThreadIdRef.current && threadIdFromStream) {
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
        } else if (isUpdatedSpecialStreamingMessage(currentMessageChunk)) {
            processChatMessageContents(currentMessageChunk);
            handleCompletedMessageChunk(currentMessageChunk);
        } else {
            setIsAgentTyping(true);
            setIsWaitingForStreamingMessages(false);
            setToolCallText(isCancellingStreamingRef.current ? null : currentToolCallText);

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
                                const newStreamingMessage = prev ? { ...prev } : composeDefaultAgentMessage();
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
                    processChatMessageContents(currentMessageChunk);
                    handleCompletedMessageChunk(currentMessageChunk);
                }
            } else {
                handleCompletedMessageChunk(currentMessageChunk);
            }
        }
    };

    const attemptToProcessMessageChunk = () => {
        if (!isTypingChars.current && streamingMessageTimestampFilterRef.current !== null && messageChunkQueue.current.length > 0) {
            isTypingChars.current = true;
            handleMessageTyping();
        }
    };

    useEffect(() => {
        let isSubscribed = true;

        const id = currentThreadIdRef.current || userDefinedThreadIdRef.current;

        const latestStreamingMessageHandler = (messageChunk?: StreamingMessage | null) => {
            if (messageChunk && !isFinalStreamingMessage(messageChunk) && !isUpdatedSpecialStreamingMessage(messageChunk)) {
                setStreamingMessage(prev => {
                    return prev === undefined ? composeDefaultAgentMessage() : prev;
                });
                setIsAgentTyping(prev => (prev === undefined ? true : prev));
                setIsWaitingForStreamingMessages(prev => (prev === undefined ? true : prev));
                setToolCallText(prev => (prev === undefined ? getToolCallText(messageChunk) : prev));
            }
        };

        const messageUpdateHandler = (messageChunk?: StreamingMessage) => {
            if (
                messageChunk &&
                isSubscribed &&
                messageChunk.additionalProperties?.threadId &&
                messageChunk.additionalProperties.threadId === id
            ) {
                messageChunkQueue.current.push(messageChunk);
                attemptToProcessMessageChunk();
            }
        };

        const unsubscribeChatStreaming = subscribeMessageUpdateEvent({
            handler: messageUpdateHandler,
            threadId: id,
            latestStreamingMessageHandler,
        });

        return () => {
            isSubscribed = false;
            unsubscribeChatStreaming();
        };
        // Ask owner to review if you need to add any dependencies here as the retriggering will cause potential message duplications
    }, [subscribeMessageUpdateEvent]);

    useEffect(() => {
        if (newestMessageTimestampInOldMessages !== null) {
            streamingMessageTimestampFilterRef.current = newestMessageTimestampInOldMessages;
            attemptToProcessMessageChunk();
        }
    }, [newestMessageTimestampInOldMessages]);

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

    useEffect(() => {
        setIsDeepInvestigationButtonEnabled(!isLoadingInitialChatHistory && !isAgentTyping);
    }, [isLoadingInitialChatHistory, isAgentTyping]);

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
        setAllMessages([...(chatHistory?.flat() || []), ...newMessages]);
    }, [chatHistory, newMessages]);

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
        updateSpecialMessageInStreamingMessage,
        userDefinedThreadIdRef,
        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        onClickDeepInvestigationButton,
    };
};
