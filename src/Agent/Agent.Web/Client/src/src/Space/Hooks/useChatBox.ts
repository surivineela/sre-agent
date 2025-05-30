import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { MessageClient } from '../../Common/Clients/MessageClient';
import { Message, Thread, ThreadOrchestrationReasoningState } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { PromptResources, SreAgentResources, ThreadContextStateResources } from '../../Strings/SREAgentResources';
import { noGapBetweenNewMessagesAndExistingMessages, processNewMessages } from '../Activities/Utility';
import { MessageLoadingCounts, MessagePollingCounts, MessagePollingInterval } from '../Contracts/Activities';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';
import { ThreadClient } from '../../Common/Clients/ThreadClient';

const composeTemporaryUserMessage = (userId: string, userDisplayName: string, message: string): Message => {
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

export const useChatBox = (addThread: (thread: Thread) => void, promoteThread: () => void, threadId?: string | null, _?: string | null) => {
    const intl = useIntl();

    const [messages, setMessages] = useState<Message[]>([]); // All messages are in the descending order by timeStamp
    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [noChatHistoryLeftToLoad, setNoChatHistoryLeftToLoad] = useState<boolean>(false);
    const [waitingForSendMessageResponse, setWaitingForSendMessageResponse] = useState<boolean>(false);
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);
    const [isThreadOnTop, setIsThreadOnTop] = useState<boolean>(false);

    const [temporaryUserMessage, setTemporaryUserMessage] = useState<Message | null>(null);
    const [agentTypingMessage, setAgentTypingMessage] = useState<Message | null>(null);
    const [threadOrchestrationReasoningState, setThreadOrchestrationReasoningState] = useState<string>();

    const [showNewMessageButton, setShowNewMessageButton] = useState(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const messageClient = MessageClient.getInstance(sreAgentEndpoint);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const disableInput = useMemo(
        () => !!agentTypingMessage || isLoadingInitialChatHistory,
        [agentTypingMessage, isLoadingInitialChatHistory]
    );

    const isNewAndCleanThread = useMemo(
        () => !isLoadingInitialChatHistory && !currentThreadId && messages.length === 0 && !temporaryUserMessage,
        [isLoadingInitialChatHistory, currentThreadId, messages, temporaryUserMessage]
    );

    const getThreadOrchestrationReasoningStateDisplayString = (state?: ThreadOrchestrationReasoningState) => {
        switch (state?.toLowerCase()) {
            case ThreadOrchestrationReasoningState.OrchestrationInitialized.toLowerCase():
                return intl.formatMessage(ThreadContextStateResources.initializing);
            case ThreadOrchestrationReasoningState.Waiting.toLowerCase():
                return intl.formatMessage(ThreadContextStateResources.waiting);
            case ThreadOrchestrationReasoningState.PlanningNextAction.toLowerCase():
                return intl.formatMessage(ThreadContextStateResources.determiningNextSteps);
            case ThreadOrchestrationReasoningState.RunningFunctionCall.toLowerCase():
                return intl.formatMessage(ThreadContextStateResources.generatingAResponse);
            case ThreadOrchestrationReasoningState.Error.toLowerCase():
                return intl.formatMessage(ThreadContextStateResources.somethingWentWrong);
            default:
                return undefined;
        }
    };

    const isMounted = useRef(true);
    const isPreviousNewMessagesPollingCompleted = useRef(true);
    // The latest message of either the latest message of chat history, the latest message of the polling that happens every 5 seconds or the answers of the send message
    const latestMessageRef = useRef<Message>();
    const oldestMessageRef = useRef<Message>();
    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const abortControllerRef = useRef<AbortController>();
    const loadOldChatHistoryCallId = useRef<number>(0);

    const scrollToBottom = (smooth: boolean) =>
        messagesDivRef.current?.scrollTo({ top: messagesDivRef.current.scrollHeight, behavior: smooth ? 'smooth' : undefined });

    const isChatAtBottom = () =>
        messagesDivRef.current &&
        messagesDivRef.current.scrollHeight - messagesDivRef.current.offsetHeight - messagesDivRef.current.scrollTop <= 2;

    const handleScroll = debounce(() => {
        const isAtBottom = isChatAtBottom();

        if (isAtBottom) {
            setShowNewMessageButton(false);
        }
    }, 300);

    const onClickNewMessageButton = () => {
        scrollToBottom(false);
        setShowNewMessageButton(false);
    };

    const cancelResponse = useCallback(() => {
        abortControllerRef.current?.abort();
    }, []);

    const composeAgentTypingMessage = useCallback((): Message => {
        return {
            id: Guid.newGuid(),
            timeStamp: new Date().toISOString(),
            author: {
                role: 'SREAgent',
                userId: Guid.newGuid(),
                displayName: intl.formatMessage(SreAgentResources.sreAgent),
            },
            text: '',
        };
    }, [intl]);

    /**
     *
     * @param newMessages messages in desceding order by timeStamp
     * @param shouldAutoScrollOrShowNewMessagesButton
     */
    const handleNewMessages = (newMessages: Message[], shouldAutoScrollOrShowNewMessagesButton: boolean) => {
        setMessages(prev => {
            const updatedMessages = processNewMessages(prev, newMessages);

            const wasAtBottom = isChatAtBottom();
            const hasNewMessages = updatedMessages.length > 0 && updatedMessages[0].id !== prev[0]?.id;

            if (shouldAutoScrollOrShowNewMessagesButton && hasNewMessages) {
                setTimeout(() => {
                    if (wasAtBottom) {
                        scrollToBottom(true);
                    } else {
                        setShowNewMessageButton(true);
                    }
                }, 100);
            }

            return updatedMessages;
        });

        latestMessageRef.current = newMessages[0];
    };

    /**
     * Polling 2 messages each time until polled messages includes the latest message in the current messages, which
     * indicates there is no message left out between the latest message sand polled messages.
     * @param latestMessage
     * @param threadId
     * @param interval
     * @returns
     */
    const pollResponses = async (messageCount: number, threadId: string, latestMessage?: Message, signal?: AbortSignal) => {
        // latest response sorted in descending order by timestamp
        const responses: Message[] = [];

        while (true) {
            const messagesResponse = await messageClient.getMessages(
                threadId,
                {
                    skip: responses.length,
                    top: messageCount,
                    descending: true,
                },
                signal
            );

            const messages = messagesResponse.content || [];
            if (messagesResponse.isSuccessful && messages.length < 0) {
                break;
            } else {
                responses.push(...messages);
                if (noGapBetweenNewMessagesAndExistingMessages(messages, latestMessage)) {
                    break;
                }
            }
        }

        return [...responses];
    };

    const waitUntilNewMessageIsAvailable = async (threadId: string, signal: AbortSignal, latestMessage?: Message) => {
        const [threadContextResponse, messagesResponse] = await Promise.all([
            threadClient.getThreadContext(threadId, signal),
            messageClient.getMessages(threadId, { skip: 0, top: 2, descending: true }, signal),
        ]);

        const reasoningState = threadContextResponse.content?.orchestrationState?.reasoningState;
        const messages = messagesResponse.content || [];

        const isAnswerAvailable = messages.length >= 2 && !messages.some(message => message.id === latestMessage?.id);
        if (isAnswerAvailable) {
            return;
        } else {
            setThreadOrchestrationReasoningState(getThreadOrchestrationReasoningStateDisplayString(reasoningState));
            await new Promise(resolve => setTimeout(resolve, 1000));
            await waitUntilNewMessageIsAvailable(threadId, signal, latestMessage);
        }
    };

    const sendMessageHandler = useCallback(
        async (message: string) => {
            if (abortControllerRef.current) {
                abortControllerRef.current.abort();
            }
            abortControllerRef.current = new AbortController();
            const { signal } = abortControllerRef.current;

            setWaitingForSendMessageResponse(true);
            setTemporaryUserMessage(composeTemporaryUserMessage(userId, displayName, message));
            setThreadOrchestrationReasoningState(undefined);
            setAgentTypingMessage(composeAgentTypingMessage());

            let newThread: Thread | undefined = undefined;
            let answers: Message[] = [];

            try {
                //ToDo: Handle errors of sendMessage, createThread and pollResponses
                if (currentThreadId) {
                    // issue a request to send a message
                    await messageClient.postMessage(
                        currentThreadId,
                        {
                            userId,
                            userDisplayName: displayName,
                            message,
                        },
                        signal
                    );
                } else {
                    // issue a request to create a new thread
                    newThread = (await threadClient.createThread({ userId, userDisplayName: displayName, message }, signal)).content;
                }

                const threadId = currentThreadId || newThread?.id;

                if (threadId) {
                    await waitUntilNewMessageIsAvailable(threadId, signal, latestMessageRef.current);
                    // poll answers by getting all messages from the most recent one to the lastest message reference
                    answers = await pollResponses(MessagePollingCounts.default, threadId, latestMessageRef.current, signal);
                }
            } catch {
                //Handle error if it is not abort error
            }

            if (isMounted.current) {
                setTemporaryUserMessage(null);
                setAgentTypingMessage(null);
                setThreadOrchestrationReasoningState(undefined);
                handleNewMessages(answers, true);
                setWaitingForSendMessageResponse(false);

                if (newThread) {
                    setCurrentThreadId(newThread.id);
                    addThread(newThread);
                } else if (!isThreadOnTop) {
                    promoteThread();
                    setIsThreadOnTop(true);
                }
            }
        },
        [currentThreadId, isThreadOnTop, addThread, promoteThread, userId, displayName]
    );

    const loadOldChatHistory = useCallback(async () => {
        if (currentThreadId && oldestMessageRef.current) {
            const callId = loadOldChatHistoryCallId.current;
            const currentMessagesResponse = await messageClient.getMessages(
                currentThreadId,
                {
                    skip: 0,
                    top: MessageLoadingCounts.active,
                    descending: true,
                    maxTimestamp: oldestMessageRef.current.timeStamp,
                }
            );

            if (callId === loadOldChatHistoryCallId.current) {
                const currentMessages = currentMessagesResponse.content || [];
                setMessages(prev => processNewMessages(prev, currentMessages));
                if (currentMessagesResponse.isSuccessful && currentMessages.length < MessageLoadingCounts.active) {
                    setNoChatHistoryLeftToLoad(true);
                }
            }
        }
    }, [currentThreadId]);

    useEffect(() => {
        loadOldChatHistoryCallId.current += 1
    }, [currentThreadId])

    useEffect(() => {
        let isSubscribed = true;

        const pollMessages = async () => {
            if (
                currentThreadId &&
                !isLoadingInitialChatHistory &&
                !waitingForSendMessageResponse &&
                isPreviousNewMessagesPollingCompleted.current
            ) {
                isPreviousNewMessagesPollingCompleted.current = false;

                const latestMessages = await pollResponses(MessagePollingCounts.default, currentThreadId, latestMessageRef.current);
                if (isSubscribed && latestMessages && latestMessages.length > 0) {
                    handleNewMessages(latestMessages, true);
                }

                isPreviousNewMessagesPollingCompleted.current = true;
            }
        };

        const interval = setInterval(pollMessages, MessagePollingInterval.default);

        return () => {
            clearInterval(interval);
            isSubscribed = false;
            isPreviousNewMessagesPollingCompleted.current = true;
        };
    }, [currentThreadId, isLoadingInitialChatHistory, waitingForSendMessageResponse]);

    // Load the latest 20 chat message history
    useEffect(() => {
        let isSubscribed = true;

        const loadLatest20ChatHistory = async () => {
            if (currentThreadId) {
                const messagesResponse = await messageClient.getMessages(currentThreadId, {
                    skip: 0,
                    top: MessageLoadingCounts.default,
                    descending: true,
                });

                const messages = messagesResponse.content || [];

                if (isSubscribed) {
                    setIsLoadingInitialChatHistory(false);
                    setMessages(messages);
                    latestMessageRef.current = messages.length > 0 ? messages[0] : undefined;

                    // The threshold depends on the number of the messages this query is intended to return.
                    // if the top parameter for calling getMessages, the threshold should be changed accordingly
                    if (messagesResponse.isSuccessful && messages.length < MessageLoadingCounts.default) {
                        setNoChatHistoryLeftToLoad(true);
                    }
                }
            } else {
                setIsLoadingInitialChatHistory(false);
                setNoChatHistoryLeftToLoad(true);
            }
        };

        loadLatest20ChatHistory();

        return () => {
            isSubscribed = false;
        };
    }, [currentThreadId]);

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
            const loadOldMessages = async () => {
                await loadOldChatHistory();

                timeoutId = setTimeout(loadOldChatHistory, 100);
            }

            loadOldMessages();
        }

        return () => {
            clearTimeout(timeoutId);
        };
    }, [loadOldChatHistory, noChatHistoryLeftToLoad, isIntersecting]);

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
        if (temporaryUserMessage && agentTypingMessage) {
            scrollToBottom(true);
        }
    }, [temporaryUserMessage, agentTypingMessage]);

    useEffect(() => {
        isMounted.current = true;

        return () => {
            isMounted.current = false;
        };
    }, []);

    useEffect(() => {
        oldestMessageRef.current = messages.length > 0 ? messages[messages.length - 1] : undefined;
    }, [messages]);

    return {
        messages,
        isLoadingInitialChatHistory,
        temporaryUserMessage,
        agentTypingMessage,
        threadOrchestrationReasoningState,
        sendMessage: sendMessageHandler,
        disableInput,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelResponse,
        prompts,
        messagePromptsUsed,
        handleScroll,
        showNewMessageButton,
        onClickNewMessageButton,
    };
};
