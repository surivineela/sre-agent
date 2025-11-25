import { createContext } from 'react';
import { Response } from '../../../Common/Clients/DataPlaneClient';
import { ExtendedAgentAnchorEntity } from '../../Contracts/ExtendedAgentGraph';
import { CreateScheduledTaskRequest } from '../../Contracts/ScheduledTasks';

export interface ScheduledTasksContextProps {
    refreshTasks: (anchorEntity?: ExtendedAgentAnchorEntity) => Promise<void>;
    createTask: (task: CreateScheduledTaskRequest) => Promise<Response<{ taskId: string }>>;
    updateTask: (id: string, task: CreateScheduledTaskRequest) => Promise<Response<void>>;
    runTask: (id: string) => Promise<Response<void>>;
    pauseTask: (id: string) => Promise<Response<void>>;
    resumeTask: (id: string) => Promise<Response<void>>;
    deleteTask: (id: string) => Promise<Response<void>>;
    isOperationInProgress: boolean;
    setIsOperationInProgress: (value: boolean) => void;
}

export const ScheduledTasksContext = createContext<ScheduledTasksContextProps>({} as ScheduledTasksContextProps);
