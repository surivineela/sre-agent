import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import { StreamingMessage } from '../../Common/Contracts/DataPlane/Streaming';
import { SubagentToolInvocation, TaskToolExecution, TaskToolExecutionGroup } from '../../Common/Contracts/DataPlane/TaskToolExecution';
import { StreamingContext } from '../Contracts/Context';

type TaskToolExecutionStreamData = {
    executionId: string;
    subAgentType: string;
    description: string;
    prompt?: string;
    status: string;
    startedAt: string;
    completedAt?: string;
    error?: string;
};

type TaskToolGroupStreamData = {
    groupId: string;
    startedAt: string;
    completedAt?: string;
    isComplete?: boolean;
    executions: TaskToolExecutionStreamData[];
};

type TaskToolInvocationStreamData = {
    executionId: string;
    toolName: string;
    description?: string;
    status: string;
    startedAt?: string;
    completedAt?: string;
};

const toTaskToolExecution = (data: TaskToolExecutionStreamData): TaskToolExecution => ({
    id: data.executionId,
    subagentType: data.subAgentType as TaskToolExecution['subagentType'],
    description: data.description,
    prompt: data.prompt ?? '',
    status: data.status as TaskToolExecution['status'],
    startedAt: data.startedAt,
    completedAt: data.completedAt,
    error: data.error,
});

/**
 * Hook for subscribing to Task tool (subagent) execution updates via SignalR.
 */
export const useTaskToolExecutions = (threadId: string | undefined | null) => {
    const [executionGroups, setExecutionGroups] = useState<Map<string, TaskToolExecutionGroup>>(new Map());
    const [standaloneExecutions, setStandaloneExecutions] = useState<Map<string, TaskToolExecution>>(new Map());
    const threadIdRef = useRef<string | null>(threadId || null);
    const { subscribeSubagentUpdateEvent } = useContext(StreamingContext);

    threadIdRef.current = threadId || null;

    // Helper to update an execution within groups
    const updateExecutionInGroups = useCallback((execData: TaskToolExecutionStreamData, checkCompletion: boolean) => {
        setExecutionGroups(prev => {
            for (const [groupId, group] of prev) {
                const execIndex = group.executions.findIndex(e => e.id === execData.executionId);
                if (execIndex >= 0) {
                    const newExecutions = [...group.executions];
                    newExecutions[execIndex] = toTaskToolExecution(execData);

                    const isComplete = checkCompletion && newExecutions.every(
                        e => e.status === 'Completed' || e.status === 'Failed' || e.status === 'Cancelled'
                    );

                    const newMap = new Map(prev);
                    newMap.set(groupId, {
                        ...group,
                        executions: newExecutions,
                        isComplete,
                        completedAt: isComplete ? new Date().toISOString() : group.completedAt,
                    });
                    return newMap;
                }
            }
            return prev;
        });

        setStandaloneExecutions(prev => {
            const newMap = new Map(prev);
            newMap.set(execData.executionId, toTaskToolExecution(execData));
            return newMap;
        });
    }, []);

    // Helper to update tool invocations within an execution
    const updateToolInvocation = useCallback((invData: TaskToolInvocationStreamData, isEnd: boolean) => {
        const updateExecution = (exec: TaskToolExecution): TaskToolExecution => {
            const invocations = exec.toolInvocations ? [...exec.toolInvocations] : [];
            const existingIdx = invocations.findIndex(inv => inv.toolName === invData.toolName && inv.status === 'Running');

            if (isEnd && existingIdx >= 0) {
                // Update existing invocation
                invocations[existingIdx] = {
                    ...invocations[existingIdx],
                    status: invData.status as SubagentToolInvocation['status'],
                    completedAt: invData.completedAt,
                };
            } else if (!isEnd) {
                // Add new invocation
                invocations.push({
                    toolName: invData.toolName,
                    description: invData.description,
                    status: 'Running',
                    startedAt: invData.startedAt || new Date().toISOString(),
                });
            }

            // Keep only the last 5 invocations to avoid memory bloat
            return { ...exec, toolInvocations: invocations.slice(-5) };
        };

        setExecutionGroups(prev => {
            let updated = false;
            const newMap = new Map(prev);

            for (const [groupId, group] of prev) {
                const execIndex = group.executions.findIndex(e => e.id === invData.executionId);
                if (execIndex >= 0) {
                    const newExecutions = [...group.executions];
                    newExecutions[execIndex] = updateExecution(newExecutions[execIndex]);
                    newMap.set(groupId, { ...group, executions: newExecutions });
                    updated = true;
                    break;
                }
            }

            return updated ? newMap : prev;
        });

        setStandaloneExecutions(prev => {
            const exec = prev.get(invData.executionId);
            if (exec) {
                const newMap = new Map(prev);
                newMap.set(invData.executionId, updateExecution(exec));
                return newMap;
            }
            return prev;
        });
    }, []);

    const handleSubagentUpdate = useCallback((message: StreamingMessage) => {
        const messageThreadId = message?.additionalProperties?.threadId;
        const streamMessageType = message?.additionalProperties?.streamMessageType;
        const content = message?.contents?.[0]?.text;

        if (!messageThreadId || messageThreadId !== threadIdRef.current || !content) {
            return;
        }

        try {
            if (streamMessageType === 'TaskToolGroupStart' || streamMessageType === 'TaskToolGroupEnd') {
                const groupData = JSON.parse(content) as TaskToolGroupStreamData;
                setExecutionGroups(prev => {
                    const newMap = new Map(prev);
                    newMap.set(groupData.groupId, {
                        id: groupData.groupId,
                        executions: groupData.executions.map(toTaskToolExecution),
                        isComplete: streamMessageType === 'TaskToolGroupEnd' ? (groupData.isComplete ?? true) : false,
                        startedAt: groupData.startedAt,
                        completedAt: groupData.completedAt,
                    });
                    return newMap;
                });
            } else if (streamMessageType === 'TaskToolExecutionStart' || streamMessageType === 'TaskToolExecutionEnd') {
                const execData = JSON.parse(content) as TaskToolExecutionStreamData;
                updateExecutionInGroups(execData, streamMessageType === 'TaskToolExecutionEnd');
            } else if (streamMessageType === 'TaskToolInvocationStart' || streamMessageType === 'TaskToolInvocationEnd') {
                const invData = JSON.parse(content) as TaskToolInvocationStreamData;
                updateToolInvocation(invData, streamMessageType === 'TaskToolInvocationEnd');
            }
        } catch {
            // Silent fail - streaming errors shouldn't break the UI
        }
    }, [updateExecutionInGroups, updateToolInvocation]);

    useEffect(() => {
        let isSubscribed = true;
        const unsubscribe = subscribeSubagentUpdateEvent((message: StreamingMessage) => {
            if (isSubscribed) handleSubagentUpdate(message);
        });
        return () => {
            unsubscribe();
            isSubscribed = false;
        };
    }, [subscribeSubagentUpdateEvent, handleSubagentUpdate]);

    useEffect(() => {
        setExecutionGroups(new Map());
        setStandaloneExecutions(new Map());
    }, [threadId]);

    return {
        executionGroups: Array.from(executionGroups.values()),
        standaloneExecutions: Array.from(standaloneExecutions.values()),
        getExecutionById: useCallback((id: string) => standaloneExecutions.get(id), [standaloneExecutions]),
        getGroupById: useCallback((id: string) => executionGroups.get(id), [executionGroups]),
    };
};
