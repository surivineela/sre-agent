export enum ActionStatus {
    Pending = 'Pending',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Failed = 'Failed',
    All = 'All',
}

export interface Action {
    id: string;
    title: string;
    timeStamp: Date;
    status: ActionStatus;
}
