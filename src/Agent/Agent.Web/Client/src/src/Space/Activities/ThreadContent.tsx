import { memo, useContext } from 'react';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { IThreadContentProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ChatBoxV2 from './ChatBoxV2';
import ThreadContentTitle from './ThreadContentTitle';

export const ThreadContent = memo(({ thread, addThread, deleteThread, updateThreadLastReadTime }: IThreadContentProps) => {
    const { threadContentAndActionKey } = useContext(AgentContext);

    const chatBoxV2 = useConfigSetting(SettingNames.Streaming);

    return (
        <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
            <ThreadContentTitle thread={thread} deleteThread={deleteThread} />
            {chatBoxV2 ? (
                <ChatBoxV2
                    threadId={thread?.id}
                    addThread={addThread}
                    updateThreadLastReadTime={updateThreadLastReadTime}
                    threadSource={thread?.source}
                />
            ) : (
                <ChatBox
                    threadId={thread?.id}
                    addThread={addThread}
                    updateThreadLastReadTime={updateThreadLastReadTime}
                    threadSource={thread?.source}
                />
            )}
        </div>
    );
});

ThreadContent.displayName = 'ThreadContent';
