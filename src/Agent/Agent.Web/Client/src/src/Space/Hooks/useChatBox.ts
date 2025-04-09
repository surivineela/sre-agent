import { Message, Thread } from '../../Common/Contracts/SreAgent';
import { useState, useCallback, useEffect, useRef, useContext, useMemo } from 'react';
import { Guid } from '../../Common/Helpers/Guid';
import { AgentContext } from '../Activities/Activities.ReactView';
import { MessagePollingInterval } from '../Contracts/Activities';
import { Activities } from '../../Strings/SREResources.resjson';
import axios from 'axios';

const user = {
  displayName: 'Web Client User',
  userId: 'web-client-user',
}
const getMessages = async (threadId: string) => {
  const url = `../api/v1/threads/${threadId}/messages`;
    const { data } = await axios.get(url);
  return data.value ?? [];
};

const sendMessage = async (threadId: string, message: string) => {
  const { userId, displayName } = user;
  const url = `../api/v1/threads/${threadId}/messages`;
  await axios.post(url, {
    text: message,
    role: 'User',
    displayName: displayName,
    userId: userId,
  });
};
                         
const createThread = async (message: string) => {
  const { userId, displayName } = user;
  const url = `../api/v1/threads`;

  const response = await axios.post(url, {
    startMessage: {
      text: message,
      userId: userId,
      displayName: displayName,
    }
  });
  return response?.data;
};

const getNewMessages = (oldMessages: Message[], newMessages: Message[]) => {
  return newMessages.slice(oldMessages.length);
};

const composeTemporaryUserMessage = (message: string): Message => {
  return {
    id: Guid.newGuid(),
    timestamp: new Date().toISOString(),
    author: {
      role: 'User',
      userId: Guid.newGuid(),
      // Currently we are not displaying the user name in the chat box, so we can ignore it for now.
      displayName: '',
    },
    text: message,
  };
};

const composeAgentTypingMessage = (): Message => {
  return {
    id: Guid.newGuid(),
    timestamp: new Date().toISOString(),
    author: {
      role: 'SREAgent',
      userId: Guid.newGuid(),
      displayName: Activities.sreAgentDisplayName,
    },
    text: '',
  };
};

export const useChatBox = (addThread: (thread: Thread) => void, threadId?: string | null) => {
  const [messages, setMessages] = useState<Message[]>([]);
  const [currentThreadId, setCurrentThreadId] = useState<string | null>(threadId || null);
  const [shouldLoadHistory, setShouldLoadHistory] = useState<boolean>(true);
  const [temporaryUserMessage, setTemporaryUserMessage] = useState<Message | null>(null);
  const [agentTypingMessage, setAgentTypingMessage] = useState<Message | null>(null);
  const [pausePolling, setPausePolling] = useState<boolean>(true);
  const { threadsInitialized } = useContext(AgentContext);

  const disableInput = useMemo(
    () => !!agentTypingMessage || shouldLoadHistory || !threadsInitialized,
    [agentTypingMessage, shouldLoadHistory, threadsInitialized]
  );

  const isMounted = useRef(true);
  const latestMessageIdRef = useRef<string>('');

  const sendMessageHandler = useCallback(
    async (message: string) => {
     
          setPausePolling(true);
          setTemporaryUserMessage(composeTemporaryUserMessage(message));
          setAgentTypingMessage(composeAgentTypingMessage());

        if (currentThreadId) {
          // issue a request to send a message
          await sendMessage(currentThreadId, message);

          // poll messages until the first answer is received.
          const newMessages: any[] = await getMessages(currentThreadId);

          if (isMounted.current) {
              if (newMessages[newMessages.length - 1]?.id) {
                latestMessageIdRef.current = newMessages[newMessages.length - 1].id;
              }

              setTemporaryUserMessage(null);
              setAgentTypingMessage(null);
              setMessages(prev => [...prev, ...getNewMessages(prev, newMessages)]);
              setPausePolling(false);
          }
        } else {
          // issue a request to create a new thread
          const newThread: any = await createThread(message);
          if (newThread) {
            // poll messages until the first answer is received
            const newMessages: any[] = await getMessages(newThread.id);

            if (isMounted.current) {
                latestMessageIdRef.current = newMessages[newMessages.length - 1]?.id ?? '';
                setTemporaryUserMessage(null);
                setAgentTypingMessage(null);
                setMessages(prev => [...prev, ...getNewMessages(prev, newMessages)]);
                setPausePolling(false);

                setCurrentThreadId(newThread.id);
                addThread(newThread);
            }
          }
        }
    },
    [currentThreadId, addThread]
  );

  useEffect(() => {
    let isSubscribed = true;

    const pollMessages = async () => {
      if (currentThreadId && !pausePolling && !shouldLoadHistory) {
        const latestMessages: any[] = await getMessages(currentThreadId);

        const newMessageId = latestMessages[latestMessages.length - 1]?.id;

        if (isSubscribed && newMessageId && newMessageId !== latestMessageIdRef.current) {
          latestMessageIdRef.current = newMessageId;
          setMessages(prev => {
            const newMessages = getNewMessages(prev, latestMessages);
            return [...prev, ...newMessages];
          });
        }
      }
    };

    const timer = setInterval(pollMessages, MessagePollingInterval.default);

    return () => {
      clearInterval(timer);
      isSubscribed = false;
    };
  }, [currentThreadId, pausePolling, shouldLoadHistory]);

  useEffect(() => {
    let isSubscribed = true;
    if (shouldLoadHistory) {
      if (currentThreadId) {
        const loadHistory = async () => {
          const messages = await getMessages(currentThreadId);
          if (isSubscribed) {
              setShouldLoadHistory(false);
              setMessages(messages);
              setPausePolling(false);
          }
        };

        loadHistory();
      } else {
        setShouldLoadHistory(false);
      }
    }

    return () => {
      isSubscribed = false;
    };
  }, [currentThreadId, shouldLoadHistory]);

  useEffect(() => {
    isMounted.current = true;

    return () => {
      isMounted.current = false;
    };
  }, []);

  return {
    messages,
    temporaryUserMessage,
    agentTypingMessage,
    shouldLoadHistory,
    sendMessage: sendMessageHandler,
    disableInput,
  };
};
