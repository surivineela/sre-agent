import { FeedbackButtons } from '@fluentui-copilot/react-copilot';
import {
    CopilotMessageV2 as CopilotMessage,
    CopilotMessageV2Props as CopilotMessageProps,
    UserMessageV2 as UserMessage,
} from '@fluentui-copilot/react-copilot-chat';
import { mergeStyleSets } from '@fluentui/react';
import { Image, mergeClasses, Text, tokens } from '@fluentui/react-components';
import axios from 'axios';
import mermaid from 'mermaid';
import { memo, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import CopyButton from '../../Common/Components/CopyButton';
import { getAgentModeDisplayName } from '../../Common/Helpers/AgentMode';
import { formatDateTimeWithShortYear, getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { isChatMessageEmpty, shouldGroupWithPreviousMessageV2 } from '../Activities/Utility';
import { AgentMessageRegex, IChatMessageV2Props } from '../Contracts/Activities';
import { SreAgentContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ChatBoxV2Styles as ChatBoxStyles, nameAndTimestampContainerStyle, useChatBoxStyles } from '../Styles/Activities.styles';
import AgentMessage from './AgentMessage';
import AgentMessageLoadingComponent from './AgentMessageLoadingComponent';
import { FeedbackDialog } from './FeedbackDialog';
import ReactMarkdownComponent from './ReactMarkdownComponent';

const chatMessageStyles = mergeStyleSets({
    regularMessageContent: {
        backgroundColor: tokens.colorNeutralBackground3,
        padding: '0px 16px',
        borderRadius: tokens.borderRadiusXLarge,
    },
    codeBlock: {
        backgroundColor: tokens.colorNeutralBackground6,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'inline-block',
        padding: '2px 4px',
        borderRadius: tokens.borderRadiusSmall,
    },
    codeBlockInPre: {
        backgroundColor: tokens.colorTransparentBackground,
        fontFamily: tokens.fontFamilyMonospace,
        fontSize: tokens.fontSizeBase200,
        display: 'block',
    },
    preBlock: {
        overflowX: 'auto',
        overflowY: 'hidden',
        backgroundColor: tokens.colorNeutralBackground6,
        borderRadius: tokens.borderRadiusSmall,
        padding: '15px',
    },
    toolCallText: {
        background: `linear-gradient(90deg, ${tokens.colorNeutralForeground3}, ${tokens.colorNeutralBackground6}, ${tokens.colorNeutralForeground1})`,
        backgroundSize: '200% 100%',
        backgroundClip: 'text',
        color: 'transparent',
        animation: 'shimmer 2s infinite linear',
        marginBottom: `${tokens.spacingVerticalS}px`,
    },
});

// Initialize mermaid with default configuration
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: { useMaxWidth: false },
    securityLevel: 'loose',
});

// Add table styling for markdown tables
const tableStyles = `
  table {
    border-spacing: 0;
    border-collapse: collapse;
    display: block;
    padding: 1px;
    margin-top: 0;
    margin-bottom: 16px;
    width: max-content;
    max-width: 100%;
    overflow: auto;
    border-radius: 8px;
  }

  tr {
    background-color: var(--color-canvas-default, #ffffff);
    border-top: 1px solid var(--color-border-muted, #d0d7de);
  }

  tr:nth-child(2n) {
    background-color: var(--color-canvas-subtle, #f6f8fa);
  }

  td,
  th {
    padding: 6px 13px;
    border: 1px solid var(--color-border-default, #d0d7de);
  }

  th {
    font-weight: 600;
  }

  /* Round corners for first and last cells in first and last rows */
  tr:first-child th:first-child {
    border-top-left-radius: 8px;
  }
  tr:first-child th:last-child {
    border-top-right-radius: 8px;
  }
  tr:last-child td:first-child {
    border-bottom-left-radius: 8px;
  }
  tr:last-child td:last-child {
    border-bottom-right-radius: 8px;
  }

  table img {
    background-color: transparent;
  }

  @media (prefers-color-scheme: dark) {
  tr {
    background-color: #161b22;
    border-top: 1px solid #30363d;
  }

  tr:nth-child(2n) {
    background-color: #21262d;
  }

  td,
  th {
    border: 1px solid #444c56;
    color: #c9d1d9;
  }

  th {
    background-color: #21262d;
    font-weight: bold;
  }
}`;

const ChatMessageV2 = ({
    message,
    previousMessage,
    nextMessage,
    getGroupedMessages,
    isTyping,
    threadId,
    isStreamingMessage,
    toolCallText,
    isStreamingEmpty,
}: IChatMessageV2Props) => {
    const chatStyles = useChatBoxStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const sreAgentContext = useContext(SreAgentContext);
    const {
        agent: { mode },
    } = sreAgentContext;

    const [showFeedbackDialog, setShowFeedbackDialog] = useState(false);
    const [selectedFeedback, setSelectedFeedback] = useState<'positive' | 'negative'>();
    const [hasSubmittedFeedback, setHasSubmittedFeedback] = useState(false);

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const agentMode = useMemo(() => getAgentModeDisplayName(mode, intl), [intl, mode]);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: <Image src="./SreAgent.svg" width={28} height={28} alt={intl.formatMessage(SreAgentResources.azureSreAgent)} />,
            loadingState: 'none',
            mode: 'canvas',
            name: (
                <div style={nameAndTimestampContainerStyle}>
                    <span>{intl.formatMessage(SreAgentResources.sreAgent)}</span>
                    {mode && <span className={chatStyles.modePill}>{agentMode}</span>}
                    {!isTyping && message.timeStamp && (
                        <Text size={200} color={tokens.colorNeutralForeground3}>
                            {formatDateTimeWithShortYear(getSafeDateTime(message.timeStamp))}
                        </Text>
                    )}
                </div>
            ),
            disclaimer: null,
        };

        return messageProps;
    }, [intl, mode, chatStyles.modePill, agentMode, isTyping, message.timeStamp]);

    // Hide message's icon, name and timestamp if the message is grouped with the previous one
    const hideMessageHeader = useMemo(() => shouldGroupWithPreviousMessageV2(message, previousMessage), [message, previousMessage]);
    // Show feedback buttons if the message is from SREAgent, not typing and it is the last message in the group
    const showFeedbackButtons = useMemo(
        () => message.author.role === 'SREAgent' && !isTyping && !shouldGroupWithPreviousMessageV2(nextMessage, message),
        [message, nextMessage, isTyping]
    );
    const showCopyMessageButton = useMemo(
        () =>
            message.author.role === 'SREAgent' &&
            !isTyping &&
            message.contents.some(content => !!content.text) &&
            !shouldGroupWithPreviousMessageV2(nextMessage, message),
        [message, nextMessage, isTyping]
    );

    const isMessageEmpty = useMemo(() => {
        return isChatMessageEmpty(message);
    }, [message]);

    const filteredMessageContentToCopy = useMemo(() => {
        if (!showCopyMessageButton || !getGroupedMessages) return '';

        const groupedMessages = getGroupedMessages(message.id);
        return groupedMessages
            .map(msg =>
                msg.contents.map(msgContent => {
                    return msgContent.text
                        .trim()
                        .replace(AgentMessageRegex.imageRegex, '[Image]')
                        .replace(AgentMessageRegex.mermaidRegex, '[Mermaid Diagram]')
                        .replace(AgentMessageRegex.chartRegex, '[Chart]');
                })
            )
            .flat()
            .join('\n\n');
    }, [showCopyMessageButton, getGroupedMessages, message.id]);

    const handleFeedbackClick = async (isPositive: boolean) => {
        setSelectedFeedback(isPositive ? 'positive' : 'negative');

        if (isPositive) {
            try {
                const url = `${sreAgentEndpoint}/api/v1/threads/${threadId}/feedbacks`;
                await axios.post(
                    url,
                    {
                        isPositive: true,
                        feedbackText: '',
                    },
                    {
                        headers: getAgentHeaders(),
                    }
                );
                setHasSubmittedFeedback(true);
            } catch (error) {
                console.error('Failed to send positive feedback:', error);
            }
        } else {
            setShowFeedbackDialog(true);
        }
    };

    const Loading = () => {
        return (
            isStreamingMessage &&
            isTyping &&
            isStreamingEmpty && (
                <div style={{ margin: '5px 5px 0px 5px' }}>
                    <AgentMessageLoadingComponent />
                </div>
            )
        );
    };

    const ToolCallTextComponent = () => {
        const styles = `
            @keyframes shimmer {
                0% {
                    background-position: 100% 0;
                }
                100% {
                    background-position: -100% 0;
                }
            }
        `;

        return (
            isTyping &&
            toolCallText && (
                <>
                    <style>{styles}</style>
                    <Text className={chatMessageStyles.toolCallText}>{toolCallText}</Text>
                </>
            )
        );
    };

    const MessageFooter = () => {
        return (
            !isMessageEmpty && (
                <div style={{ display: 'flex', flexDirection: 'row' }}>
                    {showFeedbackButtons && ( // Only show buttons when the agent is not typing
                        <FeedbackButtons
                            positiveFeedbackButton={{ onClick: () => handleFeedbackClick(true) }}
                            negativeFeedbackButton={{ onClick: () => handleFeedbackClick(false) }}
                            selected={selectedFeedback}
                            disabled={hasSubmittedFeedback}
                        />
                    )}
                    {showCopyMessageButton && <CopyButton textToCopy={filteredMessageContentToCopy} />}
                </div>
            )
        );
    };

    switch (message.author.role) {
        case 'SREAgent':
            return (
                <div style={isStreamingMessage ? { minHeight: 'calc(100% - 120px)' } : undefined}>
                    <style>{tableStyles}</style>
                    <CopilotMessage
                        {...agentMessageProps}
                        key={message.id}
                        style={{ font: 'Segoe UI', lineHeight: '20px', wordBreak: 'unset', maxWidth: '90%' }}
                        className={mergeClasses(
                            ChatBoxStyles.agentMessage,
                            hideMessageHeader ? ChatBoxStyles.hideAgentMessageHeader : undefined
                        )}
                    >
                        <Loading key={`${message.id}-loading`} />

                        {message.contents.map((content, index) => {
                            return (
                                <AgentMessage
                                    key={index}
                                    messageContent={content}
                                    messageId={message.id}
                                    timeStamp={message.timeStamp}
                                    isTyping={isTyping}
                                    threadId={threadId}
                                />
                            );
                        })}

                        <ToolCallTextComponent key={`${message.id}-tool-call-text`} />
                        <MessageFooter key={`${message.id}-message-footer`} />
                    </CopilotMessage>

                    <FeedbackDialog
                        isOpen={showFeedbackDialog}
                        setIsOpen={setShowFeedbackDialog}
                        threadId={threadId}
                        clearSelectedFeedback={() => setSelectedFeedback(undefined)}
                        setHasSubmittedFeedback={setHasSubmittedFeedback}
                    />
                </div>
            );
        default:
            return (
                <div className={ChatBoxStyles.userMessage} key={message.id}>
                    {hideMessageHeader ? null : (
                        <div style={nameAndTimestampContainerStyle}>
                            {message.author.userId !== userIdAndDisplayName.userId && (
                                <Text block={true} weight={'semibold'} className={chatStyles.userName}>
                                    {message.author.displayName}
                                </Text>
                            )}
                            <Text size={200} color={tokens.colorNeutralForeground3} style={{ lineHeight: '26px' }}>
                                {formatDateTimeWithShortYear(getSafeDateTime(message.timeStamp))}
                            </Text>
                        </div>
                    )}
                    <UserMessage className={chatStyles.userBubble} message={{ className: chatStyles.userBubbleMessage }} key={message.id}>
                        <ReactMarkdownComponent key={message.id} content={message.contents?.[0]?.text} isUserMessage={true} />
                    </UserMessage>
                </div>
            );
    }
};

export default memo(ChatMessageV2);
