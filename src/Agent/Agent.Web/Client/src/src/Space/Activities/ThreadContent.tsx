import { memo, useCallback, useContext, useRef } from 'react';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { AgentTaskHandle, IThreadContentProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import AgentTask from './AgentTask';
import ChatBox from './ChatBox';
import ThreadContentTitle from './ThreadContentTitle';

export const ThreadContent = memo(
    ({ thread, addThread, deleteThread, updateThreadLastReadTime, collapseResizables }: IThreadContentProps) => {
        const { threadContentAndActionKey } = useContext(AgentContext);

        const showAgentTask = useConfigSetting(SettingNames.ShowAgentTask);

        const agentTaskHandleRef = useRef<AgentTaskHandle>(null);

        const openAgentTask = useCallback((taskId: string) => {
            agentTaskHandleRef.current?.openAgentTask(taskId);
        }, []);

        return (
            <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
                <ThreadContentTitle thread={thread} deleteThread={deleteThread} />
                <div className={ThreadContentStyles.chatAndAgentTask}>
                    <ChatBox
                        threadId={thread?.id}
                        addThread={addThread}
                        updateThreadLastReadTime={updateThreadLastReadTime}
                        threadSource={thread?.source}
                        openAgentTask={openAgentTask}
                    />
                    {showAgentTask && <AgentTask collapseResizables={collapseResizables} ref={agentTaskHandleRef} />}
                </div>
            </div>
        );
    }
);

ThreadContent.displayName = 'ThreadContent';
