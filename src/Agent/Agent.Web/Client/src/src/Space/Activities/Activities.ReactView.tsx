import { FC, useState } from 'react';
import { AgentContext } from '../Contracts/Context';
import { useActivities } from '../Hooks/useActivities';
import { activitiesStylesRoot } from '../Styles/Activities.styles';
import { Resizable, ResizableChildProps } from './Resizable';
import { ThreadActions } from './ThreadActions';
import { ThreadContent } from './ThreadContent';
import { ThreadsMenu } from './ThreadsMenu';

const Activities: FC = () => {
    const {
        selectedThread,
        addThread,
        promoteThread,
        deleteThread,
        selectThread,
        updateThreadLastReadTime,
        threadContentAndActionKey,
        activeThreadId,
        threadPollingTriggerId,
        threadListHandleRef,
    } = useActivities();

    const [menuCollapsed, setMenuCollapsed] = useState<boolean>(false);
    const [actionsCollapsed, setActionsCollapsed] = useState<boolean>(true);

    return (
        <AgentContext.Provider value={{ threadContentAndActionKey, activeThreadId }}>
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
                        <ThreadsMenu
                            selectThread={selectThread}
                            deleteThread={deleteThread}
                            threadPollingTriggerId={threadPollingTriggerId}
                            ref={threadListHandleRef}
                            {...resizableChildProps}
                        />
                    )}
                </Resizable>
                <ThreadContent
                    thread={selectedThread}
                    addThread={addThread}
                    deleteThread={deleteThread}
                    promoteThread={promoteThread}
                    updateThreadLastReadTime={updateThreadLastReadTime}
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
