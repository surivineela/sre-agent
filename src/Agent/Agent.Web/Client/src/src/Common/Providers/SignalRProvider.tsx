import * as signalR from '@microsoft/signalr';
import { ReactNode, useCallback, useContext, useEffect, useRef } from 'react';
import { SignalRContext } from '../../Space/Contracts/Context';
import AzPortalProxy, { defaultSreAgentEndpoint } from '../AzPortalProxy/AzPortalProxy';
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
            const endpoint = sreAgentEndpoint === defaultSreAgentEndpoint ? 'https://localhost:7023' : sreAgentEndpoint;
            const connection = new signalR.HubConnectionBuilder()
                // ToDo: Sanitize the endpoint
                .withUrl(`${endpoint}/agentHub`, {
                    accessTokenFactory: () => AzPortalProxy.envInfo.sreAgentToken || '',
                })
                .withAutomaticReconnect()
                .build();

            connection.onclose(() => (isConnectedRef.current = false));
            connection.onreconnected(() => (isConnectedRef.current = true));

            try {
                await connection.start();
                console.log(`Connected to the SignalR hub at ${endpoint}`);
                isConnectedRef.current = true;
                connectionRef.current = connection;
            } catch (e) {
                console.error(`Failed to connect to the SignalR hub at ${endpoint}`);
            }
        };

        connect();

        return () => {
            connectionRef.current?.stop();
        };
    }, [sreAgentEndpoint]);

    return <SignalRContext.Provider value={{ sendMessage, onMessage }}>{children}</SignalRContext.Provider>;
};
