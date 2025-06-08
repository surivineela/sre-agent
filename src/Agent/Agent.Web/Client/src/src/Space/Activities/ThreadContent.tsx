import { Button } from '@fluentui/react-components';
import { PanelRightExpandRegular } from '@fluentui/react-icons';
import { Text } from '@fluentui/react/lib/Text';
import { memo, useCallback, useContext } from 'react';
import { useIntl } from 'react-intl';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IThreadContentProps } from '../Contracts/Activities';
import { AgentContext } from '../Contracts/Context';
import { getExpandCollapseButtonStyles, ThreadContentStyles } from '../Styles/Activities.styles';
import ChatBox from './ChatBox';
import ThreadActionsMenu from './ThreadActionsMenu';

const expandCollapseButtonStyles = getExpandCollapseButtonStyles('right');

export const ThreadContent = memo(
    ({ thread, addThread, promoteThread, deleteThread, actionsCollapsed, expandActions }: IThreadContentProps) => {
        const { threadContentAndActionKey } = useContext(AgentContext);
        const intl = useIntl();

        const handleThreadDelete = useCallback(() => {
            if (thread) {
                deleteThread(thread);
            }
        }, [thread, deleteThread]);

        return (
            <div className={ThreadContentStyles.root} key={threadContentAndActionKey}>
                <div className={ThreadContentStyles.titleContainer}>
                    <Text as="h2" nowrap block className={ThreadContentStyles.title}>
                        {thread?.title ?? intl.formatMessage(SreAgentResources.newThread)}
                    </Text>
                    {thread && <ThreadActionsMenu thread={thread} handleThreadDelete={handleThreadDelete} />}
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
                <ChatBox threadId={thread?.id} addThread={addThread} promoteThread={promoteThread} threadSource={thread?.source} />
            </div>
        );
    }
);

ThreadContent.displayName = 'ThreadContent';
