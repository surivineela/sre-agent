import { useMemo } from 'react';
import { ScheduledTask, ScheduledTaskStatus } from '../Contracts/ScheduledTasks';

export interface ScheduledTaskMessageData {
    isScheduledTaskMessage: boolean;
    isScheduledTaskCreationMessage: boolean;
    task?: ScheduledTask;
    executionTime?: Date;
    originalMessage: string;
}

/**
 * Hook to detect and parse scheduled task execution messages
 */
export const useScheduledTaskMessage = (messageContent: string): ScheduledTaskMessageData => {
    return useMemo(() => {
        // Check if this is a scheduled task creation message
        const scheduledTaskCreationMatch = messageContent.match(/\[SCHEDULED_TASK_CREATED\](.*?)\[\/SCHEDULED_TASK_CREATED\]/s);

        if (scheduledTaskCreationMatch) {
            try {
                const taskData = JSON.parse(scheduledTaskCreationMatch[1]);

                const task: ScheduledTask = {
                    id: taskData.taskId,
                    name: taskData.taskName,
                    description: taskData.description,
                    status: taskData.status as ScheduledTaskStatus,
                    cronExpression: taskData.cronExpression,
                    agentPrompt: taskData.agentPrompt,
                    createdBy: taskData.createdBy || 'system',
                    createdAt: taskData.createdAt || new Date().toISOString(),
                    executionCount: taskData.executionCount || 0,
                };

                return {
                    isScheduledTaskMessage: false,
                    isScheduledTaskCreationMessage: true,
                    task,
                    originalMessage: messageContent,
                };
            } catch (error) {
                console.warn('Failed to parse scheduled task creation JSON:', error);
            }
        }

        // Check if this is a scheduled task execution message by looking for our special markers
        const scheduledTaskMatch = messageContent.match(/\[SCHEDULED_TASK_EXECUTION\](.*?)\[\/SCHEDULED_TASK_EXECUTION\]/s);

        if (scheduledTaskMatch) {
            try {
                const taskData = JSON.parse(scheduledTaskMatch[1]);

                const task: ScheduledTask = {
                    id: taskData.taskId,
                    name: taskData.taskName,
                    description: taskData.description,
                    status: taskData.status as ScheduledTaskStatus,
                    cronExpression: taskData.cronExpression,
                    agentPrompt: taskData.agentPrompt,
                    createdBy: 'system',
                    createdAt: new Date().toISOString(),
                    executionCount: 0,
                };

                const executionTime = taskData.executionTime ? new Date(taskData.executionTime) : new Date();

                return {
                    isScheduledTaskMessage: true,
                    isScheduledTaskCreationMessage: false,
                    task,
                    executionTime,
                    originalMessage: messageContent,
                };
            } catch (error) {
                console.warn('Failed to parse scheduled task JSON:', error);
            }
        }

        return {
            isScheduledTaskMessage: false,
            isScheduledTaskCreationMessage: false,
            originalMessage: messageContent,
        };
    }, [messageContent]);
};
