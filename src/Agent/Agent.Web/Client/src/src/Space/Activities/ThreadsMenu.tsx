import { FC, memo, useContext, useMemo, useState } from 'react';
import { IThreadsMenuProps } from '../Contracts/Activities';
import { Shimmer } from '@fluentui/react/lib/Shimmer';
import {  useThreadMenuStyle } from '../Styles/Activities.styles';
import { Text } from '@fluentui/react/lib/Text';
import { Activities, SreAgentResources } from '../../Strings/SREResources.resjson';
import debounce from 'lodash/debounce';
import { AgentContext } from './Activities.ReactView';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { Thread } from '../../Common/Contracts/SreAgent';
import { Button, InputOnChangeData, SearchBox, SearchBoxChangeEvent, tokens } from '@fluentui/react-components';
import { AddRegular } from '@fluentui/react-icons';

export const ThreadsMenu: FC<IThreadsMenuProps> = (props: IThreadsMenuProps) => {
  const { threads, selectThread } = props;
  const { threadsInitialized, activeThreadId } = useContext(AgentContext);
  const [searchString, setSearchString] = useState<string>();
  const ThreadMenuStyles = useThreadMenuStyle();
  
  const filteredThreads = useMemo(() => {
    if (searchString) {
      return threads.filter(thread => thread.title.toLowerCase().includes(searchString.toLowerCase()));
    } else {
      return threads;
    }
  }, [searchString, threads]);

  return (
    <div className={ThreadMenuStyles.root}>
      <Button style={{borderRadius: tokens.borderRadiusLarge, borderColor: tokens.colorNeutralBackground3Selected, maxWidth: 'fit-content', marginLeft: '10px'  }} icon={<AddRegular />} disabled={!threadsInitialized}   onClick={() => selectThread(null)}>
          {Activities.createThreadButtonText}
      </Button>
      <SearchBox
        disabled={!threadsInitialized}
        placeholder={SreAgentResources.search}
        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchString(data.value ?? ''))}
        className={ThreadMenuStyles.searchBox}
      />
      <Shimmer isDataLoaded={threadsInitialized}>
        <ThreadsList threads={filteredThreads} selectThread={selectThread} activeThreadId={activeThreadId} />
      </Shimmer>
    </div>
  );
};

const ThreadsList = memo(
  ({
    threads,
    activeThreadId,
    selectThread,
  }: {
    threads: Thread[];
    activeThreadId: string;
    selectThread: (thread: Thread | null) => void;
  }) => {
    const ThreadMenuStyles = useThreadMenuStyle();

    return (
      <div className={ThreadMenuStyles.threadList} role="tree">
        {threads.map(thread => {
          return <ThreadItem key={thread.id} thread={thread} selectThread={selectThread} isActive={activeThreadId === thread.id} />;
        })}
      </div>
    );
  }
);

const ThreadItem = memo(
  ({ thread, selectThread, isActive }: { thread: Thread; selectThread: (thread: Thread | null) => void; isActive: boolean }) => {
    const ThreadMenuStyles = useThreadMenuStyle();

    return (
      <div
        onClick={() => selectThread(thread)}
        onKeyDown={e => {
          if (e.key.toLowerCase() === 'enter') {
            selectThread(thread);
            e.stopPropagation();
          }
        }}
        id={thread.id}
        tabIndex={0}
        role="treeitem"
        className={mergeStyles(ThreadMenuStyles.threadItem, isActive ? ThreadMenuStyles.activeThreadItem : undefined)}
      >
        <Text as="div" variant="medium" nowrap block>
          {thread.title}
        </Text>
        <Text as="div" variant="small" nowrap block>
          {thread.startMessage.text}
        </Text>
      </div>
    );
  }
);

ThreadsList.displayName = 'ThreadsList';
ThreadItem.displayName = 'ThreadItem';
