import { createContext } from 'react';
import { Response } from '../../../../Common/Clients/DataPlaneClient';

export interface ScheduledTasksContextProps {
    refreshTasks: () => Promise<void>;
    pauseTask: (id: string) => Promise<Response<void>>;
    resumeTask: (id: string) => Promise<Response<void>>;
    deleteTask: (id: string) => Promise<Response<void>>;
}

export const ScheduledTasksContext = createContext<ScheduledTasksContextProps>({} as ScheduledTasksContextProps);
