import { tokens } from '@fluentui/react-components';
import { FC, useCallback, useState } from 'react';
import { useParams } from 'react-router-dom';
import { InvestigationTreeProvider } from '../Contexts/InvestigationTreeProvider';
import { AgentContext } from '../Contracts/Context';
import { useActivities } from '../Hooks/useActivities';
import { useAgentTaskDevActivities } from '../Hooks/useAgentTaskDevActivities';
import { useInvestigationIntegration } from '../Hooks/useInvestigationIntegration';
import { activitiesStylesRoot } from '../Styles/Activities.styles';
import { Resizable, ResizableChildProps } from './Resizable';
import { ThreadActions } from './ThreadActions';
import { ThreadContent } from './ThreadContent';
import { ThreadsMenu } from './ThreadsMenu';

// Enhanced Activities component with all agent task logic isolated here
const EnhancedActivitiesContent: FC = () => {
    const {
        selectedThread,
        addThread: originalAddThread,
        deleteThread,
        selectThread: originalSelectThread,
        updateThreadLastReadTime,
        threadContentAndActionKey,
        activeThreadId,
        threadMenuHandleRef,
    } = useActivities();

    const { createEnhancedAddThread } = useAgentTaskDevActivities();

    const { handleThreadInvestigationUpdate } = useInvestigationIntegration();

    // Create an enhanced addThread that includes polling functionality
    const addThread = createEnhancedAddThread(originalAddThread);

    // Create an enhanced selectThread that includes investigation updates
    const selectThread = useCallback(
        async (thread: any) => {
            // First handle the basic thread selection
            await originalSelectThread(thread);

            // Then handle investigation updates
            await handleThreadInvestigationUpdate(thread);
        },
        [originalSelectThread, handleThreadInvestigationUpdate]
    );

    const [menuCollapsed, setMenuCollapsed] = useState<boolean>(false);
    const [actionsCollapsed, setActionsCollapsed] = useState<boolean>(true);

    const collapseResizables = useCallback(() => {
        setMenuCollapsed(true);
        setActionsCollapsed(true);
    }, []);

    // Exact same JSX as original Activities but with enhanced selectThread
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
                    style={{ backgroundColor: tokens.colorNeutralBackground3 }}
                >
                    {(resizableChildProps: ResizableChildProps) => (
                        <ThreadsMenu
                            selectThread={selectThread} // Enhanced version
                            deleteThread={deleteThread}
                            ref={threadMenuHandleRef}
                            {...resizableChildProps}
                        />
                    )}
                </Resizable>
                <ThreadContent
                    thread={selectedThread}
                    addThread={addThread}
                    deleteThread={deleteThread}
                    updateThreadLastReadTime={updateThreadLastReadTime}
                    collapseResizables={collapseResizables}
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
                    style={{ backgroundColor: tokens.colorNeutralBackground3 }}
                >
                    {(resizableChildProps: ResizableChildProps) => {
                        return <ThreadActions thread={selectedThread} {...resizableChildProps} />;
                    }}
                </Resizable>
            </div>
        </AgentContext.Provider>
    );
};

export const ActivitiesAgentTask: FC = () => {
    const { threadId: urlThreadId } = useParams();
    const { selectedThread } = useActivities();

    // Use URL thread ID as the primary source of truth, fallback to selectedThread ID
    const currentThreadId = urlThreadId || selectedThread?.id;

    return (
        <InvestigationTreeProvider threadId={currentThreadId}>
            <EnhancedActivitiesContent />
        </InvestigationTreeProvider>
    );
};
