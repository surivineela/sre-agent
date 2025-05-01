import axios from 'axios';
import debounce from 'lodash/debounce';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { Message, Thread } from '../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../Common/Helpers/Guid';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
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

export const useChatBox = (addThread: (thread: Thread) => void, threadId?: string | null) => {
    const intl = useIntl();

    const [messages, setMessages] = useState<Message[]>([]);
    const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

    const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
    const [noChatHistoryLeftToLoad, setNoChatHistoryLeftToLoad] = useState<boolean>(false);
    const [waitingForSendMessageResponse, setWaitingForSendMessageResponse] = useState<boolean>(false);

    const [temporaryUserMessage, setTemporaryUserMessage] = useState<Message | null>(null);
    const [agentTypingMessage, setAgentTypingMessage] = useState<Message | null>(null);

    const [enableIntersectObserver, setEnableIntersectObserver] = useState<boolean>(false);

    const { threadsInitialized } = useContext(AgentContext);

    const {
        userIdAndDisplayName: { userId, displayName },
    } = useAuthenticatedUserInfo();

    const disableInput = useMemo(
        () => !!agentTypingMessage || isLoadingInitialChatHistory || !threadsInitialized,
        [agentTypingMessage, isLoadingInitialChatHistory, threadsInitialized]
    );

    const isMounted = useRef(true);
    const isPreviousNewMessagesPollingCompleted = useRef(true);
    const isPreviousChatHistoryLoadingCompleted = useRef(true);
    // the latest message of either the latest message of chat history or the latest message of the polling that happens every 5 seconds
    const latestMessageRef = useRef<Message>();
    const messagesDivRef = useRef<HTMLDivElement>(null);
    const intersectionObserverRef = useRef<HTMLDivElement>(null);
    const abortControllerRef = useRef<AbortController>();

    const isDownButtonVisible =
        messagesDivRef.current &&
        messagesDivRef.current.scrollHeight - messagesDivRef.current.offsetHeight - messagesDivRef.current.scrollTop > 2;

    const scrollToBottom = () => messagesDivRef.current?.scrollTo({ top: messagesDivRef.current.scrollHeight, behavior: 'smooth' });

    const onClickDownButton = useCallback(() => {
        scrollToBottom();
    }, []);

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
                displayName: intl.formatMessage(ActivitiesResources.sreAgentDisplayName),
            },
            text: '',
        };
    }, [intl]);

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
                    // poll answers by get the latest 5 messages
                    answers = await pollResponses(MessagePollingCounts.active, threadId, undefined, signal);
                }
            } catch {
                //Handle error if it is not abort error
            }

            if (isMounted.current) {
                setTemporaryUserMessage(null);
                setAgentTypingMessage(null);
                setMessages(prev => processMessages(prev, answers, false));
                setWaitingForSendMessageResponse(false);

                if (newThread) {
                    setCurrentThreadId(newThread.id);
                    addThread(newThread);
                }
            }
        },
        [currentThreadId, addThread, userId, displayName]
    );

    const onIntersect = debounce(async (entries: IntersectionObserverEntry[]) => {
        const entry = entries[0];

        if (entry.isIntersecting && currentThreadId && isPreviousChatHistoryLoadingCompleted.current && !noChatHistoryLeftToLoad) {
            isPreviousChatHistoryLoadingCompleted.current = false;
            const currentMessages = await getMessages(currentThreadId, messages.length, MessageLoadingCounts.active);

            if (isMounted.current) {
                if (currentMessages.length > 0) {
                    setMessages(prev => processMessages(prev, currentMessages, true));
                }

                // The threshold depends on the number of the messages this query is intended to return.
                // if the top parameter for calling getMessages, the threshold should be changed accordingly
                if (currentMessages.length < MessageLoadingCounts.active) {
                    setNoChatHistoryLeftToLoad(true);
                }
            }

            isPreviousChatHistoryLoadingCompleted.current = true;
        }
    }, 100);

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
                    setMessages(prev => processMessages(prev, latestMessages, false));
                    latestMessageRef.current = latestMessages[0];
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
                    setMessages(prev => processMessages(prev, messages, true));
                    latestMessageRef.current = messages[0];

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
        const observer = new IntersectionObserver(onIntersect);
        if (observer && intersectionObserverRef.current && enableIntersectObserver) {
            observer.observe(intersectionObserverRef.current);
        }

        return () => {
            observer?.disconnect();
        };
    }, [messages, enableIntersectObserver]);

    useEffect(() => {
        // auto scroll to the bottom when the initial history loading is completed, and a new question is sent, or waiting for the answers
        if (!isLoadingInitialChatHistory) {
            scrollToBottom();
            setEnableIntersectObserver(true);
        }
    }, [temporaryUserMessage, agentTypingMessage, isLoadingInitialChatHistory]);

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
        messagesDivRef,
        onClickDownButton,
        isDownButtonVisible,
        intersectionObserverRef,
        currentThreadId,
        cancelResponse,
    };
};
