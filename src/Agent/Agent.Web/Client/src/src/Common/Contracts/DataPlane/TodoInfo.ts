import { TodoPlanStatus } from './TodoPlan';

export interface TodoInfo {
    id: string;
    title: string;
    status: TodoPlanStatus;
    lastModified?: string;
    triggerMessageId: string;
}