import { Button } from '@fluentui/react-components';
import { PanelRightExpandRegular } from '@fluentui/react-icons';
import { memo, useContext } from 'react';
import { useIntl } from 'react-intl';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import { ActivitiesResources } from '../../Strings/SREAgentResources';
import { IThreadContentProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { getExpandCollapseButtonStyles, ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ChatBoxV2 from './ChatBoxV2';
import ThreadContentTitle from './ThreadContentTitle';

const expandCollapseButtonStyles = getExpandCollapseButtonStyles('right');

export const ThreadContent = memo(
    ({ thread, addThread, deleteThread, updateThreadLastReadTime, actionsCollapsed, expandActions }: IThreadContentProps) => {
        const { threadContentAndActionKey } = useContext(AgentContext);
        const intl = useIntl();

        const chatBoxV2 = useConfigSetting(SettingNames.Streaming);

        return (
            <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
                <div className={ThreadContentStyles.titleContainer}>
                    <ThreadContentTitle thread={thread} deleteThread={deleteThread} />
                    {actionsCollapsed && (
                        <div style={expandCollapseButtonStyles.container}>
                            <Button
                                style={expandCollapseButtonStyles.button}
                                icon={<PanelRightExpandRegular />}
                                onClick={expandActions}
                                aria-label={intl.formatMessage(ActivitiesResources.showThreadActionsButtonText)}
                                appearance="transparent"
                            />
                        </div>
                    )}
                </div>
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
    }
);

ThreadContent.displayName = 'ThreadContent';
