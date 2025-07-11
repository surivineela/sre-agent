import { useMemo } from "react";
import { AgentMode, ThreadSource } from "../../Common/Contracts/Azure/SreAgent";
import { useThreadDataCache } from "./useThreadDataCache";

export const useThreadAgentMode = (threadId: string | null | undefined, threadSource: string | null | undefined) => {
    const { thread, isLoadingThread, isFetchingThread, fetchThreadError, invalidateThreadDataCache } = useThreadDataCache(threadId);

    const threadAgentMode = useMemo(() => {
        return thread?.agentMode?.toLowerCase() || AgentMode.review;
    }, [thread]);

    const threadAgentModeToDisplay = useMemo(() => {
        if (threadSource === ThreadSource.incident) {
            return threadAgentMode;
        }
        return undefined;
    }, [threadAgentMode, threadSource]);

    return {
        threadAgentMode,
        threadAgentModeToDisplay,
        isLoadingThreadAgentMode: isLoadingThread,
        isFetchingThreadAgentMode: isFetchingThread,
        fetchThreadAgentModeError: fetchThreadError,
        invalidateThreadAgentModeDataCache: invalidateThreadDataCache,
    };
};
