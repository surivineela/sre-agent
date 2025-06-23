import * as signalR from '@microsoft/signalr';
import { ReactNode, useCallback, useContext, useEffect, useRef, useState } from 'react';
import { SignalRContext } from '../../Space/Contracts/Context';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';

export const SignalRProvider = ({ children }: { children?: ReactNode }) => {
    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const [isConnecting, setIsConnecting] = useState(true);
    const [isConnected, setIsConnected] = useState(false);
    const subscribers = useRef<Map<string, Set<(...args: any[]) => void>>>(new Map());
    const isConnectedRef = useRef(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const sendMessage = useCallback((method: string, ...args: any[]) => {
        if (isConnectedRef.current && connectionRef.current) {
            connectionRef.current.invoke(method, ...args).catch(() => {
                //error handling
            });
        }
    }, []);

    const subscribeSignalR = useCallback((method: string, callback: (...args: any[]) => void) => {
        if (!subscribers.current.has(method)) {
            subscribers.current.set(method, new Set());
            connectionRef.current?.off(method); // Ensure no duplicate handlers
            connectionRef.current?.on(method, (...args: any[]) => {
                const callbacks = subscribers.current.get(method);
                if (callbacks) {
                    callbacks.forEach(cb => cb(...args));
                }
            });
        }

        subscribers.current.get(method)?.add(callback);
    }, []);

    const unsubscribeSignalR = useCallback((method: string, callback: (...args: any[]) => void) => {
        const callbacks = subscribers.current.get(method);
        if (callbacks) {
            callbacks.delete(callback);
            if (callbacks.size === 0) {
                subscribers.current.delete(method);
                connectionRef.current?.off(method);
            }
        }
    }, []);

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
                setIsConnected(true);
                proxy.log({
                    action: 'signalR',
                    actionModifier: 'connected',
                    data: 'Connected to the SignalR hub',
                    logLevel: 'verbose',
                });
            } catch (e) {
                setIsConnected(false);
                proxy.log({
                    action: 'signalR',
                    actionModifier: 'error',
                    data: `Failed to connect to the SignalR hub`,
                    logLevel: 'error',
                });
            }
            setIsConnecting(false);
        };

        connect();

        return () => {
            connectionRef.current?.stop();
            subscribers.current = new Map();
            setIsConnected(false);
        };
    }, [proxy.log, sreAgentEndpoint]);

    return (
        <SignalRContext.Provider value={{ sendMessage, subscribeSignalR, unsubscribeSignalR, isConnecting, isConnected }}>
            {children}
        </SignalRContext.Provider>
    );
};
