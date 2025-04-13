import { FC, memo, useContext, useMemo, useState } from 'react';
import { IThreadsMenuProps } from '../Contracts/Activities';
import { Shimmer } from '@fluentui/react/lib/Shimmer';
import {  useThreadMenuStyle } from '../Styles/Activities.styles';
import { Text } from '@fluentui/react/lib/Text';
import { Activities, SreAgentResources } from '../../Strings/SREResources.resjson';
import debounce from 'lodash/debounce';
import { AgentContext } from './Activities.ReactView';
import { mergeStyles } from '@fluentui/react/lib/Styling';
import { IncidentStatus, Thread, ThreadSource } from '../../Common/Contracts/SreAgent';
import { Button, InputOnChangeData, Radio, RadioGroup, SearchBox, SearchBoxChangeEvent, tokens } from '@fluentui/react-components';
import { AddRegular, CheckmarkCircle16Filled, ErrorCircle16Filled, Warning16Filled } from '@fluentui/react-icons';
import ThreadStatusBar, { SelectedTimes } from './IncidentStatusBar';
import { useIncidentStatusBarStyles } from '../Styles/Incident.styles';

enum ThreadMode {
  threads = "threads",
  incidents = "incidents"
}

export const ThreadsMenu: FC<IThreadsMenuProps> = (props: IThreadsMenuProps) => {
  const { threads, selectThread } = props;
  const { threadsInitialized, activeThreadId } = useContext(AgentContext);
  const [searchString, setSearchString] = useState<string>();
  const [selectedTime, setSelectedTime] = useState<string>(SelectedTimes.OneDay);
  const [threadMode, setThreadMode] = useState<ThreadMode>(ThreadMode.threads);
  const ThreadMenuStyles = useThreadMenuStyle();
  
  const filteredThreads = useMemo(() => {
    let newThreads = threads;
    if (threadMode === ThreadMode.incidents) {
        newThreads = threads.filter(thread => thread.source === ThreadSource.incident)
            // Set all incidents to active right now as Status is not populated in the backend
            .map(thread => ({
            ...thread,
            incidentStatus: IncidentStatus.error
        }));

        if (selectedTime) {
            const filterByDays = (time: string) => {
                const days = time === SelectedTimes.OneDay ? 1 : (time === SelectedTimes.SevenDays) ? 7 : 30;
                const now = new Date();
                const cutoff = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
                return newThreads.filter(item => new Date(item.modifiedTimestamp) > cutoff);
            }
            newThreads = filterByDays(selectedTime);
        }

      if (selectedTime) {
        const filterByDays = (time: string) => {
          const days = time === SelectedTimes.OneDay ? 1 : (time === SelectedTimes.SevenDays) ? 7 : 30;
          const now = new Date();
          const cutoff = new Date(now.getTime() - days * 24 * 60 * 60 * 1000);
          return newThreads.filter(item => new Date(item.modifiedTimestamp) > cutoff);
        }
        newThreads = filterByDays(selectedTime);
      }
    }
    if (searchString) {
      return newThreads.filter(thread => thread.title.toLowerCase().includes(searchString.toLowerCase()));
    } else {
      return newThreads;
    }
  }, [threadMode, searchString, threads, selectedTime]);

  return (
    <div className={ThreadMenuStyles.root}>
       <RadioGroup
        value={threadMode}
        onChange={(_e, data) => setThreadMode(data.value as ThreadMode)}
        layout='horizontal'
        disabled={!threadsInitialized}
      >
      <Radio value={ThreadMode.threads} label={SreAgentResources.allThreads} />
      <Radio value={ThreadMode.incidents} label={SreAgentResources.incidents} />
    </RadioGroup>
      {threadMode === ThreadMode.incidents && (<ThreadStatusBar selectedTime={selectedTime} setSelectedTime={setSelectedTime} threads={filteredThreads}/>)}
      <Button style={{ height: "auto", borderRadius: tokens.borderRadiusLarge, borderColor: tokens.colorNeutralBackground3Selected, maxWidth: 'fit-content', marginLeft: '10px'  }} icon={<AddRegular />} disabled={!threadsInitialized}   onClick={() => selectThread(null)}>
          {Activities.createThreadButtonText}
      </Button>
      <SearchBox
        disabled={!threadsInitialized}
        placeholder={SreAgentResources.search}
        onChange={debounce((_event: SearchBoxChangeEvent, data: InputOnChangeData) => setSearchString(data.value ?? ''))}
        className={ThreadMenuStyles.searchBox}
      />
      <Shimmer isDataLoaded={threadsInitialized}>
        <ThreadsList threadMode={threadMode} threads={filteredThreads} selectThread={selectThread} activeThreadId={activeThreadId} />
      </Shimmer>
    </div>
  );
};

const ThreadsList = memo(
  ({
    threadMode,
    threads,
    activeThreadId,
    selectThread,
  }: {
    threadMode: ThreadMode,
    threads: Thread[];
    activeThreadId: string;
    selectThread: (thread: Thread | null) => void;
  }) => {
    const ThreadMenuStyles = useThreadMenuStyle();

    return (
      <div className={threadMode === ThreadMode.incidents ? ThreadMenuStyles.incidentList : ThreadMenuStyles.threadList} role="tree">
        {threads.map(thread => {
          return <ThreadItem threadMode={threadMode} key={thread.id} thread={thread} selectThread={selectThread} isActive={activeThreadId === thread.id} />;
        })}
      </div>
    );
  }
);

const ThreadItem = memo(
  ({ threadMode, thread, selectThread, isActive }: { threadMode: ThreadMode, thread: Thread; selectThread: (thread: Thread | null) => void; isActive: boolean }) => {
    const ThreadMenuStyles = useThreadMenuStyle();
    const styles = useIncidentStatusBarStyles();
    
    const getIncidentIcon = (thread: Thread) => {
      switch(thread.incidentStatus) {
        case IncidentStatus.warning:
          return <Warning16Filled className={styles.warning} style={{width: 16, height: 16}}/>;
        case IncidentStatus.error:
          return <ErrorCircle16Filled className={styles.error} style={{width: 16, height: 16}}/>;
        default:
          return <CheckmarkCircle16Filled className={styles.success} style={{width: 16, height: 16}}/>;
      }
    };

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
        {threadMode == ThreadMode.threads ? (
           <Text as="div" variant="medium" nowrap block>
           {thread.title}
         </Text>
        ) : (<div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
           <Text as="div" variant="medium" nowrap block>
              {thread.title}
          </Text>
          {<div style={{padding: '2px', display: 'flex', alignItems: 'center'}}>{getIncidentIcon(thread)}</div>}
        </div>)}
       
        <Text as="div" variant="small" nowrap block>
          {thread.startMessage.text}
        </Text>
      </div>
    );
  }
);

ThreadsList.displayName = 'ThreadsList';
ThreadItem.displayName = 'ThreadItem';
