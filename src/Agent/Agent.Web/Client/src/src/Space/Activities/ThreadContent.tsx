import { memo, useContext } from 'react';
import { IThreadContentProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ThreadContentTitle from './ThreadContentTitle';

export const ThreadContent = memo(
    ({ thread, addThread, deleteThread, updateThreadLastReadTime, collapseResizables }: IThreadContentProps) => {
        const { threadContentAndActionKey } = useContext(AgentContext);

        return (
            <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
                <ThreadContentTitle thread={thread} deleteThread={deleteThread} />
                <ChatBox
                    threadId={thread?.id}
                    addThread={addThread}
                    updateThreadLastReadTime={updateThreadLastReadTime}
                    threadSource={thread?.source}
                    collapseResizables={collapseResizables}
                    isAgentTaskEnabled={true}
                />
            </div>
        );
    }
);

ThreadContent.displayName = 'ThreadContent';
