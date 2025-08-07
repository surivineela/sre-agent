import { useCallback, useState } from 'react';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';

export const useAgentTaskDevActivities = () => {
    const showAgentTaskDev = useConfigSetting(SettingNames.ShowAgentTaskDev);
    const [threadPollingTriggerId, setThreadPollingTriggerId] = useState<number>(0);

    const pollNewThreadsImmediately = useCallback(() => {
        if (!showAgentTaskDev) return;
        setThreadPollingTriggerId(prev => prev + 1);
    }, [showAgentTaskDev]);

    const promoteThread = useCallback(
        (_threadId: string) => {
            if (!showAgentTaskDev) return;
            // poll thread immediately to make the recently updated thread on top.
            pollNewThreadsImmediately();
        },
        [showAgentTaskDev, pollNewThreadsImmediately]
    );

    // Enhanced addThread function that includes polling
    const enhancedAddThreadWrapper = useCallback(
        (originalAddThread: (threadId: string, newThreadToSelect?: any) => void) => {
            return (threadId: string, newThreadToSelect?: any) => {
                if (showAgentTaskDev) {
                    // poll thread immediately to get the thread just added.
                    pollNewThreadsImmediately();
                }
                originalAddThread(threadId, newThreadToSelect);
            };
        },
        [showAgentTaskDev, pollNewThreadsImmediately]
    );

    return {
        isEnabled: showAgentTaskDev,
        threadPollingTriggerId,
        pollNewThreadsImmediately,
        promoteThread,
        createEnhancedAddThread: enhancedAddThreadWrapper,
    };
};
