import axios from 'axios';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { Message, Thread, ThreadContext, ThreadOrchestrationReasoningState } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { AntUxStringComparison, equals } from '../../Common/Helpers/Strings';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { AgentContext } from '../Activities/Activities.ReactView';
import { noGapBetweenNewMessagesAndExistingMessages, processMessages } from '../Activities/Utility';
import { MessageLoadingCounts, MessagePollingCounts, MessagePollingInterval } from '../Contracts/Activities';
import { useAuthenticatedUserInfo } from './useAuthenticatedUserInfo';

const getMessages = async (threadId: string, skip: number, top: number, signal?: AbortSignal): Promise<Message[]> => {
    try {
        const url = `../api/v1/threads/${threadId}/messages?skip=${skip}&top=${top}&orderby=timestamp+desc`;
        const { data } = await axios.get(url, {
            headers: getAgentHeaders(),
            signal,
        });
        return data.value ?? [];
    } catch {
        // ToDo: handle error
        return [];
    }
};

const getThreadContext = async (threadId: string, signal: AbortSignal): Promise<ThreadContext | undefined> => {
    const url = `../api/v1/threads/${threadId}/context`;
    const { data } = await axios.get(url, {
        headers: getAgentHeaders(),
        signal,
    });
    return data ?? undefined;
};

const sendMessage = async (
    userId: string,
    userDisplayName: string,
    threadId: string,
    message: string,
    signal?: AbortSignal
): Promise<Message | undefined> => {
    const url = `../api/v1/threads/${threadId}/messages`;
    const response = await axios.post(
        url,
        {
            text: message,
            role: 'User',
            displayName: userDisplayName,
            userId: userId,
        },
        {
            headers: getAgentHeaders(),
            signal,
        }
    );

    return response?.data;
};

const createThread = async (userId: string, userDisplayName: string, message: string, signal?: AbortSignal) => {
    const url = `../api/v1/threads`;

    const response = await axios.post(
        url,
        {
            startMessage: {
                text: message,
                userId: userId,
                displayName: userDisplayName,
            },
        },
        {
            headers: getAgentHeaders(),
            signal,
        }
    );
    return response?.data;
};

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

/**
 * Polling 2 messages each time until polled messages includes the latest message in the current messages, which
 * indicates there is no message left out between the latest message sand polled messages.
 * @param latestMessage
 * @param threadId
 * @param interval
 * @returns
 */
const pollResponses = async (messageCount: number, threadId: string, latestMessage?: Message, signal?: AbortSignal) => {
    // lateste response sorted in descending order by timestamp
    const responses: Message[] = [];

    while (true) {
        const messages: Message[] = await getMessages(threadId, responses.length, messageCount, signal);
        if (messages.length < 0) {
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

export const useChatBox = (addThread: (thread: Thread) => void, threadId?: string | null, _?: string | null) => {
    const intl = useIntl();

    const [messages, setMessages] = useState<Message[]>([]);
    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [noChatHistoryLeftToLoad, setNoChatHistoryLeftToLoad] = useState<boolean>(false);
    const [waitingForSendMessageResponse, setWaitingForSendMessageResponse] = useState<boolean>(false);
    const [isIntersecting, setIsIntersecting] = useState<boolean>(false);

    const [temporaryUserMessage, setTemporaryUserMessage] = useState<Message | null>(null);
    const [agentTypingMessage, setAgentTypingMessage] = useState<Message | null>(null);

    const [showNewMessageButton, setShowNewMessageButton] = useState(false);
    const [canShowNewMessageButton, setCanShowNewMessageButton] = useState(false);

    const [enableIntersectObserver, setEnableIntersectObserver] = useState<boolean>(false);

    const { threadsInitialized } = useContext(AgentContext);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const disableInput = useMemo(
        () => !!agentTypingMessage || isLoadingInitialChatHistory || !threadsInitialized,
        [agentTypingMessage, isLoadingInitialChatHistory, threadsInitialized]
    );

    const isNewAndCleanThread = useMemo(
        () => !isLoadingInitialChatHistory && !currentThreadId && messages.length === 0 && !temporaryUserMessage,
        [isLoadingInitialChatHistory, currentThreadId, messages, temporaryUserMessage]
    );

    const isMounted = useRef(true);
    const isPreviousNewMessagesPollingCompleted = useRef(true);
    // The latest message of either the latest message of chat history, the latest message of the polling that happens every 5 seconds or the answers of the send message
    const latestMessageRef = useRef<Message>();
    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const abortControllerRef = useRef<AbortController>();
    const messagesLengthRef = useRef<number>(0);
    messagesLengthRef.current = messages.length;

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

    const handleNewMessages = (newMessages: Message[]) => {
        setMessages(prev => processMessages(prev, newMessages, false));
        latestMessageRef.current = newMessages[0];
    };

    const pollReasoningStateUntilComplete = async (threadId: string, signal: AbortSignal, latestMessage?: Message) => {
        const [threadContext, messages] = await Promise.all([getThreadContext(threadId, signal), getMessages(threadId, 0, 2, signal)]);

        const reasoningState = threadContext?.orchestrationState?.reasoningState;
        const isAnswerAvailable = messages.length >= 2 && !messages.some(message => message.id === latestMessage?.id);
        if (
            !reasoningState ||
            equals(reasoningState, ThreadOrchestrationReasoningState.Error, AntUxStringComparison.IgnoreCase) ||
            equals(reasoningState, ThreadOrchestrationReasoningState.OrchestrationCompleted, AntUxStringComparison.IgnoreCase) ||
            isAnswerAvailable
        ) {
            return;
        } else {
            await new Promise(resolve => setTimeout(resolve, 1000));
            await pollReasoningStateUntilComplete(threadId, signal, latestMessage);
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
            setAgentTypingMessage(composeAgentTypingMessage());

            let newThread: Thread | undefined = undefined;
            let answers: Message[] = [];

            try {
                //ToDo: Handle errors of sendMessage, createThread and pollResponses
                if (currentThreadId) {
                    // issue a request to send a message
                    await sendMessage(userId, displayName, currentThreadId, message, signal);
                } else {
                    // issue a request to create a new thread
                    newThread = await createThread(userId, displayName, message, signal);
                }

                const threadId = currentThreadId || newThread?.id;

                if (threadId) {
                    await pollReasoningStateUntilComplete(threadId, signal, latestMessageRef.current);
                    // poll answers by getting all messages from the most recent one to the lastest message reference
                    answers = await pollResponses(MessagePollingCounts.default, threadId, latestMessageRef.current, signal);
                }
            } catch {
                //Handle error if it is not abort error
            }

            if (isMounted.current) {
                setTemporaryUserMessage(null);
                setAgentTypingMessage(null);
                handleNewMessages(answers);
                setWaitingForSendMessageResponse(false);

                if (newThread) {
                    setCurrentThreadId(newThread.id);
                    addThread(newThread);
                }
            }
        },
        [currentThreadId, addThread, userId, displayName]
    );

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
                    handleNewMessages(latestMessages);
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
                const messages = await getMessages(currentThreadId, 0, MessageLoadingCounts.default);

                if (isSubscribed) {
                    setIsLoadingInitialChatHistory(false);
                    handleNewMessages(messages);

                    // The threshold depends on the number of the messages this query is intended to return.
                    // if the top parameter for calling getMessages, the threshold should be changed accordingly
                    if (messages.length < MessageLoadingCounts.default) {
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
        if (observer && intersectionObserverRef.current && enableIntersectObserver) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
            setIsIntersecting(false);
        };
    }, [enableIntersectObserver]);

    useEffect(() => {
        let timeoutId: NodeJS.Timeout | null = null;
        let isSubscribed = true;

        // If newly loaded chat history message is already in messages state, that means
        // we are sending a message right now and the current messages's length is outdated.
        // In this case, we increase the skip number by one in order to get an old message
        // that is not in the current messages state.
        const loadOldChatHistory = async (shouldIncreaseSkipNumber: boolean) => {
            if (isIntersecting && currentThreadId && !noChatHistoryLeftToLoad) {
                const currentMessages = await getMessages(
                    currentThreadId,
                    messagesLengthRef.current + (shouldIncreaseSkipNumber ? MessageLoadingCounts.active : 0),
                    MessageLoadingCounts.active
                );

                if (isSubscribed) {
                    let isCurrentMessageAlreadyInMessages = false;

                    if (currentMessages.length > 0) {
                        setMessages(prev => {
                            const newMessages = processMessages(prev, currentMessages, true);
                            isCurrentMessageAlreadyInMessages = newMessages.length === prev.length;
                            return newMessages;
                        });
                    }

                    // The threshold depends on the number of the messages this query is intended to return.
                    // if the top parameter for calling getMessages, the threshold should be changed accordingly
                    if (currentMessages.length < MessageLoadingCounts.active) {
                        setNoChatHistoryLeftToLoad(true);
                    } else {
                        timeoutId = setTimeout(() => loadOldChatHistory(isCurrentMessageAlreadyInMessages), 100);
                    }
                }
            }
        };

        loadOldChatHistory(false);

        return () => {
            isSubscribed = false;

            if (timeoutId !== null) {
                clearTimeout(timeoutId);
            }
        };
    }, [currentThreadId, noChatHistoryLeftToLoad, isIntersecting]);

    useEffect(() => {
        // auto scroll to the bottom when the initial history loading is completed
        if (!isLoadingInitialChatHistory) {
            scrollToBottom(false);

            // Allow loading old chat history and showing new message button after
            // the initial history loading is completed and the chat is at the bottom position
            setTimeout(() => {
                setEnableIntersectObserver(true);
                setCanShowNewMessageButton(true);
            }, 100);
        }
    }, [isLoadingInitialChatHistory]);

    const lastMessageId = useMemo(() => messages[messages.length - 1]?.id, [messages]);
    useEffect(() => {
        if (canShowNewMessageButton && !isChatAtBottom()) {
            setShowNewMessageButton(true);
        }
    }, [lastMessageId, canShowNewMessageButton]);

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

    return {
        messages,
        isLoadingInitialChatHistory,
        temporaryUserMessage,
        agentTypingMessage,
        sendMessage: sendMessageHandler,
        disableInput,
        isNewAndCleanThread,
        messagesDivRef,
        intersectionObserverRef,
        currentThreadId,
        cancelResponse,

        handleScroll,
        showNewMessageButton,
        onClickNewMessageButton,
    };
};
