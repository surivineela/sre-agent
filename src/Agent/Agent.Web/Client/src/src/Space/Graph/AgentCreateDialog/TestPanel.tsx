import { InfoLabel, mergeClasses, MessageBar, Text, tokens, ToolbarButton, Tooltip } from '@fluentui/react-components';
import { Dismiss24Regular, Replay16Regular } from '@fluentui/react-icons';
import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { ThreadSource } from '../../../Common/Contracts/DataPlane/Thread';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ChatBox } from '../../Activities/ChatBox';
import { ChatBoxStyleProps } from '../../Styles/Activities.styles';
import { useAgentCreateDialogStyles } from './AgentCreateDialog.Styles';
import { TestPanelProps } from './Contracts';

const playgroundChatStyles: ChatBoxStyleProps = {
    rootStyle: {
        flex: '1 1 auto',
        height: '0%',
    },
    chatBoxAndAgentTask: {
        width: '100%',
        boxShadow: 'none !important',
        borderRadius: tokens.borderRadiusLarge,
        height: '100%',
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        border: 'none !important',
    },
    chatBox: {
        '& > div': {
            padding: '0 !important',
        },
    },
    chatBoxInner: {
        borderRadius: tokens.borderRadiusLarge,
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        padding: `${tokens.spacingVerticalXXS} ${tokens.spacingHorizontalXS}`,
        border: 'none !important',
        boxShadow: 'none !important',
    },
};

export const TestPanel: FC<TestPanelProps> = memo(
    ({ agentName, threadId, restartTest, threadAutoTerminated, testStarted, addThread, onClose, chatKey }) => {
        const intl = useIntl();
        const styles = useAgentCreateDialogStyles();

        return (
            <div className={mergeClasses(styles.dialogContentWrapper, styles.chatBoxWrapper)}>
                <div className={styles.toolsPickerTitleWrapper}>
                    <InfoLabel info={testStarted ? intl.formatMessage(ExtendedAgentsGraphResources.testLiveAgentTooltip) : undefined}>
                        <Text size={400} weight="semibold">
                            {intl.formatMessage(ExtendedAgentsGraphResources.testLiveAgent)}
                        </Text>
                    </InfoLabel>
                    <Tooltip relationship="label" content={intl.formatMessage(ExtendedAgentsGraphResources.restartTestButton)}>
                        <ToolbarButton
                            appearance="transparent"
                            aria-label={intl.formatMessage(ExtendedAgentsGraphResources.restartTestButton)}
                            icon={<Replay16Regular />}
                            onClick={() => restartTest()}
                            className={styles.dialogCloseButton}
                            disabled={!chatKey}
                        />
                    </Tooltip>
                    <ToolbarButton appearance="transparent" icon={<Dismiss24Regular />} onClick={onClose}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.closePanel)}
                    </ToolbarButton>
                </div>
                {chatKey ? (
                    <ChatBox
                        key={chatKey}
                        threadId={threadId}
                        addThread={addThread}
                        updateThreadLastReadTime={() => {}}
                        threadSource={ThreadSource.playground}
                        stylesProps={playgroundChatStyles}
                        forcedAgentName={agentName}
                        lockAgentSelection={true}
                        renderEmptyState={() => (
                            <MessageBar intent="info" layout="multiline">
                                {intl.formatMessage(
                                    threadAutoTerminated
                                        ? ExtendedAgentsGraphResources.resumeTestThread
                                        : ExtendedAgentsGraphResources.startTestThread
                                )}
                            </MessageBar>
                        )}
                        canOpenSidePanel={true}
                    />
                ) : (
                    <MessageBar intent="info" layout="multiline">
                        {intl.formatMessage(ExtendedAgentsGraphResources.testThreadNoAgent)}
                    </MessageBar>
                )}
            </div>
        );
    }
);
