import { useCallback, useContext } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { AgentTaskDevClient } from '../../Common/Clients/AgentTaskDevClient';
import { AgentTask } from '../../Common/Contracts/Azure/AgentTaskDevTypes';
import { Thread } from '../../Common/Contracts/DataPlane/Thread';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { InvestigationTreeContext } from '../Contexts/InvestigationTreeContext';

/**
 * Hook to handle investigation and agent task integration with thread selection
 * Isolated from useActivities for better separation of concerns
 */
export const useInvestigationIntegration = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const agentTaskDevClient = AgentTaskDevClient.getInstance(sreAgentEndpoint);

    // Check if agent task feature is enabled
    const showAgentTaskDev = useConfigSetting(SettingNames.ShowAgentTaskDev);

    // Get investigation tree context for clearing and hiding tree
    const investigationTreeContext = useContext(InvestigationTreeContext);
    const updateFromTaskUpdate = investigationTreeContext?.updateFromTaskUpdate || (() => {});
    const clearTree = investigationTreeContext?.clearTree || (() => {});
    const hideTree = investigationTreeContext?.hideTree || (() => {});

    /**
     * Load and display agent task data for a thread when streaming activity starts
     * This can be called when streaming messages arrive to refresh/update existing data
     */
    const loadInvestigationForThread = useCallback(
        async (thread: Thread) => {
            if (!showAgentTaskDev) {
                return;
            }

            // Support both agentTasks (frontend) and AgentTasks (backend)
            const agentTasks = (thread as any)?.agentTasks || (thread as any)?.AgentTasks || [];

            if (agentTasks.length > 0) {
                const lastAgentTask: AgentTask = agentTasks[agentTasks.length - 1];

                try {
                    console.log('🔄 Refreshing task data from API for streaming activity');
                    const agentTaskResponse = await agentTaskDevClient.getAgentTask(thread.id, lastAgentTask.id);
                    console.log('📡 API response:', agentTaskResponse);

                    if (agentTaskResponse.isSuccessful && agentTaskResponse.content) {
                        console.log('✅ Got fresh task data, updating tree');
                        // Update the tree with fresh data from streaming activity
                        updateFromTaskUpdate(agentTaskResponse.content);
                    } else {
                        console.warn('⚠️ Failed to get fresh task data:', agentTaskResponse.error);
                    }
                } catch (err) {
                    console.error('❌ Error refreshing task data:', err);
                }
            }
        },
        [agentTaskDevClient, showAgentTaskDev] // Only include stable dependencies
    );

    /**
     * Handle investigation updates when a thread is selected
     * This checks the database for existing agent task data and shows panel if data exists
     */
    const handleThreadInvestigationUpdate = useCallback(
        async (thread: Thread | null) => {
            console.log('🔧 handleThreadInvestigationUpdate called:', {
                threadId: thread?.id,
                hasAgentTasks: (thread as any)?.agentTasks?.length || (thread as any)?.AgentTasks?.length,
                showAgentTaskDev,
            });

            // Get fresh references inside the callback to avoid dependency issues
            const currentInvestigationContext = investigationTreeContext;
            const currentUpdateFromTaskUpdate = currentInvestigationContext?.updateFromTaskUpdate || (() => {});
            const currentClearTree = currentInvestigationContext?.clearTree || (() => {});
            const currentHideTree = currentInvestigationContext?.hideTree || (() => {});

            // If agent task feature is disabled or no thread, hide tree and return early
            if (!showAgentTaskDev || !thread || !thread.id) {
                console.log('🚫 DEBUG: Feature disabled, no thread, or no thread ID - hiding tree', {
                    showAgentTaskDev,
                    hasThread: !!thread,
                    threadId: thread?.id,
                    action: 'clearTree and hideTree',
                });
                currentClearTree();
                currentHideTree();
                return;
            }

            // Check if there's already an active investigation tree for this thread
            const hasActiveTree =
                currentInvestigationContext?.treeState?.rootNodes?.length > 0 &&
                currentInvestigationContext?.treeState?.isVisible &&
                currentInvestigationContext?.treeState?.currentTask?.taskId;

            if (hasActiveTree) {
                console.log('⚠️ DEBUG: Active investigation tree detected, skipping clear to avoid interrupting streaming');

                // Just verify the tree is still for the correct task
                const currentTaskId = currentInvestigationContext?.treeState?.currentTask?.taskId;
                const agentTasks = (thread as any)?.agentTasks || (thread as any)?.AgentTasks || [];

                if (agentTasks.length > 0) {
                    const lastAgentTask = agentTasks[agentTasks.length - 1];
                    const taskId = typeof lastAgentTask === 'string' ? lastAgentTask : lastAgentTask?.id;

                    if (currentTaskId === taskId) {
                        console.log('✅ DEBUG: Tree is for current task, keeping existing tree');
                        return; // Keep existing tree, don't interfere
                    }
                }
            }

            // Only clear the tree when switching threads or when no active investigation
            console.log('🧹 Clearing tree for thread switch');
            currentClearTree();

            // Only clear the tree when switching threads or when no active investigation
            console.log('🧹 DEBUG: Clearing tree for thread switch');
            currentClearTree();

            // CRITICAL: Always hide the tree immediately when switching threads to prevent race conditions
            console.log('🙈 DEBUG: Hiding tree for thread switch');
            currentHideTree();

            // Support both agentTasks (frontend) and AgentTasks (backend)
            const threadAgentTasks = (thread as any)?.agentTasks || (thread as any)?.AgentTasks || [];

            if (threadAgentTasks.length > 0) {
                console.log('📋 DEBUG: Found agent tasks, checking database', {
                    count: threadAgentTasks.length,
                    threadId: thread.id,
                });

                const lastAgentTask = threadAgentTasks[threadAgentTasks.length - 1];

                // Extract task ID - it could be a string ID or a full task object
                const taskId = typeof lastAgentTask === 'string' ? lastAgentTask : lastAgentTask?.id;

                if (!taskId) {
                    console.warn('⚠️ DEBUG: No valid task ID found in agent task:', lastAgentTask);
                    currentHideTree();
                    return;
                }

                try {
                    console.log('🔄 DEBUG: Fetching agent task details from database', {
                        threadId: thread.id,
                        taskId: taskId,
                        taskType: typeof lastAgentTask,
                    });
                    const agentTaskResponse = await agentTaskDevClient.getAgentTask(thread.id, taskId);
                    console.log('📡 API response:', agentTaskResponse);

                    if (agentTaskResponse.isSuccessful && agentTaskResponse.content) {
                        console.log('✅ DEBUG: Found existing agent task data, showing panel');
                        // Update the tree with fresh data from streaming activity
                        currentUpdateFromTaskUpdate(agentTaskResponse.content);
                    } else {
                        console.warn('⚠️ DEBUG: Failed to fetch agent task details:', {
                            error: agentTaskResponse.error,
                            status: agentTaskResponse.error?.response?.status,
                            statusText: agentTaskResponse.error?.response?.statusText,
                            url: agentTaskResponse.error?.config?.url,
                        });
                        currentHideTree(); // Hide if we can't fetch the data
                    }
                } catch (err) {
                    console.error('❌ DEBUG: Error fetching agent task details:', err);
                    currentHideTree(); // Hide if there's an error
                }
            } else {
                console.log('📭 DEBUG: No agent tasks found, hiding tree');
                // No agent tasks found, hide the tree
                currentHideTree();
            }

            // If agent task feature is disabled or no thread, return early
            if (!showAgentTaskDev || !thread) {
                return;
            }

            // Support both agentTasks (frontend) and AgentTasks (backend)
            const agentTasks = (thread as any)?.agentTasks || (thread as any)?.AgentTasks || [];

            if (agentTasks.length > 0) {
                const lastAgentTask: AgentTask = agentTasks[agentTasks.length - 1];

                try {
                    const agentTaskResponse = await agentTaskDevClient.getAgentTask(thread.id, lastAgentTask.id);

                    if (agentTaskResponse.isSuccessful && agentTaskResponse.content) {
                        // Show the panel because we have existing investigation data
                        currentUpdateFromTaskUpdate(agentTaskResponse.content);
                    } else {
                        currentHideTree(); // Hide if we can't fetch the data
                    }
                } catch (err) {
                    currentHideTree(); // Hide if there's an error
                }
            } else {
                // No agent tasks found, hide the tree
                currentHideTree();
            }
        },
        [agentTaskDevClient, showAgentTaskDev] // Only include stable dependencies
    );

    return {
        handleThreadInvestigationUpdate,
        loadInvestigationForThread,
        showAgentTaskDev,
        // Expose tree controls for external use if needed
        clearInvestigationTree: clearTree,
        hideInvestigationTree: hideTree,
    };
};
