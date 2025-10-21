import { createContext } from 'react';
import { Response } from '../../../../Common/Clients/DataPlaneClient';
import { CreateScheduledTaskRequest } from '../../../Contracts/ScheduledTasks';

export interface ScheduledTasksContextProps {
    refreshTasks: () => Promise<void>;
    createTask: (task: CreateScheduledTaskRequest) => Promise<Response<{ taskId: string }>>;
    pauseTask: (id: string) => Promise<Response<void>>;
    resumeTask: (id: string) => Promise<Response<void>>;
    deleteTask: (id: string) => Promise<Response<void>>;
}

export const ScheduledTasksContext = createContext<ScheduledTasksContextProps>({} as ScheduledTasksContextProps);
