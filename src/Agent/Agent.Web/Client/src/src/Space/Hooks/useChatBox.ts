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
    composeUserMessage,
    createChatMessageFromStreamingMessage,
    getDefaultDeepInvestigationStatusChatMessageGroup,
    getStreamingMessageText,
    getToolCallText,
    isAgentMessagesEmpty,
    isChatMessageAMatchedAgentTaskMessage,
    isChatMessageAMatchedApprovalOrCliMessage,
    isChatMessageContentNonImageText,
    isFinalStreamingMessage,
    isInitialState,
    isUpdatedCliOrApprovalStreamingMessage,
    isUserStreamingMessage,
} from '../Activities/Utility';
import { ChatMessage, ChatMessageGroup, SendMessageOptions } from '../Contracts/Activities';
import { StreamingContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';
import { useChatHistory } from './useChatHistory';

const DEEP_INVESTIGATION_CONFIRM_DISMISSED_KEY = 'sreagent.deepInvestigationConfirmDismissed';

export const useChatBox = (
    addThread: (threadId: string) => void,
    updateThreadLastReadTime: (threadId: string) => void,
    threadId: string | null | undefined,
    threadSource: string | null | undefined
) => {
    const intl = useIntl();

    const proxy = useContext(AzPortalContext);
    const { startMessageStreamingOnNewThread, startMessageStreamingOnExistingThread, cancelMessageStreaming, subscribeMessageUpdateEvent } =
        useContext(StreamingContext);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [messageGroups, setMessageGroups] = useState<ChatMessageGroup[]>([]);

    const [streamingMessageGroup, setStreamingMessageGroup] = useState<ChatMessageGroup | null | undefined>();
    const [isCancellingStreaming, setIsCancellingStreaming] = useState<boolean>(false);
    const [toolCallText, setToolCallText] = useState<string | null | undefined>();
    const [isAgentTyping, setIsAgentTyping] = useState<boolean | undefined>();
    const [isWaitingForStreamingMessages, setIsWaitingForStreamingMessages] = useState<boolean | undefined>();
    const [hasExistingStreamingMessage, setHasExistingStreamingMessage] = useState<boolean>(false);

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

    const threadIdUsedForCreatingNewThread = useMemo(() => Guid.newGuid(), []);

    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const currentScrollTop = useRef<number>(0);
    const currentScrollHeight = useRef<number>(0);
    const streamingMessageGroupRef = useRef<ChatMessageGroup | null | undefined>();
    const currentThreadIdRef = useRef<string>(threadId || '');
    const isNewThreadAdded = useRef<boolean>(false);
    // pass userDefinedThreadId to thread create for matching the thread id from the stream message
    const threadIdUsedForCreatingNewThreadRef = useRef<string>(threadIdUsedForCreatingNewThread);
    threadIdUsedForCreatingNewThreadRef.current = threadIdUsedForCreatingNewThread;
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

    const scrollToBottom = (smooth: boolean) =>
        messagesDivRef.current?.scrollTo({ top: messagesDivRef.current.scrollHeight, behavior: smooth ? 'smooth' : undefined });

    const { chatHistoryChangeTrigger, isLoadingInitialChatHistory, loadOlderMessagesRef, newestMessageTimestampInOldMessages } =
        useChatHistory(
            setMessageGroups,
            threadId,
            threadIdUsedForCreatingNewThread,
            prepareForAddingChatHistory,
            scrollToBottom,
            setStreamingMessageGroup,
            setIsAgentTyping,
            setIsWaitingForStreamingMessages,
            hasExistingStreamingMessage
        );

    const isNewAndCleanThread = useMemo(
        () => !isLoadingInitialChatHistory && !currentThreadId && messageGroups.length === 0 && !streamingMessageGroup,
        [isLoadingInitialChatHistory, currentThreadId, messageGroups, streamingMessageGroup]
    );

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
            setMessageGroups(prev => [...prev, getDefaultDeepInvestigationStatusChatMessageGroup(on)]);

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

    const finishReasoning = () => {
        setStreamingMessageGroup(prev => {
            if (!prev) return prev;
            const newestAgentMessage = prev.agentMessages.length > 0 ? prev.agentMessages[prev.agentMessages.length - 1] : null;
            if (newestAgentMessage && newestAgentMessage.reasoning && newestAgentMessage.reasoning.active) {
                return {
                    ...prev,
                    agentMessages: [
                        ...prev.agentMessages.slice(0, -1),
                        {
                            ...newestAgentMessage,
                            reasoning: {
                                ...newestAgentMessage.reasoning,
                                active: false,
                            },
                        },
                    ],
                };
            }
            return prev;
        });
    };

    const finishStreaming = () => {
        setIsAgentTyping(false);
        setIsWaitingForStreamingMessages(false);
        setToolCallText(null);
        setIsCancellingStreaming(false);
        finishReasoning();

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

    const pushCurrentStreamingMessageToNewMessages = () => {
        const currentStreamingMessageGroup = streamingMessageGroupRef.current;
        setStreamingMessageGroup(null);

        if (currentStreamingMessageGroup) {
            setMessageGroups(prev => [...prev, cloneDeep(currentStreamingMessageGroup)]);
        }
    };

    const sendMessageHandler = useCallback(
        async (message: string, options?: SendMessageOptions) => {
            pushCurrentStreamingMessageToNewMessages();

            setStreamingMessageGroup({
                id: Guid.newGuid(),
                userMessages: [composeUserMessage(userId, displayName, message)],
                agentMessages: [],
            });
            setIsAgentTyping(true);
            setIsWaitingForStreamingMessages(true);
            setToolCallText(null);

            setTimeout(() => {
                scrollToBottom(true);
            }, 0);

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
                    startMessageStreamingOnNewThread(threadIdUsedForCreatingNewThreadRef.current, {
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
        streamingMessageGroupRef.current = streamingMessageGroup;
    }, [streamingMessageGroup]);

    const getApprovalOrCliMessageIndexInStreamingMessage = (
        streamingMessageGroup: ChatMessageGroup | undefined | null,
        specialMessageId: string | undefined
    ) => {
        const result = streamingMessageGroup?.agentMessages.findIndex(message => {
            return isChatMessageAMatchedApprovalOrCliMessage(message, specialMessageId);
        });
        return result === -1 ? undefined : result;
    };

    const getAgentTaskMessageIndexInStreamingMessage = (
        streamingMessageGroup: ChatMessageGroup | undefined | null,
        specialMessageId: string | undefined
    ) => {
        const result = streamingMessageGroup?.agentMessages.findIndex(message => {
            return isChatMessageAMatchedAgentTaskMessage(message, specialMessageId);
        });
        return result === -1 ? undefined : result;
    };

    const getApprovalOrCliMessageIndexInExistingMessages = (messageGroups: ChatMessageGroup[], specialMessageId: string | undefined) => {
        for (let i = messageGroups.length - 1; i >= 0; i--) {
            for (let j = messageGroups[i].agentMessages.length - 1; j >= 0; j--) {
                const message = messageGroups[i].agentMessages[j];
                if (isChatMessageAMatchedApprovalOrCliMessage(message, specialMessageId)) {
                    return [i, j];
                }
            }
        }
        return undefined;
    };

    const getAgentTaskMessageIndexInExistingMessages = (messageGroups: ChatMessageGroup[], specialMessageId: string | undefined) => {
        for (let i = messageGroups.length - 1; i >= 0; i--) {
            for (let j = messageGroups[i].agentMessages.length - 1; j >= 0; j--) {
                const message = messageGroups[i].agentMessages[j];
                if (isChatMessageAMatchedAgentTaskMessage(message, specialMessageId)) {
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

            setStreamingMessageGroup(prev => {
                if (!prev) return prev;
                const specialMessageId = approval?.id || azCliExecution?.id || kubectlExecution?.id || psqlExecution?.id;
                const index = getApprovalOrCliMessageIndexInStreamingMessage(prev, specialMessageId);
                if (index !== undefined) {
                    const agentMessages = [...prev.agentMessages];
                    agentMessages[index] = {
                        ...agentMessages[index],
                        approval,
                        azCliExecution,
                        kubectlExecution,
                        psqlExecution,
                    };
                    return cloneDeep({
                        ...prev,
                        agentMessages,
                    });
                } else {
                    return prev;
                }
            });
        },
        []
    );

    const processChatMessageContents = (streamingMessage: StreamingMessage) => {
        const chatMessage = createChatMessageFromStreamingMessage(streamingMessage);

        const cliOrApproval =
            chatMessage.approval || chatMessage.azCliExecution || chatMessage.kubectlExecution || chatMessage.psqlExecution;
        const isCliOrApprovalMessageInInitialState = isInitialState(cliOrApproval?.status);
        const agentTaskInfo = chatMessage.agentTaskInfo;

        // Log telemetry for pending approval/execution messages (once per message)
        if (cliOrApproval) {
            const isPending =
                cliOrApproval.status === ApprovalDecision.Pending ||
                cliOrApproval.status === ApprovalDecision.PendingAuthorization ||
                cliOrApproval.status === ExecutionStatus.Pending ||
                cliOrApproval.status === ExecutionStatus.PendingAuthorization;

            if (isPending) {
                const isApproval = !!chatMessage.approval;

                const metadata: Record<string, unknown> = {
                    threadId:
                        currentThreadIdRef.current ||
                        streamingMessage.additionalProperties?.threadId ||
                        threadId ||
                        threadIdUsedForCreatingNewThreadRef.current,
                    threadType: threadSource ?? 'unknown',
                    messageId: cliOrApproval.id,
                    status: cliOrApproval.status,
                };

                if (!isApproval) {
                    metadata.executionType = chatMessage.azCliExecution ? 'azCli' : chatMessage.kubectlExecution ? 'kubectl' : 'psql';
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
            setStreamingMessageGroup(prev => {
                if (!prev) {
                    return {
                        id: Guid.newGuid(),
                        userMessages: [],
                        agentMessages: [chatMessage],
                    };
                }

                const latestAgentMessage = prev.agentMessages[prev.agentMessages.length - 1];
                const latestContentText = latestAgentMessage?.text;
                const latestContentReasoning = latestAgentMessage?.reasoning;
                const latestContentId = latestAgentMessage?.id;
                const currentText = chatMessage.text;
                const currentContentReasoning = chatMessage.reasoning;
                const currentId = chatMessage.id;

                if (
                    latestAgentMessage &&
                    latestContentText &&
                    latestContentId &&
                    currentText &&
                    currentId &&
                    isChatMessageContentNonImageText(latestAgentMessage) &&
                    isChatMessageContentNonImageText(chatMessage) &&
                    latestContentId === currentId
                ) {
                    return {
                        ...prev,
                        agentMessages: [
                            ...prev.agentMessages.slice(0, -1),
                            {
                                ...latestAgentMessage,
                                text: latestContentText + currentText,
                            },
                        ],
                    };
                }

                if (latestAgentMessage && latestContentReasoning && latestContentReasoning.items.length > 0 && currentId) {
                    if (currentContentReasoning && currentContentReasoning.items.length > 0) {
                        const latestReasoning = latestContentReasoning.items[latestContentReasoning.items.length - 1];
                        if (latestReasoning.messageId === currentId) {
                            const updatedLatestContentReasoning = [
                                ...latestContentReasoning.items.slice(0, -1),
                                {
                                    messageId: latestReasoning.messageId,
                                    content: latestReasoning.content + currentContentReasoning.items.flatMap(r => r.content).join(''),
                                },
                            ];
                            return {
                                ...prev,
                                agentMessages: [
                                    ...prev.agentMessages.slice(0, -1),
                                    {
                                        ...latestAgentMessage,
                                        reasoning: { active: true, items: updatedLatestContentReasoning },
                                    },
                                ],
                            };
                        } else {
                            return {
                                ...prev,
                                agentMessages: [
                                    ...prev.agentMessages.slice(0, -1),
                                    {
                                        ...latestAgentMessage,
                                        reasoning: {
                                            active: true,
                                            items: [...latestContentReasoning.items, ...currentContentReasoning.items],
                                        },
                                    },
                                ],
                            };
                        }
                    } else {
                        return {
                            ...prev,
                            agentMessages: [
                                ...prev.agentMessages.slice(0, -1),
                                {
                                    ...latestAgentMessage,
                                    reasoning: { active: false, items: [...latestContentReasoning.items] },
                                },
                                chatMessage,
                            ],
                        };
                    }
                }

                return {
                    ...prev,
                    agentMessages: [...prev.agentMessages, chatMessage],
                };
            });
        };

        const updateSpecialMessage = (type: 'approvalOrCli' | 'agentTask', id: string) => {
            setStreamingMessageGroup(prev => {
                if (!prev) return prev;

                const index =
                    type === 'approvalOrCli'
                        ? getApprovalOrCliMessageIndexInStreamingMessage(prev, id)
                        : getAgentTaskMessageIndexInStreamingMessage(prev, id);

                if (index !== undefined) {
                    const updatedAgentMessages = [...prev.agentMessages];
                    updatedAgentMessages[index] = chatMessage;
                    return cloneDeep({
                        ...prev,
                        agentMessages: updatedAgentMessages,
                    });
                } else {
                    return prev;
                }
            });

            setMessageGroups(prev => {
                const messageGroups = [...prev];
                const index =
                    type === 'approvalOrCli'
                        ? getApprovalOrCliMessageIndexInExistingMessages(messageGroups, id)
                        : getAgentTaskMessageIndexInExistingMessages(messageGroups, id);
                if (index !== undefined) {
                    const [messageGroupIndex, messageIndex] = index;
                    const messageGroup = { ...messageGroups[messageGroupIndex] };
                    const agentMessages = [...messageGroup.agentMessages];
                    agentMessages[messageIndex] = chatMessage;
                    messageGroups[messageGroupIndex] = {
                        ...messageGroup,
                        agentMessages: [...agentMessages],
                    };
                    return cloneDeep(messageGroups);
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

        const id = currentThreadIdRef.current || threadIdUsedForCreatingNewThreadRef.current;

        const latestStreamingMessageHandler = (messageChunk?: StreamingMessage | null) => {
            if (messageChunk && !isFinalStreamingMessage(messageChunk) && !isUpdatedCliOrApprovalStreamingMessage(messageChunk)) {
                setHasExistingStreamingMessage(true);
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
            if (entry.isIntersecting && intersectionObserverRef.current) {
                // Unobserve immediately to prevent multiple triggers
                observer.unobserve(intersectionObserverRef.current);

                // Trigger the load request and wait for it to complete
                const loadFunction = loadOlderMessagesRef.current;
                if (loadFunction) {
                    loadFunction().finally(() => {
                        if (intersectionObserverRef.current) {
                            observer.observe(intersectionObserverRef.current);
                        }
                    });
                }
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
        if (messagesDivRef.current && chatHistoryChangeTrigger !== null) {
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
    }, [chatHistoryChangeTrigger]);

    const prompts = useMemo(
        () => [
            intl.formatMessage(PromptResources.bestPracticesPrompt),
            intl.formatMessage(PromptResources.notWorkingPrompt),
            intl.formatMessage(PromptResources.availabilityPrompt),
        ],
        [intl]
    );

    const messagePromptsUsed = useMemo(() => {
        let result: string[] = [];
        const seenTexts = new Set<string>();

        const getMessagePrompts = (userMessages: ChatMessage[], result: string[]): string[] => {
            for (let j = userMessages.length - 1; j >= 0; j--) {
                const text = userMessages[j].text;
                if (text && !seenTexts.has(text)) {
                    result.push(text);
                    seenTexts.add(text);

                    if (result.length >= 3) {
                        return result;
                    }
                }
            }
            return result;
        };

        result = getMessagePrompts(streamingMessageGroup?.userMessages || [], result);

        for (let i = messageGroups.length - 1; i >= 0; i--) {
            const messageGroup = messageGroups[i];
            const userMessages = messageGroup.userMessages;

            result = getMessagePrompts(userMessages, result);

            if (result.length >= 3) {
                break;
            }
        }
        return result;
    }, [messageGroups, streamingMessageGroup?.userMessages]);

    useEffect(() => {
        if (streamingMessageGroup && !isAgentMessagesEmpty(streamingMessageGroup)) {
            if (!isChatAtBottom()) {
                setDownButtonState({ visible: true, flash: !!isAgentTyping });
            } else {
                setDownButtonState({ visible: false, flash: false });
            }
        }
    }, [streamingMessageGroup, isAgentTyping]);

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
        messageGroups,
        isLoading: isLoadingInitialChatHistory,
        isAgentTyping,
        isWaitingForStreamingMessages,
        streamingMessageGroup,
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
        updateApprovalOrCliMessageInStreamingMessage,
        threadIdUsedForCreatingNewThread,

        isDeepInvestigationButtonEnabled,
        isDeepInvestigationTurnedOn,
        isDeepInvestigationDialogVisible,
        setIsDeepInvestigationDialogVisible,
        onClickDeepInvestigationDialogActionButton,
        onClickDeepInvestigationButton,
    };
};
