import cloneDeep from 'lodash/cloneDeep';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useLayoutEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import {
    Approval,
    ApprovalDecision,
    AzCliExecution,
    ExecutionStatus,
    KubectlExecution,
    PsqlExecution,
} from '../../Common/Contracts/DataPlane/Message';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { Guid } from '../../Common/Helpers/Guid';
import { MessageCreateRequest } from '../../Common/Providers/StreamingProvider';
import { PromptResources } from '../../Strings/SREAgentResources';
import {
    composeDefaultAgentMessage,
    composeUserMessage,
    constructUserMessageFromStreamingMessage,
    createChatMessageContentFromStreamingMessage,
    getDefaultDeepInvestigationStatusChatMessage,
    getStreamingMessageText,
    getToolCallText,
    isChatMessageContentAnAgentTaskMessageContent,
    isChatMessageContentAnApprovalOrCliMessageContent,
    isChatMessageContentNonImageText,
    isChatMessageEmpty,
    isFinalStreamingMessage,
    isInitialState,
    isUpdatedCliOrApprovalStreamingMessage,
    isUserStreamingMessage,
    shouldGroupWithPreviousMessage,
} from '../Activities/Utility';
import { ChatMessage, SendMessageOptions } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';
import { useChatHistory } from './useChatHistory';

const DEEP_INVESTIGATION_CONFIRM_DISMISSED_KEY = 'sreagent.deepInvestigationConfirmDismissed';

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

    const [messages, setMessages] = useState<ChatMessage[]>([]);

    const [temporaryUserMessage, setTemporaryUserMessage] = useState<ChatMessage | null>(null);
    const [streamingMessage, setStreamingMessage] = useState<ChatMessage | null | undefined>();
    const [isCancellingStreaming, setIsCancellingStreaming] = useState<boolean>(false);
    const [toolCallText, setToolCallText] = useState<string | null | undefined>();
    const [isAgentTyping, setIsAgentTyping] = useState<boolean | undefined>();
    const [isWaitingForStreamingMessages, setIsWaitingForStreamingMessages] = useState<boolean | undefined>();

    const [downButtonState, setDownButtonState] = useState<{ visible: boolean; flash: boolean }>({ visible: false, flash: false });

    const [isDeepInvestigationButtonEnabled, setIsDeepInvestigationButtonEnabled] = useState<boolean>(false);
    const [isDeepInvestigationTurnedOn, setIsDeepInvestigationTurnedOn] = useState<boolean>(false);
    const [isDeepInvestigationDialogVisible, setIsDeepInvestigationDialogVisible] = useState<boolean>(false);
    const [isDeepInvestigationDialogDismissedForCurrentAgent, setIsDeepInvestigationDialogDismissedForCurrentAgent] = useState<boolean>(
        () => {
            try {
                return localStorage.getItem(DEEP_INVESTIGATION_CONFIRM_DISMISSED_KEY) === 'true';
            } catch {
                return false;
            }
        }
    );

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
    const isHandlingStreamingMessage = useRef<boolean>(false);
    const isCancellingStreamingRef = useRef<boolean>(false);
    const addThreadRef = useRef(addThread);
    const responseEndLoggedRef = useRef<boolean>(false);
    const isDeepInvestigationTurnedOnRef = useRef<boolean>(isDeepInvestigationTurnedOn);
    isDeepInvestigationTurnedOnRef.current = isDeepInvestigationTurnedOn;

    const prepareForAddingChatHistory = () => {
        currentScrollHeight.current = messagesDivRef.current?.scrollHeight || 0;
    };
    const { chatHistoryLength, isLoadingInitialChatHistory, loadOlderMessagesRef, newestMessageTimestampInOldMessages } = useChatHistory(
        setMessages,
        threadId,
        prepareForAddingChatHistory,
        setStreamingMessage,
        setIsAgentTyping,
        setIsWaitingForStreamingMessages
    );

    const isNewAndCleanThread = useMemo(
        () => !isLoadingInitialChatHistory && !currentThreadId && messages.length === 0 && !streamingMessage,
        [isLoadingInitialChatHistory, currentThreadId, messages, streamingMessage]
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

    const changeDeepInvestigationStatus = useCallback(
        (on: boolean) => {
            setIsDeepInvestigationTurnedOn(on);

            pushCurrentStreamingMessageToNewMessages();
            setMessages(prev => [...prev, getDefaultDeepInvestigationStatusChatMessage(on)]);

            proxy.logAmplitudeControlEvent({
                targetType: 'button',
                targetAction: 'clicked',
                targetName: 'deepInvestigationMode',
                targetFriendlyName: 'Deep investigation mode',
                valueObjectName: SpecialControlValue.DoAction,
                valueObjectFriendlyName: SpecialControlValue.DoAction,
            });

            requestAnimationFrame(() => {
                scrollToBottom(false);
            });
        },
        [proxy]
    );

    const onClickDeepInvestigationButton = useCallback(() => {
        if (!isDeepInvestigationTurnedOn && !isDeepInvestigationDialogDismissedForCurrentAgent) {
            setIsDeepInvestigationDialogVisible(true);
        } else {
            changeDeepInvestigationStatus(!isDeepInvestigationTurnedOn);
        }
    }, [changeDeepInvestigationStatus, isDeepInvestigationTurnedOn, isDeepInvestigationDialogDismissedForCurrentAgent]);

    const onClickDeepInvestigationDialogActionButton = useCallback(
        (dismissDialog: boolean, yes: boolean) => {
            if (yes) {
                changeDeepInvestigationStatus(true);
            }

            if (dismissDialog) {
                try {
                    localStorage.setItem(DEEP_INVESTIGATION_CONFIRM_DISMISSED_KEY, 'true');
                } catch {
                    // Ignore localStorage errors
                }
                setIsDeepInvestigationDialogDismissedForCurrentAgent(true);
            }
        },
        [changeDeepInvestigationStatus]
    );

    const finishStreaming = () => {
        setIsAgentTyping(false);
        setIsWaitingForStreamingMessages(false);
        setToolCallText(null);
        setIsCancellingStreaming(false);

        messageChunkQueue.current = [];
        isHandlingStreamingMessage.current = false;
        responseEndLoggedRef.current = false;
    };

    const cancelStreaming = useCallback(() => {
        setIsCancellingStreaming(true);
        if (!responseEndLoggedRef.current) {
            proxy.logAmplitudeOperationEvent({
                targetType: 'update',
                targetAction: 'cancel',
                targetName: 'agentResponse',
                targetFriendlyName: 'Agent response',
                metadata: {
                    threadId: currentThreadIdRef.current,
                    threadType: threadSource,
                },
            });
            responseEndLoggedRef.current = true;
        }
    }, [proxy, threadSource]);

    useEffect(() => {
        if (isCancellingStreaming && currentThreadId) {
            cancelMessageStreaming(currentThreadId);
        }
    }, [isCancellingStreaming, currentThreadId, cancelMessageStreaming]);

    const getGroupedChatMessages = useCallback(
        (message: ChatMessage, isStreamingMessage?: boolean): ChatMessage[] => {
            // Treat streaming messages as the latest message
            const currentMessageIndex = isStreamingMessage ? messages.length : messages.findIndex(msg => msg.id === message.id);

            const groupedMessages: ChatMessage[] = [message];
            for (let i = currentMessageIndex - 1; i >= 0; i--) {
                const previousMessage = messages[i];
                if (shouldGroupWithPreviousMessage(message, previousMessage)) {
                    groupedMessages.unshift(previousMessage);
                } else {
                    break;
                }
            }

            return groupedMessages;
        },
        [messages]
    );

    const pushCurrentStreamingMessageToNewMessages = () => {
        const currentStreamingMessage = streamingMessageRef.current;
        setStreamingMessage(null);

        if (currentStreamingMessage && !isChatMessageEmpty(currentStreamingMessage)) {
            setMessages(prev => [...prev, cloneDeep(currentStreamingMessage)]);
        }
    };

    const sendMessageHandler = useCallback(
        async (message: string, options?: SendMessageOptions) => {
            pushCurrentStreamingMessageToNewMessages();

            setTemporaryUserMessage(composeUserMessage(userId, displayName, message));
            setStreamingMessage(composeDefaultAgentMessage());
            setIsAgentTyping(true);
            setIsWaitingForStreamingMessages(true);
            setToolCallText(null);

            try {
                const starterAgentName = options?.starterAgentName?.trim() || undefined;

                const messageRequest: MessageCreateRequest = {
                    text: message,
                    userId,
                    displayName,
                    conversationModifier: isDeepInvestigationTurnedOnRef.current ? 'DeepInvestigation' : undefined,
                    agent: starterAgentName,
                };

                if (currentThreadId) {
                    // Issue a request to create a new message in the current thread
                    startMessageStreamingOnExistingThread(currentThreadId, messageRequest);
                } else {
                    // Issue a request to create a new thread
                    startMessageStreamingOnNewThread(userDefinedThreadIdRef.current, {
                        startMessage: messageRequest,
                        startingAgent: starterAgentName,
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
        [userId, displayName, currentThreadId, proxy.log, startMessageStreamingOnExistingThread, startMessageStreamingOnNewThread]
    );

    useEffect(() => {
        streamingMessageRef.current = streamingMessage;
    }, [streamingMessage]);

    const getApprovalOrCliMessageIndexInStreamingMessage = (
        streamingMessage: ChatMessage | undefined | null,
        specialMessageId: string | undefined
    ) => {
        const result = streamingMessage?.contents.findIndex(content => {
            return isChatMessageContentAnApprovalOrCliMessageContent(content, specialMessageId);
        });
        return result === -1 ? undefined : result;
    };

    const getAgentTaskMessageIndexInStreamingMessage = (
        streamingMessage: ChatMessage | undefined | null,
        specialMessageId: string | undefined
    ) => {
        const result = streamingMessage?.contents.findIndex(content => {
            return isChatMessageContentAnAgentTaskMessageContent(content, specialMessageId);
        });
        return result === -1 ? undefined : result;
    };

    const getApprovalOrCliMessageIndexInExistingMessages = (messages: ChatMessage[], specialMessageId: string | undefined) => {
        for (let i = messages.length - 1; i >= 0; i--) {
            for (let j = messages[i].contents.length - 1; j >= 0; j--) {
                const content = messages[i].contents[j];
                if (isChatMessageContentAnApprovalOrCliMessageContent(content, specialMessageId)) {
                    return [i, j];
                }
            }
        }
        return undefined;
    };

    const getAgentTaskMessageIndexInExistingMessages = (messages: ChatMessage[], specialMessageId: string | undefined) => {
        for (let i = messages.length - 1; i >= 0; i--) {
            for (let j = messages[i].contents.length - 1; j >= 0; j--) {
                const content = messages[i].contents[j];
                if (isChatMessageContentAnAgentTaskMessageContent(content, specialMessageId)) {
                    return [i, j];
                }
            }
        }
        return undefined;
    };

    const updateApprovalOrCliMessageInStreamingMessage = useCallback(
        (approvalOrCliMessageProperties: {
            approval?: Approval;
            azCliExecution?: AzCliExecution;
            kubectlExecution?: KubectlExecution;
            psqlExecution?: PsqlExecution;
        }) => {
            const { approval, azCliExecution, kubectlExecution, psqlExecution } = approvalOrCliMessageProperties;

            setStreamingMessage(prev => {
                if (!prev) return prev;
                const specialMessageId = approval?.id || azCliExecution?.id || kubectlExecution?.id || psqlExecution?.id;
                const index = getApprovalOrCliMessageIndexInStreamingMessage(prev, specialMessageId);
                if (index !== undefined) {
                    prev.contents[index] = {
                        ...prev.contents[index],
                        approval,
                        azCliExecution,
                        kubectlExecution,
                        psqlExecution,
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
        const chatMessageContent = createChatMessageContentFromStreamingMessage(streamingMessage);

        const cliOrApproval =
            chatMessageContent.approval ||
            chatMessageContent.azCliExecution ||
            chatMessageContent.kubectlExecution ||
            chatMessageContent.psqlExecution;
        const isCliOrApprovalMessageInInitialState = isInitialState(cliOrApproval?.status);
        const agentTaskInfo = chatMessageContent.agentTaskInfo;

        // Log telemetry for pending approval/execution messages (once per message)
        if (cliOrApproval) {
            const isPending =
                cliOrApproval.status === ApprovalDecision.Pending ||
                cliOrApproval.status === ApprovalDecision.PendingAuthorization ||
                cliOrApproval.status === ExecutionStatus.Pending ||
                cliOrApproval.status === ExecutionStatus.PendingAuthorization;

            if (isPending) {
                const isApproval = !!chatMessageContent.approval;

                const metadata: Record<string, unknown> = {
                    threadId:
                        currentThreadIdRef.current ||
                        streamingMessage.additionalProperties?.threadId ||
                        threadId ||
                        userDefinedThreadIdRef.current,
                    threadType: threadSource ?? 'unknown',
                    messageId: cliOrApproval.id,
                    status: cliOrApproval.status,
                };

                if (!isApproval) {
                    metadata.executionType = chatMessageContent.azCliExecution
                        ? 'azCli'
                        : chatMessageContent.kubectlExecution
                          ? 'kubectl'
                          : 'psql';
                }

                proxy.logAmplitudeOperationEvent({
                    targetType: 'load',
                    targetAction: 'loaded',
                    targetName: isApproval ? 'pendingApprovalMessage' : 'pendingExecutionMessage',
                    targetFriendlyName: isApproval ? 'Pending approval message' : 'Pending execution message',
                    metadata,
                });
            }
        }

        const appendMessageInTheStreamingMessage = () => {
            setStreamingMessage(prev => {
                const newStreamingMessage = prev ? { ...prev } : composeDefaultAgentMessage();

                const latestContent = newStreamingMessage.contents[newStreamingMessage.contents.length - 1];
                const latestContentText = latestContent?.text;
                const latestContentId = latestContent?.messageId;
                const currentContentText = chatMessageContent.text;
                const currentContentId = chatMessageContent.messageId;

                if (
                    latestContent &&
                    latestContentText &&
                    latestContentId &&
                    currentContentText &&
                    currentContentId &&
                    isChatMessageContentNonImageText(latestContent) &&
                    isChatMessageContentNonImageText(chatMessageContent) &&
                    latestContentId === currentContentId
                ) {
                    return {
                        ...newStreamingMessage,
                        contents: [
                            ...newStreamingMessage.contents.slice(0, -1),
                            {
                                ...latestContent,
                                text: latestContentText + currentContentText,
                            },
                        ],
                    };
                }
                return {
                    ...newStreamingMessage,
                    contents: [...newStreamingMessage.contents, chatMessageContent],
                };
            });
        };

        const updateSpecialMessage = (type: 'approvalOrCli' | 'agentTask', id: string) => {
            setStreamingMessage(prev => {
                const newStreamingMessage = prev ? { ...prev } : composeDefaultAgentMessage();
                const index =
                    type === 'approvalOrCli'
                        ? getApprovalOrCliMessageIndexInStreamingMessage(newStreamingMessage, id)
                        : getAgentTaskMessageIndexInStreamingMessage(newStreamingMessage, id);
                if (index !== undefined) {
                    newStreamingMessage.contents[index] = chatMessageContent;
                    return cloneDeep(newStreamingMessage);
                } else {
                    return prev;
                }
            });

            setMessages(prev => {
                const messages = [...prev];
                const index =
                    type === 'approvalOrCli'
                        ? getApprovalOrCliMessageIndexInExistingMessages(messages, id)
                        : getAgentTaskMessageIndexInExistingMessages(messages, id);
                if (index !== undefined) {
                    const [messageIndex, contentIndex] = index;
                    messages[messageIndex].contents[contentIndex] = chatMessageContent;
                    return cloneDeep(messages);
                } else {
                    return prev;
                }
            });
        };

        if (cliOrApproval) {
            if (isCliOrApprovalMessageInInitialState) {
                appendMessageInTheStreamingMessage();
            } else {
                const id = cliOrApproval.id;
                updateSpecialMessage('approvalOrCli', id);
            }
        } else if (agentTaskInfo) {
            const id = agentTaskInfo.id;
            updateSpecialMessage('agentTask', id);
        } else {
            appendMessageInTheStreamingMessage();
        }
    };

    const handleMessageTyping = () => {
        if (messageChunkQueue.current.length === 0) {
            isHandlingStreamingMessage.current = false;
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
                        metadata: {
                            threadId: currentThreadIdRef.current,
                            threadType: threadSource,
                        },
                    });
                    responseEndLoggedRef.current = true;
                }
                finishStreaming();
            } else {
                handleMessageTyping();
            }
        };

        isHandlingStreamingMessage.current = true;
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
            setMessages(prev => [...prev, userMessage]);
        } else if (isUpdatedCliOrApprovalStreamingMessage(currentMessageChunk)) {
            processChatMessageContents(currentMessageChunk);
        } else {
            setIsAgentTyping(true);
            setIsWaitingForStreamingMessages(false);
            setToolCallText(isCancellingStreamingRef.current ? null : currentToolCallText);

            if (currentMessageText && !isCancellingStreamingRef.current) {
                processChatMessageContents(currentMessageChunk);
            }
        }

        handleCompletedMessageChunk(currentMessageChunk);
    };

    const attemptToProcessMessageChunk = () => {
        if (
            !isHandlingStreamingMessage.current &&
            streamingMessageTimestampFilterRef.current !== null &&
            messageChunkQueue.current.length > 0
        ) {
            isHandlingStreamingMessage.current = true;
            handleMessageTyping();
        }
    };

    useEffect(() => {
        let isSubscribed = true;

        const id = currentThreadIdRef.current || userDefinedThreadIdRef.current;

        const latestStreamingMessageHandler = (messageChunk?: StreamingMessage | null) => {
            if (messageChunk && !isFinalStreamingMessage(messageChunk) && !isUpdatedCliOrApprovalStreamingMessage(messageChunk)) {
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
        if (messagesDivRef.current && chatHistoryLength > 0) {
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
    }, [chatHistoryLength]);

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
        if (streamingMessage && !isChatMessageEmpty(streamingMessage)) {
            if (!isChatAtBottom()) {
                setDownButtonState({ visible: true, flash: !!isAgentTyping });
            } else {
                setDownButtonState({ visible: false, flash: false });
            }
        }
    }, [streamingMessage, isAgentTyping]);

    useEffect(() => {
        isCancellingStreamingRef.current = isCancellingStreaming;
    }, [isCancellingStreaming]);

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

    return {
        messages,
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
        updateApprovalOrCliMessageInStreamingMessage,
        userDefinedThreadIdRef,

        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        isDeepInvestigationDialogVisible,
        setIsDeepInvestigationDialogVisible,
        onClickDeepInvestigationDialogActionButton,
        onClickDeepInvestigationButton,
    };
};
