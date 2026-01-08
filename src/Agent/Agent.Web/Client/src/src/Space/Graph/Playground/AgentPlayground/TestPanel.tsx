import { mergeClasses, MessageBar, Text, tokens } from '@fluentui/react-components';
import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { ThreadSource } from '../../../../Common/Contracts/DataPlane/Thread';
import { ExtendedAgentsGraphResources, PlaygroundResources } from '../../../../Strings/SREAgentResources';
import { ChatBox } from '../../../Activities/ChatBox';
import { ChatBoxStyleProps } from '../../../Styles/Activities.styles';
import { useAgentPlaygroundStyles } from './AgentPlayground.Styles';
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
    ({ mode, agentName, threadId, testStarted, addThread, selectThread, chatKey, onTelemetryUpdate, hidden }) => {
        const intl = useIntl();
        const styles = useAgentPlaygroundStyles();

        return (
            <div
                className={mergeClasses(styles.dialogContentWrapper, styles.chatBoxWrapper)}
                style={{ display: hidden ? 'none' : undefined }}
            >
                {mode !== 'test' && testStarted && (
                    <MessageBar intent="info" layout="multiline">
                        {intl.formatMessage(PlaygroundResources.unsavedChangesTestingContinue)}
                    </MessageBar>
                )}
                {chatKey ? (
                    <ChatBox
                        key={chatKey}
                        threadId={threadId}
                        addThread={addThread}
                        selectThread={selectThread}
                        updateThreadLastReadTime={() => {}}
                        threadSource={ThreadSource.playground}
                        stylesProps={playgroundChatStyles}
                        forcedAgentName={agentName}
                        lockAgentSelection={true}
                        renderEmptyState={() => (
                            <div
                                style={{
                                    display: 'flex',
                                    flexDirection: 'column',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    height: '100%',
                                    gap: '16px',
                                    overflow: 'auto',
                                }}
                            >
                                <img src="./AIChatLM.svg" alt="AI Chat" style={{ height: 128 }} />
                                <Text>
                                    {mode !== 'test'
                                        ? intl.formatMessage(PlaygroundResources.unsavedChangesTestingBegin)
                                        : intl.formatMessage(ExtendedAgentsGraphResources.startTestThread)}
                                </Text>
                            </div>
                        )}
                        canOpenSidePanel={true}
                        onTelemetryUpdate={onTelemetryUpdate}
                        inputDisabledMessage={
                            mode === 'test'
                                ? undefined
                                : testStarted
                                  ? intl.formatMessage(PlaygroundResources.unsavedChangesTestingContinue)
                                  : intl.formatMessage(PlaygroundResources.unsavedChangesTestingBegin)
                        }
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
