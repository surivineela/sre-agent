import { Message, Thread } from '../../Common/Contracts/Azure/SreAgent';
import { useState, useCallback, useEffect, useRef, useContext, useMemo } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import { AgentContext } from '../Activities/Activities.ReactView';
import { MessageLoadingCounts, MessagePollingCounts, MessagePollingInterval } from '../Contracts/Activities';
import { Activities } from '../../Strings/SREResources.resjson';
import axios from 'axios';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import debounce from 'lodash/debounce';
import { noGapBetweenNewMessagesAndExistingMessages, processMessages } from '../Activities/Utility';

const user = {
  displayName: 'Web Client User',
  userId: 'web-client-user',
}

const getMessages = async (threadId: string, skip: number, top = MessagePollingCounts.default): Promise<Message[]> => {
  try {
    const url = `../api/v1/threads/${threadId}/messages?skip=${skip}&top=${top}&orderby=timestamp+desc`;
    const { data } = await axios.get(url, {
      headers: getAgentHeaders()
    });
    return data.value ?? [];
  } catch {
    // ToDo: handle error
    return [];
  }

};

const sendMessage = async (threadId: string, message: string): Promise<Message | undefined> => {
  try {
    const { userId, displayName } = user;
    const url = `../api/v1/threads/${threadId}/messages`;
    const response = await axios.post(url, {
      text: message,
      role: 'User',
      displayName: displayName,
      userId: userId,
    }, {
      headers: getAgentHeaders()
    });

    return response?.data
  } catch {
    // ToDo: handle error
    return undefined;
  }

};

const sendMessageFeedback = async (threadId: string, isPositive: boolean, feedbackText: string) => {
  try {
    const url = `../api/v1/threads/${threadId}/feedbacks`;
    await axios.post(url, {
      isPositive: isPositive,
      feedbackText: feedbackText
    }, {
      headers: getAgentHeaders()
    });
  } catch {
    // ToDo: handle error
    return undefined;
  }

};

const createThread = async (message: string) => {

  try {
    const { userId, displayName } = user;
    const url = `../api/v1/threads`;

    const response = await axios.post(url, {
      startMessage: {
        text: message,
        userId: userId,
        displayName: displayName,
      }
    }, {
      headers: getAgentHeaders()
    });
    return response?.data;
  } catch {
    // ToDo: handle error
    return undefined;
  }

};

const composeTemporaryUserMessage = (message: string): Message => {
  return {
    id: Guid.newGuid(),
    timeStamp: new Date().toISOString(),
    author: {
      role: 'User',
      userId: Guid.newGuid(),
      // Currently we are not displaying the user name in the chat box, so we can ignore it for now.
      displayName: '',
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
const pollResponses = async (threadId: string, latestMessage?: Message) => {
  // lateste response sorted in descending order by timestamp
  const responses: Message[] = [];

  while (true) {
    const messages: Message[] = await getMessages(threadId, responses.length, MessagePollingCounts.active);
    if (messages.length < 0) {
      break;
    } else {
      responses.push(...messages);
      if (noGapBetweenNewMessagesAndExistingMessages(responses, latestMessage)) {
        break;
      }
    }
  }

  return [...responses];
}

const composeAgentTypingMessage = (): Message => {
  return {
    id: Guid.newGuid(),
    timeStamp: new Date().toISOString(),
    author: {
      role: 'SREAgent',
      userId: Guid.newGuid(),
      displayName: Activities.sreAgentDisplayName,
    },
    text: '',
  };
};

const useChatBox = (addThread: (thread: Thread) => void, threadId?: string | null) => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);

  const [isLoadingInitialChatHistory, setIsLoadingInitialChatHistory] = useState<boolean>(true);
  const [noChatHistoryLeftToLoad, setNoChatHistoryLeftToLoad] = useState<boolean>(false);
  const [waitingForSendMessageResponse, setWaitingForSendMessageResponse] = useState<boolean>(false);

  const [temporaryUserMessage, setTemporaryUserMessage] = useState<Message | null>(null);
  const [agentTypingMessage, setAgentTypingMessage] = useState<Message | null>(null);

  const [enableIntersectObserver, setEnableIntersectObserver] = useState<boolean>(false);

  const { threadsInitialized } = useContext(AgentContext);

  const disableInput = useMemo(
    () => !!agentTypingMessage || isLoadingInitialChatHistory || !threadsInitialized,
    [agentTypingMessage, isLoadingInitialChatHistory, threadsInitialized]
  );

  const isMounted = useRef(true);
  const isPreviousNewMessagesPollingCompleted = useRef(true);
  const isPreviousChatHistoryLoadingCompleted = useRef(true);
  const latestMessageRef = useRef<Message>();
  const messagesDivRef = useRef<HTMLDivElement>(null);
  const intersectionObserverRef = useRef<HTMLDivElement>(null);

  const isDownButtonVisible = messagesDivRef.current && messagesDivRef.current.scrollHeight - messagesDivRef.current.offsetHeight - messagesDivRef.current.scrollTop > 2;

  const scrollToBottom = () => messagesDivRef.current?.scrollTo({ top: messagesDivRef.current.scrollHeight, behavior: 'smooth' });

  const onClickDownButton = useCallback(() => {
    scrollToBottom();
  }, []);

  const sendMessageHandler = useCallback(
    async (message: string) => {

      setWaitingForSendMessageResponse(true);
      setTemporaryUserMessage(composeTemporaryUserMessage(message));
      setAgentTypingMessage(composeAgentTypingMessage());

      let newThread: Thread | undefined = undefined;
      let answers: Message[] = [];

      //ToDo: Handle errors of sendMessage, createThread and pollResponses
      if (currentThreadId) {
        // issue a request to send a message
        const latestMessage = await sendMessage(currentThreadId, message);
        latestMessageRef.current = latestMessage;
      } else {
        // issue a request to create a new thread
        newThread = await createThread(message);
        latestMessageRef.current = newThread?.lastMessage;
      }

      const threadId = currentThreadId || newThread?.id;

      if (threadId) {
        // poll answers
        answers = await pollResponses(threadId, latestMessageRef.current);
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
    [currentThreadId, addThread]
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
    };
  }, 100);

  useEffect(() => {
    let isSubscribed = true;

    const pollMessages = async () => {
      if (currentThreadId && !isLoadingInitialChatHistory && !waitingForSendMessageResponse && isPreviousNewMessagesPollingCompleted.current) {
        isPreviousNewMessagesPollingCompleted.current = false;

        const latestMessages = await pollResponses(currentThreadId, latestMessageRef.current);
        if (isSubscribed && latestMessages && latestMessages.length > 0) {
          setMessages(prev => processMessages(prev, latestMessages, false));
          latestMessageRef.current = latestMessages[0];
        }

        isPreviousNewMessagesPollingCompleted.current = true;
      }
    }

    const interval = setInterval(pollMessages, MessagePollingInterval.default);

    return () => {
      clearInterval(interval);
      isSubscribed = false;
      isPreviousNewMessagesPollingCompleted.current = true;
    };
  }, [currentThreadId, isLoadingInitialChatHistory, waitingForSendMessageResponse]);

  // Load the latest 10 chat message history
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
          if (messages.length < MessagePollingCounts.default) {
            setNoChatHistoryLeftToLoad(true);
          }
        }
      } else {
        setIsLoadingInitialChatHistory(false);
        setNoChatHistoryLeftToLoad(true);
      }
    }

    loadLatest20ChatHistory();

    return () => {
      isSubscribed = false;
    }
  }, [currentThreadId]);

  useEffect(() => {
    const observer = new IntersectionObserver(onIntersect);
    if (observer && intersectionObserverRef.current && enableIntersectObserver) {
      observer.observe(intersectionObserverRef.current);
    }

    return () => {
      observer?.disconnect();
    }
  }, [messages, enableIntersectObserver])

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
    currentThreadId
  };
};

export {
  sendMessageFeedback,
  useChatBox,
};
