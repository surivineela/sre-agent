import { ReactNode, useCallback, useContext, useEffect, useRef } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { WebSocketContext } from '../../Space/Contracts/Context';
import { defaultSreAgentEndpoint } from '../AzPortalProxy/AzPortalProxy';

export const WebSocketProvider = ({ children }: { children?: ReactNode }) => {
    const websocket = useRef<WebSocket | null>(null);
    const listener = useRef<(e: MessageEvent<any>) => void>(() => { });

    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const sendMessage = useCallback((message: string) => {
        websocket.current?.send(message);
    }, []);

    const addMessageListener = useCallback((handler: (e: MessageEvent<any>) => void) => {
        listener.current = handler;
    }, []);

    useEffect(() => {
        // ToDo(liuqi): sanitize input endpoint
        const endpoint = sreAgentEndpoint === defaultSreAgentEndpoint ? 'localhost:7024' : new URL(sreAgentEndpoint).host;
        websocket.current = new WebSocket(`ws://${endpoint}/ws`);

        websocket.current.onopen = () => {
            console.log('WebSocket connection is open');
        };

        websocket.current.onerror = error => {
            console.error('WebSocket error:', error);
        };

        websocket.current.onclose = () => {
            console.log('WebSocket connection closed');
        };

        websocket.current.onmessage = e => {
            listener.current?.(e);
        };

        return () => {
            websocket.current?.close();
        };
    }, [sreAgentEndpoint]);

    return <WebSocketContext.Provider value={{ sendMessage, addMessageListener }}>{children}</WebSocketContext.Provider>;
};
