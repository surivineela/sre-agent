import * as signalR from '@microsoft/signalr';
import { ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { StreamingContext } from '../../Space/Contracts/Context';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { MessageRequestType, MessageResponseType, StreamingMessage } from '../Contracts/Azure/Streaming';

export const StreamingProvider = ({ children }: { children?: ReactNode }) => {
    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const [isConnecting, setIsConnecting] = useState(true);
    const [isConnected, setIsConnected] = useState(false);
    const [isReconnecting, setIsReconnecting] = useState(false);
    const [noPermission, setNoPermission] = useState(false);
    const isConnectedRef = useRef(false);
    // key: threadId, value: the latest streaming messages for that thread in the current session
    const latestStreamingMessageRef = useRef<Map<string, StreamingMessage | null | undefined>>(new Map());
    // key: method name, value: map of threadId to handler function
    const handlersRef = useRef<Map<MessageResponseType, Map<string, (...args: any[]) => void>>>(new Map());

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const sendMessage = useCallback((method: MessageRequestType, ...args: any[]) => {
        if (isConnectedRef.current && connectionRef.current) {
            connectionRef.current.invoke(method, ...args).catch(() => {
                //error handling
            });
        }
    }, []);

    const addHandler = (method: MessageResponseType, threadId: string, handler: (...args: any[]) => void) => {
        const threadHandlers = handlersRef.current.get(method);

        if (threadHandlers) {
            threadHandlers.set(threadId, handler);
        } else {
            handlersRef.current.set(method, new Map([[threadId, handler]]));
        }
    };

    const removeHandler = (method: MessageResponseType, threadId: string) => {
        const threadHandlers = handlersRef.current.get(method);
        if (threadHandlers) {
            threadHandlers.delete(threadId);
            if (threadHandlers.size === 0) {
                handlersRef.current.delete(method);
            }
        }
    };

    const subscribeStreaming = (methodName: MessageResponseType, threadId: string, handler: (...args: any[]) => void) => {
        addHandler(methodName, threadId, handler);
        return () => {
            removeHandler(methodName, threadId);
        };
    };

    const subscribeChatStreaming = useCallback(
        (
            threadId: string,
            latestStreamingMessageHandler: (latestStreamingMessage: StreamingMessage | null | undefined) => void,
            messageUpdateHandler: (...args: any[]) => void,
            threadUpdateHandler: (...args: any[]) => void
        ) => {
            latestStreamingMessageHandler(latestStreamingMessageRef.current.get(threadId));

            const removeMessageUpdateHandler = subscribeStreaming(MessageResponseType.MessageUpdate, threadId, messageUpdateHandler);
            const removeThreadUpdateHandler = subscribeStreaming(MessageResponseType.ThreadUpdate, threadId, threadUpdateHandler);

            return () => {
                removeMessageUpdateHandler();
                removeThreadUpdateHandler();
            };
        },
        []
    );

    const onChatMessage = () => {
        const storeLatestMessage = (threadId: string, message: StreamingMessage) => {
            latestStreamingMessageRef.current.set(threadId, message);
        };

        const onReceiveMessage = (methodName: MessageResponseType) => {
            connectionRef.current?.on(methodName, (message: StreamingMessage) => {
                //Keep it for now for testing purpose. Will remove it once the streaming is not behind the feature flag
                console.log(message);
                const threadId = message?.additionalProperties?.threadId;
                if (threadId) {
                    storeLatestMessage(threadId, message);
                    handlersRef.current.get(methodName)?.get(threadId)?.(message);
                }
            });
        };
        onReceiveMessage(MessageResponseType.ThreadUpdate);
        onReceiveMessage(MessageResponseType.MessageUpdate);
    };

    const offChatMessage = () => {
        connectionRef.current?.off(MessageResponseType.ThreadUpdate);
        connectionRef.current?.off(MessageResponseType.MessageUpdate);
    };

    useEffect(() => {
        isConnectedRef.current = isConnected;
    }, [isConnected]);

    useEffect(() => {
        let isSubscribed = true;

        const connect = async () => {
            setIsConnecting(true);
            const isReactLocalhost = window.location.hostname.toLowerCase() === 'localhost' && window.location.port === '5173';
            const endpoint = isReactLocalhost ? 'https://localhost:7023' : sreAgentEndpoint;

            connectionRef.current = new signalR.HubConnectionBuilder()
                // ToDo: Sanitize the endpoint
                .withUrl(`${endpoint}/agentHub`, {
                    accessTokenFactory: () => AzPortalProxy.envInfo.sreAgentToken || '',
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
            });

            connectionRef.current.onreconnected(() => {
                setIsReconnecting(false);
            });

            try {
                await connectionRef.current.start();

                if (isSubscribed) {
                    offChatMessage();
                    onChatMessage();
                    setIsConnected(true);
                    setNoPermission(false);
                }
            } catch (e) {
                if (isSubscribed) {
                    const isPermissionError =
                        (e instanceof signalR.HttpError && (e.statusCode === 403 || e.statusCode === 401)) ||
                        (e instanceof Error && (e.message.includes('403') || e.message.includes('401')));

                    if (isPermissionError) {
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
            latestStreamingMessageRef.current = new Map();
            handlersRef.current = new Map();
            offChatMessage();
            setIsConnected(false);
            setNoPermission(false);
            setIsConnecting(true);
            setIsReconnecting(false);
            isSubscribed = false;
        };
    }, [proxy.log, sreAgentEndpoint]);

    return (
        <StreamingContext.Provider value={{ sendMessage, subscribeChatStreaming, isConnecting, isConnected, isReconnecting, noPermission }}>
            {children}
        </StreamingContext.Provider>
    );
};
