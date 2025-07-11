import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useCallback, useContext } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ThreadClient } from '../../Common/Clients/ThreadClient';

const getThreadDataCachePrefix = 'getThreadAgentMode';

export const useThreadDataCache = (threadId?: string | null) => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const threadClient = ThreadClient.getInstance(sreAgentEndpoint);

    const queryClient = useQueryClient();

    const {
        data: thread,
        isLoading: isLoadingThread,
        isFetching: isFetchingThread,
        error: fetchThreadError,
    } = useQuery({
        queryKey: [getThreadDataCachePrefix, threadId],
        enabled: !!threadId,
        queryFn: async () => {
            const response = await threadClient.getThread(threadId!);
            if (response.isSuccessful && response.content) {
                return response.content;
            } else {
                throw new Error(response.error?.message || 'Failed to fetch thread');
            }
        },
        refetchOnWindowFocus: false,
        refetchOnReconnect: false,
        staleTime: Infinity,
        gcTime: Infinity,
    });

    const invalidateThreadDataCache = useCallback(() => {
        queryClient.invalidateQueries({
            queryKey: [getThreadDataCachePrefix, threadId],
        });
    }, [threadId]);

    return {
        thread,
        isLoadingThread,
        isFetchingThread,
        fetchThreadError,
        invalidateThreadDataCache,
    };
};
