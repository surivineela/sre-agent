export interface TodoPlan {
    id: string;
    title: string;
    threadId: string;
    triggerMessageId: string;
    status: TodoPlanStatus;
    items: TodoItem[];
    createdAt: string;
    lastUpdated?: string;
}

export interface TodoItem {
    content: string;
    activeForm: string;
    status: TodoItemStatus;
    order: number;
    startedAt?: string;
    completedAt?: string;
}

export enum TodoPlanStatus {
    Planning = 'Planning',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Cancelled = 'Cancelled',
}

export enum TodoItemStatus {
    Pending = 'Pending',
    InProgress = 'InProgress',
    Completed = 'Completed',
    Failed = 'Failed',
}
