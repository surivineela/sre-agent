import { createContext, FC, useState } from 'react';
import { AgentContextProps } from '../Contracts/Activities';
import { useActivities } from '../Hooks/useActivities';
import { activitiesStylesRoot } from '../Styles/Activities.styles';
import { Resizable, ResizableChildProps } from './Resizable';
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

    const [menuCollapsed, setMenuCollapsed] = useState<boolean>(false);
    const [actionsCollapsed, setActionsCollapsed] = useState<boolean>(true);

    return (
        <AgentContext.Provider value={{ threadContentAndActionKey, threadsInitialized, activeThreadId }}>
            <div style={activitiesStylesRoot}>
                <Resizable
                    position="left"
                    initialWidth="320px"
                    minWidthPixels={200}
                    maxWidthPixels={640}
                    maxWidthPercent={actionsCollapsed ? 50 : 33}
                    collapsedWidthPixels={70}
                    collapsed={menuCollapsed}
                    setCollapsed={setMenuCollapsed}
                >
                    {(resizableChildProps: ResizableChildProps) => (
                        <ThreadsMenu threads={threads} selectThread={selectThread} {...resizableChildProps} />
                    )}
                </Resizable>
                <ThreadContent
                    thread={selectedThread}
                    addThread={addThread}
                    deleteThread={deleteThread}
                    actionsCollapsed={actionsCollapsed}
                    expandActions={() => setActionsCollapsed(false)}
                />
                <Resizable
                    position="right"
                    initialWidth="285px"
                    minWidthPixels={200}
                    maxWidthPixels={640}
                    maxWidthPercent={menuCollapsed ? 50 : 33}
                    collapsedWidthPixels={0}
                    collapsed={actionsCollapsed}
                    setCollapsed={setActionsCollapsed}
                >
                    {(resizableChildProps: ResizableChildProps) => {
                        return <ThreadActions thread={selectedThread} {...resizableChildProps} />;
                    }}
                </Resizable>
            </div>
        </AgentContext.Provider>
    );
};

export default Activities;
