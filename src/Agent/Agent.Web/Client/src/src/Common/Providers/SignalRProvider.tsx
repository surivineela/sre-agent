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
    const isConnectedRef = useRef(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { log } = useContext(AzPortalContext);

    const sendMessage = useCallback((method: string, ...args: any[]) => {
        if (isConnectedRef.current && connectionRef.current) {
            connectionRef.current.invoke(method, ...args).catch(() => {
                //error handling
            });
        }
    }, []);

    const onMessage = useCallback((method: string, callback: (...args: any[]) => void) => {
        if (connectionRef.current) {
            connectionRef.current.on(method, callback);
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
                log({
                    action: 'signalR',
                    actionModifier: 'connected',
                    data: 'Connected to the SignalR hub',
                    logLevel: 'verbose',
                });
                setIsConnected(true);
            } catch (e) {
                log({
                    action: 'signalR',
                    actionModifier: 'error',
                    data: `Failed to connect to the SignalR hub`,
                    logLevel: 'error',
                });
                setIsConnected(false);
            }
            setIsConnecting(false);
        };

        connect();

        return () => {
            connectionRef.current?.stop();
            setIsConnected(false);
        };
    }, [log, sreAgentEndpoint]);

    return <SignalRContext.Provider value={{ sendMessage, onMessage, isConnecting, isConnected }}>{children}</SignalRContext.Provider>;
};
