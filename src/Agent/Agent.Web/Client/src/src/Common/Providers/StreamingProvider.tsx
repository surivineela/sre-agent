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
    const isConnectedRef = useRef(false);
    // key: threadId, value: array of streaming messages of this thread in current session
    const streamingMessagesRef = useRef<Map<string, StreamingMessage[]>>(new Map());
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
            existingStreamingMessageHandler: (streamingMessages: StreamingMessage[] | null | undefined) => void,
            messageUpdateHandler: (...args: any[]) => void,
            threadUpdateHandler: (...args: any[]) => void
        ) => {
            existingStreamingMessageHandler(streamingMessagesRef.current.get(threadId));

            const removeMessageUpdateHandler = subscribeStreaming(MessageResponseType.MessageUpdate, threadId, messageUpdateHandler);
            const removeThreadUpdateHandler = subscribeStreaming(MessageResponseType.ThreadUpdate, threadId, threadUpdateHandler);

            return () => {
                removeMessageUpdateHandler();
                removeThreadUpdateHandler();
            };
        },
        []
    );

    const deleteStreamingMessages = useCallback((threadId: string) => {
        streamingMessagesRef.current.delete(threadId);
    }, []);

    const onChatMessage = () => {
        const storeMessage = (threadId: string, message: StreamingMessage) => {
            const messages = streamingMessagesRef.current.get(threadId);

            if (messages) {
                messages.push(message);
            } else {
                streamingMessagesRef.current.set(threadId, [message]);
            }
        };

        const onReceiveMessage = (methodName: MessageResponseType) => {
            connectionRef.current?.on(methodName, (message: StreamingMessage) => {
                //Keep it for now for testing purpose. Will remove it once the streaming is not behind the feature flag
                console.log(message);
                const threadId = message?.additionalProperties?.threadId;
                if (threadId) {
                    storeMessage(threadId, message);
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

            connectionRef.current.onclose(() => setIsConnected(false));
            connectionRef.current.onreconnected(() => setIsConnected(true));

            try {
                await connectionRef.current.start();
                offChatMessage();
                onChatMessage();
                setIsConnected(true);
            } catch (e) {
                setIsConnected(false);
            }
            setIsConnecting(false);
        };

        connect();

        return () => {
            connectionRef.current?.stop();
            streamingMessagesRef.current = new Map();
            handlersRef.current = new Map();
            offChatMessage();
            setIsConnected(false);
        };
    }, [proxy.log, sreAgentEndpoint]);

    return (
        <StreamingContext.Provider value={{ sendMessage, subscribeChatStreaming, deleteStreamingMessages, isConnecting, isConnected }}>
            {children}
        </StreamingContext.Provider>
    );
};
