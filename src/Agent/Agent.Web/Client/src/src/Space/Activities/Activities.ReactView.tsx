import { createContext, FC } from 'react';
import { AgentContextProps } from '../Contracts/Activities';
import { useActivities } from '../Hooks/useActivities';
import { activitiesStylesRoot } from '../Styles/Activities.styles';
import { ThreadActions } from './ThreadActions';
import { ThreadContent } from './ThreadContent';
import { ThreadsMenu } from './ThreadsMenu';

export const AgentContext = createContext<AgentContextProps>({
    threadContentKey: '',
    threadsInitialized: false,
    activeThreadId: '',
});

interface IActivitiesProps {
    initialThreadId?: string | null;
}

const Activities: FC<IActivitiesProps> = ({ initialThreadId }: IActivitiesProps) => {
    const { threads, threadsInitialized, selectedThread, threadContentKey, selectThread, addThread, deleteThread, activeThreadId } =
        useActivities(initialThreadId);

    return (
        <AgentContext.Provider value={{ threadContentKey, threadsInitialized, activeThreadId }}>
            <div style={activitiesStylesRoot}>
                <ThreadsMenu threads={threads} selectThread={selectThread} />
                <ThreadContent thread={selectedThread} addThread={addThread} deleteThread={deleteThread} />
                <ThreadActions thread={selectedThread} />
            </div>
        </AgentContext.Provider>
    );
};

export default Activities;
