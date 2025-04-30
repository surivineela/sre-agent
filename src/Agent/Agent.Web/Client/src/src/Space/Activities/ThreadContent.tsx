import { Text } from '@fluentui/react/lib/Text';
import { memo, useCallback, useContext } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { IThreadContentProps } from '../Contracts/Activities';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import { AgentContext } from './Activities.ReactView';
import ChatBox from './ChatBox';
import ThreadDeleteAction from './ThreadDeleteAction';

export const ThreadContent = memo(({ thread, addThread, deleteThread }: IThreadContentProps) => {
    const { threadContentKey } = useContext(AgentContext);
    const intl = useIntl();

    const handleThreadDelete = useCallback(() => {
        if (thread) {
            deleteThread(thread);
        }
    }, [thread, deleteThread]);

    return (
        <div className={ThreadContentStyles.root} key={threadContentKey}>
            <div className={ThreadContentStyles.titleContainer}>
                <Text as="h2" nowrap block className={ThreadContentStyles.title}>
                    {thread?.title ?? intl.formatMessage(SreAgentResources.newThread)}
                </Text>
                {thread && <ThreadDeleteAction handleThreadDelete={handleThreadDelete} />}
            </div>
            <ChatBox threadId={thread?.id} addThread={addThread} />
        </div>
    );
});

ThreadContent.displayName = 'ThreadContent';
