import { createContext } from 'react';
import { AgentContextProps } from './Activities';

type SreAgentContextProps = {
    grafana: {
        isGrafanaUpdating: boolean;
        deploymentId: string;
        notificationId: string;
        setNotificationId: React.Dispatch<React.SetStateAction<string>>;
        setIsGrafanaUpdating: React.Dispatch<React.SetStateAction<boolean>>;
        setDeploymentId: React.Dispatch<React.SetStateAction<string>>;
    };
};

type WebSocketContextProps = {
    sendMessage: (message: string) => void;
    addMessageListener: (handler: (e: MessageEvent<any>) => void) => void;
};

export const SreAgentContext = createContext<SreAgentContextProps>({
    grafana: {
        isGrafanaUpdating: false,
        deploymentId: '',
        notificationId: '',
        setNotificationId: () => { },
        setIsGrafanaUpdating: () => { },
        setDeploymentId: () => { },
    },
});

export const AgentContext = createContext<AgentContextProps>({
    threadContentAndActionKey: '',
    activeThreadId: '',
});

export const WebSocketContext = createContext<WebSocketContextProps>({
    sendMessage: () => {},
    addMessageListener: () => {},
});
