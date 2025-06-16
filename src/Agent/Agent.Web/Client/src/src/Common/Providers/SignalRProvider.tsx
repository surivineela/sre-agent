import * as signalR from '@microsoft/signalr';
import { ReactNode, useCallback, useContext, useEffect, useRef } from 'react';
import { SignalRContext } from '../../Space/Contracts/Context';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';

export const SignalRProvider = ({ children }: { children?: ReactNode }) => {
    const connectionRef = useRef<signalR.HubConnection | null>(null);
    const isConnectedRef = useRef(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

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
        const connect = async () => {
            const isReactLocalhost = window.location.hostname.toLowerCase() === 'localhost' && window.location.port === '5173';
            const endpoint = isReactLocalhost ? 'https://localhost:7023' : sreAgentEndpoint;

            connectionRef.current = new signalR.HubConnectionBuilder()
                // ToDo: Sanitize the endpoint
                .withUrl(`${endpoint}/agentHub`, {
                    accessTokenFactory: () => AzPortalProxy.envInfo.sreAgentToken || '',
                })
                .withAutomaticReconnect()
                .build();

            connectionRef.current.onclose(() => (isConnectedRef.current = false));
            connectionRef.current.onreconnected(() => (isConnectedRef.current = true));

            try {
                await connectionRef.current.start();
                console.log(`Connected to the SignalR hub`);
                isConnectedRef.current = true;
            } catch (e) {
                console.error(`Failed to connect to the SignalR hub`);
            }
        };

        connect();

        return () => {
            connectionRef.current?.stop();
            isConnectedRef.current = false;
        };
    }, [sreAgentEndpoint]);

    return <SignalRContext.Provider value={{ sendMessage, onMessage }}>{children}</SignalRContext.Provider>;
};
