import { createContext } from 'react';

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

export const SreAgentContext = createContext<SreAgentContextProps>({
    grafana: {
        isGrafanaUpdating: false,
        deploymentId: '',
        notificationId: '',
        setNotificationId: () => {},
        setIsGrafanaUpdating: () => {},
        setDeploymentId: () => {},
    },
});
