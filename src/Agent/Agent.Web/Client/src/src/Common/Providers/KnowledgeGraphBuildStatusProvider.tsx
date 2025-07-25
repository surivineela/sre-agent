import axios from 'axios';
import { createContext, ReactNode, useCallback, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { KnowledgeGraphBuildStatus } from '../Contracts/Azure/SreAgent';
import { getAgentHeaders } from '../Helpers/headers';

interface KnowledgeGraphBuildStatusContextProps {
    isKnowledgeGraphBuildCompleted: boolean;
    hasChatPermissions: boolean;
    progressPercent: number;
}

export const KnowledgeGraphBuildStatusContext = createContext<KnowledgeGraphBuildStatusContextProps>({
    isKnowledgeGraphBuildCompleted: true,
    hasChatPermissions: true,
    progressPercent: 100,
});

export const KnowledgeGraphBuildStatusProvider = ({ children }: { children?: ReactNode }) => {
    const [isKnowledgeGraphBuildCompleted, setIsKnowledgeGraphBuildCompleted] = useState(true);
    const [hasChatPermissions, setHasChatPermissions] = useState(true);
    const [progressPercent, setProgressPercent] = useState(100);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const proxy = useContext(AzPortalContext);

    const getProgress = useCallback(async (): Promise<{ data?: KnowledgeGraphBuildStatus; permissionsError: boolean }> => {
        const maxRetries = 5;
        let consecutivePermissionErrors = 0;

        for (let attempt = 0; attempt < maxRetries; attempt++) {
            const requestUrl = `${sreAgentEndpoint}/api/v1/graph/progress`;
            try {
                const response = await axios.get(requestUrl, {
                    headers: getAgentHeaders(),
                });

                return { data: response.data, permissionsError: false };
            } catch (error: any) {
                const permissionsError = error.response?.status === 403;
                const errorMessage = permissionsError
                    ? 'Permissions error while fetching knowledge graph build progress'
                    : 'Error fetching knowledge graph build progress';

                proxy.log({
                    action: 'GetKnowledgeGraphBuildProgress',
                    actionModifier: 'failed',
                    data: {
                        errorMessage: errorMessage,
                        permissionsError: permissionsError,
                        requestUrl: requestUrl,
                        statusCode: error.response?.status,
                    },
                });

                if (permissionsError) {
                    consecutivePermissionErrors++;

                    if (consecutivePermissionErrors >= maxRetries) {
                        setHasChatPermissions(false);
                        setIsKnowledgeGraphBuildCompleted(true);
                        setProgressPercent(100);
                        return { data: undefined, permissionsError: true };
                    }
                } else {
                    return { data: undefined, permissionsError: false };
                }
            }
        }

        return { data: undefined, permissionsError: true };
    }, [sreAgentEndpoint, proxy]);

    useEffect(() => {
        let isSubscribed = true;
        let timeoutId: NodeJS.Timeout | null = null;

        const pollProgress = async () => {
            if (!hasChatPermissions) {
                return;
            }

            const result = await getProgress();

            if (isSubscribed && !result.permissionsError) {
                const isCompleted = result.data?.hasCompletedInitialGraphCrawl ?? false;
                setIsKnowledgeGraphBuildCompleted(isCompleted);

                // Calculate progress percent
                let percent = 100;
                if (
                    result.data &&
                    typeof result.data.crawledCount === 'number' &&
                    typeof result.data.totalVisibleResources === 'number' &&
                    result.data.totalVisibleResources > 0
                ) {
                    percent = Math.min(100, Math.floor((result.data.crawledCount / result.data.totalVisibleResources) * 100));
                }
                setProgressPercent(percent);

                const interval = isCompleted ? 20000 : 5000;

                timeoutId = setTimeout(() => {
                    pollProgress();
                }, interval);
            }
        };

        pollProgress();

        return () => {
            isSubscribed = false;

            if (timeoutId !== null) {
                clearTimeout(timeoutId);
            }
        };
    }, [getProgress, hasChatPermissions]);

    return (
        <KnowledgeGraphBuildStatusContext.Provider value={{ isKnowledgeGraphBuildCompleted, hasChatPermissions, progressPercent }}>
            {children}
        </KnowledgeGraphBuildStatusContext.Provider>
    );
};
