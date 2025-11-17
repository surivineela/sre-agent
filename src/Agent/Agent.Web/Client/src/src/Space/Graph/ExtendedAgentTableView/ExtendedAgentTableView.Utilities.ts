import { TaskStatusFilterKey } from '../../ScheduledTasks/V2/ScheduledTasksUtilities';

export const getFilterKeyFromTriggerStatus = (status: string = '') => {
    switch (status.toLowerCase()) {
        case 'active':
            return TaskStatusFilterKey.On;
        case 'paused':
            return TaskStatusFilterKey.Off;
        case 'completed':
            return TaskStatusFilterKey.Ended;
        default:
            return TaskStatusFilterKey.All;
    }
};
