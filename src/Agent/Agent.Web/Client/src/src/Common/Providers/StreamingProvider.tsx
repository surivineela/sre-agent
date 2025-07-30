import * as signalR from '@microsoft/signalr';
import { MutableRefObject, ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { StreamingContext } from '../../Space/Contracts/Context';
import AzPortalProxy, { defaultSreAgentEndpoint } from '../AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { standaloneAgentEndpoint, standaloneReactPort } from '../Constants/Uri';
import { MessageRequestType, MessageResponseType, StreamingMessage } from '../Contracts/Azure/Streaming';

export interface MessageCreateRequest {
    text: string;
    userId: string;
    displayName: string;
}

export interface ThreadCreateRequest {
    startMessage: MessageCreateRequest;
}

const useLatestStreamingMessages = () => {
    // key: threadId, value: the latest streaming messages for that thread in the current session
    const latestStreamingMessageRef = useRef<Map<string, StreamingMessage | null | undefined>>(new Map());

    const latestMessageUpdateCallback = (message: StreamingMessage) => {
        const threadId = message?.additionalProperties?.threadId;

        if (threadId) {
            latestStreamingMessageRef.current.set(threadId, message);
        }
    };

    const cleanupLatestStreamingMessages = () => {
        latestStreamingMessageRef.current = new Map();
    };

    return {
        latestStreamingMessageRef,
        latestMessageUpdateCallback,
        cleanupLatestStreamingMessages,
    };
};

const useChatBoxEventStreaming = (latestStreamingMessageRef: MutableRefObject<Map<string, StreamingMessage | null | undefined>>) => {
    // key: threadId, value: the latest streaming messages for that thread in the current session
    const chatMessageHandlersRef = useRef<Map<string, (...args: any[]) => void>>(new Map());

    const subscribeChatStreaming = useCallback(
        (
            threadId: string,
            latestStreamingMessageHandler: (latestStreamingMessage: StreamingMessage | null | undefined) => void,
            messageUpdateHandler: (...args: any[]) => void
        ) => {
            latestStreamingMessageHandler(latestStreamingMessageRef.current.get(threadId));

            chatMessageHandlersRef.current.set(threadId, messageUpdateHandler);
            return () => {
                chatMessageHandlersRef.current.delete(threadId);
            };
        },
        []
    );

    const messageUpdateCallbackFromChatBox = (message: StreamingMessage) => {
        const threadId = message?.additionalProperties?.threadId;

        if (threadId) {
            chatMessageHandlersRef.current.get(threadId)?.(message);
        }
    };

    const cleanupChatMessageStreamingSetup = () => {
        chatMessageHandlersRef.current = new Map();
    };

    return {
        subscribeChatStreaming,
        messageUpdateCallbackFromChatBox,
        cleanupChatMessageStreamingSetup,
    };
};

const useThreadMenuEventStreaming = () => {
    const threadCreateHandlerRef = useRef<((message: StreamingMessage) => void) | null>(null);
    const threadUpdateHandlerRef = useRef<((message: StreamingMessage) => void) | null>(null);

    const subscribeThreadMenuEventStreaming = useCallback(
        (threadCreateHandler: (message: StreamingMessage) => void, threadUpdateHandler: (message: StreamingMessage) => void) => {
            threadCreateHandlerRef.current = threadCreateHandler;
            threadUpdateHandlerRef.current = threadUpdateHandler;

            return () => {
                threadCreateHandlerRef.current = null;
                threadUpdateHandlerRef.current = null;
            };
        },
        []
    );

    const threadCreateCallbackFromThreadMenu = (message: StreamingMessage) => {
        threadCreateHandlerRef.current?.(message);
    };

    const threadUpdateCallbackFromThreadMenu = (message: StreamingMessage) => {
        threadUpdateHandlerRef.current?.(message);
    };

    const cleanupThreadMenuEventStreamingSetup = () => {
        threadUpdateHandlerRef.current = null;
        threadCreateHandlerRef.current = null;
    };

    return {
        subscribeThreadMenuEventStreaming,
        threadCreateCallbackFromThreadMenu,
        threadUpdateCallbackFromThreadMenu,
        cleanupThreadMenuEventStreamingSetup,
    };
};

const useResourceInfoThreadCreateEventStreaming = () => {
    const threadCreateHandlerRef = useRef<((message: StreamingMessage) => void) | null>(null);

    const subscribeResourceInfoThreadCreateEventStreaming = useCallback((threadCreateHandler: (message: StreamingMessage) => void) => {
        threadCreateHandlerRef.current = threadCreateHandler;

        return () => {
            threadCreateHandlerRef.current = null;
        };
    }, []);

    const threadCreateCallbackFromResourceInfo = (message: StreamingMessage) => {
        threadCreateHandlerRef.current?.(message);
    };

    const cleanupResourceInfoThreadCreateEventStreamingSetup = () => {
        threadCreateHandlerRef.current = null;
    };

    return {
        subscribeResourceInfoThreadCreateEventStreaming,
        threadCreateCallbackFromResourceInfo,
        cleanupResourceInfoThreadCreateEventStreamingSetup,
    };
};

export const StreamingProvider = ({ children }: { children?: ReactNode }) => {
    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const [isConnecting, setIsConnecting] = useState(true);
    const [isConnected, setIsConnected] = useState(false);
    const [isReconnecting, setIsReconnecting] = useState(false);
    const [noPermission, setNoPermission] = useState(false);
    const isConnectedRef = useRef(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const { latestStreamingMessageRef, latestMessageUpdateCallback, cleanupLatestStreamingMessages } = useLatestStreamingMessages();
    const { subscribeChatStreaming, messageUpdateCallbackFromChatBox, cleanupChatMessageStreamingSetup } =
        useChatBoxEventStreaming(latestStreamingMessageRef);
    const {
        subscribeThreadMenuEventStreaming,
        threadCreateCallbackFromThreadMenu,
        threadUpdateCallbackFromThreadMenu,
        cleanupThreadMenuEventStreamingSetup,
    } = useThreadMenuEventStreaming();
    const {
        subscribeResourceInfoThreadCreateEventStreaming,
        threadCreateCallbackFromResourceInfo,
        cleanupResourceInfoThreadCreateEventStreamingSetup,
    } = useResourceInfoThreadCreateEventStreaming();

    const sendMessage = (method: MessageRequestType, ...args: any[]) => {
        if (isConnectedRef.current && connectionRef.current) {
            connectionRef.current.invoke(method, ...args).catch(() => {
                //error handling
            });
        }
    };

    const startMessageStreamingOnNewThread = useCallback((newThreadId: string, threadCreateRequest: ThreadCreateRequest) => {
        sendMessage(MessageRequestType.CreateThread, newThreadId, threadCreateRequest, false);
    }, []);

    const startMessageStreamingOnExistingThread = useCallback((threadId: string, messageCreateRequest: MessageCreateRequest) => {
        sendMessage(MessageRequestType.CreateMessage, threadId, messageCreateRequest, false);
    }, []);

    const cancelMessageStreaming = useCallback((threadId: string) => {
        sendMessage(MessageRequestType.CancelThread, threadId);
    }, []);

    const subscribe = () => {
        connectionRef.current?.on(MessageResponseType.MessageUpdate, (message: StreamingMessage) => {
            latestMessageUpdateCallback(message);
            messageUpdateCallbackFromChatBox(message);
            threadUpdateCallbackFromThreadMenu(message);
        });
        connectionRef.current?.on(MessageResponseType.ThreadUpdate, (message: StreamingMessage) => {
            threadCreateCallbackFromThreadMenu(message);
            threadCreateCallbackFromResourceInfo(message);
        });
    };

    const unsubscribe = () => {
        connectionRef.current?.off(MessageResponseType.MessageUpdate);
        connectionRef.current?.off(MessageResponseType.ThreadUpdate);
    };

    useEffect(() => {
        isConnectedRef.current = isConnected;
    }, [isConnected]);

    useEffect(() => {
        let isSubscribed = true;

        const connect = async () => {
            setIsConnecting(true);

            const isLocalHost = window.location.hostname.toLowerCase() === 'localhost';
            const isReactLocalhost =
                isLocalHost && window.location.port === standaloneReactPort && sreAgentEndpoint === defaultSreAgentEndpoint;
            const endpoint = isReactLocalhost ? standaloneAgentEndpoint : sreAgentEndpoint;

            connectionRef.current = new signalR.HubConnectionBuilder()
                // ToDo: Sanitize the endpoint
                .withUrl(`${endpoint}/agentHub`, {
                    accessTokenFactory: () => AzPortalProxy.envInfo.sreAgentToken || '',
                    logMessageContent: isLocalHost,
                })
                .configureLogging({
                    log: (logLevel, message) => {
                        if (
                            isSubscribed &&
                            !isLocalHost &&
                            (logLevel === signalR.LogLevel.Error || logLevel === signalR.LogLevel.Critical)
                        ) {
                            // If the log message has url starting from wss://, then redact the url.
                            // If the log message has string(s) starting from /agentHub, then remove anything after /agentHub.
                            // If the log message has string(s) starting from access_token, then remove it.
                            // If the log message has string(s) that is sreAgentToken, then remove it.
                            const messagesToLog = message
                                .replace(/wss?:\/\/.*$/gi, '[REDACTED_URL]')
                                .replace(/\/agentHub.*$/gi, '/agentHub')
                                .replace(/access_token=.*$/gi, '')
                                .replace(AzPortalProxy.envInfo.sreAgentToken || '', '');
                            proxy.log({
                                action: 'SignalREvent',
                                actionModifier: 'failed',
                                data: {
                                    message: `Error in SignalR Hub connection from agent url: ${sreAgentEndpoint}. Message: ${messagesToLog}`,
                                },
                            });
                        }

                        if (isLocalHost) {
                            console.log(message);
                        }
                    },
                })
                .withAutomaticReconnect()
                .build();

            connectionRef.current.onclose(() => {
                setIsConnected(false);
                setIsConnecting(false);
                setIsReconnecting(false);
            });

            connectionRef.current.onreconnecting(() => {
                setIsReconnecting(true);

                if (isSubscribed && !isLocalHost) {
                    proxy.log({
                        action: 'ReconnectToSignalR',
                        actionModifier: 'started',
                        data: {
                            message: `Reconnecting to SignalR hub from agent url: ${sreAgentEndpoint}`,
                        },
                    });
                }
            });

            connectionRef.current.onreconnected(() => {
                setIsReconnecting(false);

                if (isSubscribed && !isLocalHost) {
                    proxy.log({
                        action: 'ReconnectToSignalR',
                        actionModifier: 'stopped',
                        data: {
                            message: `Reconnected to SignalR hub from agent url: ${sreAgentEndpoint}`,
                        },
                    });
                }
            });

            try {
                await connectionRef.current.start();

                if (isSubscribed) {
                    unsubscribe();
                    subscribe();

                    setIsConnected(true);
                    setNoPermission(false);
                }
            } catch (e) {
                if (isSubscribed) {
                    const isPermissionError =
                        (e instanceof signalR.HttpError && (e.statusCode === 403 || e.statusCode === 401)) ||
                        (e instanceof Error && (e.message.includes('403') || e.message.includes('401')));

                    if (isPermissionError && !isLocalHost) {
                        proxy.log({
                            action: 'ConnectToSignalR',
                            actionModifier: 'failed',
                            logLevel: 'error',
                            data: {
                                // !important: Do not log the entire error as it may contain access token
                                message: `Failed to connect to SignalR hub from agent url: ${sreAgentEndpoint}. Please check your permissions.`,
                            },
                        });
                    }

                    setNoPermission(isPermissionError);
                    setIsConnected(false);
                }
            }
            setIsConnecting(false);
        };

        connect();

        return () => {
            connectionRef.current?.stop();

            cleanupLatestStreamingMessages();
            cleanupChatMessageStreamingSetup();
            cleanupThreadMenuEventStreamingSetup();
            cleanupResourceInfoThreadCreateEventStreamingSetup();
            unsubscribe();

            setIsConnected(false);
            setNoPermission(false);
            setIsConnecting(true);
            setIsReconnecting(false);

            isSubscribed = false;
        };
    }, [proxy.log, sreAgentEndpoint]);

    return (
        <StreamingContext.Provider
            value={{
                startMessageStreamingOnNewThread,
                startMessageStreamingOnExistingThread,
                cancelMessageStreaming,
                subscribeChatStreaming,
                subscribeThreadMenuEventStreaming,
                subscribeResourceInfoThreadCreateEventStreaming,
                isConnecting,
                isConnected,
                isReconnecting,
                noPermission,
            }}
        >
            {children}
        </StreamingContext.Provider>
    );
};
