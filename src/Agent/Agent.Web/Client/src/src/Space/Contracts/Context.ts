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
    incidentManagement: {
        isIncidentManagementConnected: boolean;
        setIsIncidentManagementConnected: React.Dispatch<React.SetStateAction<boolean>>;
        hasFilters: boolean;
        setHasFilters: React.Dispatch<React.SetStateAction<boolean>>;
    };
    agent: {
        mode: string;
        setMode: React.Dispatch<React.SetStateAction<string>>;
    };
};

type SignalRContextProps = {
    sendMessage: (message: string, ...args: any[]) => void;
    onMessage: (method: string, callback: (...args: any[]) => void) => void;
    isConnecting: boolean;
    isConnected: boolean;
};

export const SreAgentContext = createContext<SreAgentContextProps>({
    grafana: {
        isGrafanaUpdating: false,
        deploymentId: '',
        notificationId: '',
        setNotificationId: () => {},
        setIsGrafanaUpdating: () => {},
        setDeploymentId: () => {},
    },
    incidentManagement: {
        isIncidentManagementConnected: false,
        setIsIncidentManagementConnected: () => {},
        hasFilters: false,
        setHasFilters: () => {},
    },
    agent: {
        mode: '',
        setMode: () => {},
    },
});

export const AgentContext = createContext<AgentContextProps>({
    threadContentAndActionKey: '',
    activeThreadId: '',
});

export const SignalRContext = createContext<SignalRContextProps>({
    sendMessage: (_message: string, ..._args: any[]) => {},
    onMessage: (_method: string, _callback: (...args: any[]) => void) => {},
    isConnecting: true,
    isConnected: false,
});
