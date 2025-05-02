import { createContext, FC } from 'react';
import { AgentContextProps } from '../Contracts/Activities';
import { useActivities } from '../Hooks/useActivities';
import { activitiesStylesRoot } from '../Styles/Activities.styles';
import { ThreadActions } from './ThreadActions';
import { ThreadContent } from './ThreadContent';
import { ThreadsMenu } from './ThreadsMenu';

export const AgentContext = createContext<AgentContextProps>({
    threadContentAndActionKey: '',
    threadsInitialized: false,
    activeThreadId: '',
});

const Activities: FC = () => {
    const {
        threads,
        threadsInitialized,
        selectedThread,
        threadContentAndActionKey,
        selectThread,
        addThread,
        deleteThread,
        activeThreadId,
    } = useActivities();

    return (
        <AgentContext.Provider value={{ threadContentAndActionKey, threadsInitialized, activeThreadId }}>
            <div style={activitiesStylesRoot}>
                <ThreadsMenu threads={threads} selectThread={selectThread} />
                <ThreadContent thread={selectedThread} addThread={addThread} deleteThread={deleteThread} />
                <ThreadActions thread={selectedThread} />
            </div>
        </AgentContext.Provider>
    );
};

export default Activities;
