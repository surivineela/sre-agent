import axios from 'axios';
import { createContext, ReactNode, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { KnowledgeGraphBuildStatus } from '../Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../Helpers/headers';

interface KnowledgeGraphBuildStatusContextProps {
    isKnowledgeGraphBuildCompleted: boolean;
}

export const KnowledgeGraphBuildStatusContext = createContext<KnowledgeGraphBuildStatusContextProps>({
    isKnowledgeGraphBuildCompleted: true,
});

export const KnowledgeGraphBuildStatusProvider = ({ children }: { children?: ReactNode }) => {
    const [isKnowledgeGraphBuildCompleted, setIsKnowledgeraphBuildCompleted] = useState(true);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { log } = useContext(AzPortalContext);

    const getProgress = async (): Promise<KnowledgeGraphBuildStatus | undefined> => {
        try {
            const response = await axios.get(`${sreAgentEndpoint}/api/v1/graph/progress`, {
                headers: getAgentHeaders(),
            });
            return response.data;
        } catch (error) {
            log({
                action: 'GetKnowledgeGraphBuildProgress',
                actionModifier: 'failed',
                data: error?.toString() || 'Failed to get knowledge graph build progress',
            });
            return undefined;
        }
    };

    useEffect(() => {
        let isSubscribed = true;

        let timeoutId: NodeJS.Timeout | null = null;

        const pollProgress = async () => {
            const progress = await getProgress();

            if (isSubscribed) {
                if (progress?.hasCompletedInitialGraphCrawl) {
                    setIsKnowledgeraphBuildCompleted(true);
                } else {
                    setIsKnowledgeraphBuildCompleted(false);
                    timeoutId = setTimeout(() => {
                        pollProgress();
                    }, 5000);
                }
            }
        };

        pollProgress();

        return () => {
            isSubscribed = false;

            if (timeoutId !== null) {
                clearTimeout(timeoutId);
            }
        };
    }, []);

    return (
        <KnowledgeGraphBuildStatusContext.Provider value={{ isKnowledgeGraphBuildCompleted }}>
            {children}
        </KnowledgeGraphBuildStatusContext.Provider>
    );
};
