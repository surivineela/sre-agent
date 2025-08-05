import { memo, useCallback, useContext, useRef } from 'react';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { AgentTaskHandle, IThreadContentProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import AgentTask from './AgentTask';
import ChatBox from './ChatBox';
import ChatBoxV2 from './ChatBoxV2';
import ThreadContentTitle from './ThreadContentTitle';

export const ThreadContent = memo(
    ({ thread, addThread, deleteThread, updateThreadLastReadTime, collapseResizables }: IThreadContentProps) => {
        const { threadContentAndActionKey } = useContext(AgentContext);

        const chatBoxV2 = useConfigSetting(SettingNames.Streaming);
        const showAgentTask = useConfigSetting(SettingNames.ShowAgentTask);

        const agentTaskHandleRef = useRef<AgentTaskHandle>(null);

        const openAgentTask = useCallback((taskId: string) => {
            agentTaskHandleRef.current?.openAgentTask(taskId);
        }, []);

        return (
            <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
                <ThreadContentTitle thread={thread} deleteThread={deleteThread} />
                <div className={ThreadContentStyles.chatAndAgentTask}>
                    {chatBoxV2 ? (
                        <ChatBoxV2
                            threadId={thread?.id}
                            addThread={addThread}
                            updateThreadLastReadTime={updateThreadLastReadTime}
                            threadSource={thread?.source}
                            openAgentTask={openAgentTask}
                        />
                    ) : (
                        <ChatBox
                            threadId={thread?.id}
                            addThread={addThread}
                            updateThreadLastReadTime={updateThreadLastReadTime}
                            threadSource={thread?.source}
                        />
                    )}
                    {showAgentTask && <AgentTask collapseResizables={collapseResizables} ref={agentTaskHandleRef} />}
                </div>
            </div>
        );
    }
);

ThreadContent.displayName = 'ThreadContent';
