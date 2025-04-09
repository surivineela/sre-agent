import { memo, useContext } from 'react';
import { IThreadContentProps } from '../Contracts/Activities';
import { ThreadContentStyles } from '../Styles/Activities.styles';
import { Text } from '@fluentui/react/lib/Text';
import ChatBox from './ChatBox';
import { AgentContext } from './Activities.ReactView';
import { SreAgentResources } from '../../Strings/SREResources.resjson';

export const ThreadContent = memo(({ thread, addThread }: IThreadContentProps) => {
  const { threadContentKey } = useContext(AgentContext);

  return (
    <div className={ThreadContentStyles.root} key={threadContentKey}>
      <Text as="h2" nowrap block className={ThreadContentStyles.title}>
        {thread?.title ?? SreAgentResources.newThread}
      </Text>
      <ChatBox threadId={thread?.id} addThread={addThread} />
    </div>
  );
});

ThreadContent.displayName = 'ThreadContent';
