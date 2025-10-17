import { ScheduledTaskStatus } from '../../Contracts/ScheduledTasks';

export enum TaskStatusFilterKey {
    All = 'all',
    On = 'on',
    Off = 'off',
    Completed = 'completed',
}

export const getFilterKeyFromScheduledTaskStatus = (status: ScheduledTaskStatus) => {
    switch (status) {
        case ScheduledTaskStatus.Active:
            return TaskStatusFilterKey.On;
        case ScheduledTaskStatus.Paused:
            return TaskStatusFilterKey.Off;
        case ScheduledTaskStatus.Completed:
            return TaskStatusFilterKey.Completed;
        default:
            return TaskStatusFilterKey.All;
    }
};
