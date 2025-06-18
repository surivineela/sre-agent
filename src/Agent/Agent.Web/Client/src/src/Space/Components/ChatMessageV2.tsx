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
import { memo, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import ReactMarkdown from 'react-markdown';
import rehypeRaw from 'rehype-raw';
import remarkGfm from 'remark-gfm';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import CopyButton from '../../Common/Components/CopyButton';
import IncidentAlert from '../../Common/Components/IncidentAlert';
import InvestigationSummary from '../../Common/Components/InvestigationSummary';
import InvestigationSummaryPanel from '../../Common/Components/InvestigationSummaryPanel';
import { getAgentModeDisplayName } from '../../Common/Helpers/AgentMode';
import { formatDateTimeWithShortYear, getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { shouldGroupWithPreviousMessage } from '../Activities/Utility';
import { IChatMessageV2Props } from '../Contracts/Activities';
import { SreAgentContext } from '../Contracts/Context';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ChatBoxStyles, nameAndTimestampContainerStyle, useChatBoxStyles } from '../Styles/Activities.styles';
import ApprovalMessage from './ApprovalMessage';
import AzCliExecutionMessage from './AzCliExecutionMessage';
import AgentChart from './Charts';
import DailyReportMessage from './DailyReportMessage';
import { FeedbackDialog } from './FeedbackDialog';
import KubectlExecutionMessage from './KubectlExecutionMessage';
import MermaidChart from './Mermaid';

// Check for markdown image syntax with base64 data
const imageRegex = /!\[(.*?)\]\((data:image\/[a-z]+;base64,[A-Za-z0-9+/=]+)\)/g;
// Check for mermaid code blocks
const mermaidRegex = /```mermaid\n([\s\S]*?)\n```/g;
// Check for chart data blocks
const chartRegex = /```chart-data\n([\s\S]*?)\n```/g;
// Check if the entire message is just a incident-alert block
const incidentAlertRegex = /```incident-alert\s+([\s\S]*?)```/;
// Check for investigation summary formats
const investigationSummaryRegex = /<investigation-summary>([\s\S]*?)<\/investigation-summary>/;
const investigationSummariesRegex = /<investigation-summaries>([\s\S]*?)<\/investigation-summaries>/;

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

// Helper function to parse and render markdown with images and mermaid diagrams
const processMessageText = (text: string) => {
    if (!text) return text;

    if (!imageRegex.test(text) && !mermaidRegex.test(text) && !chartRegex.test(text)) {
        return text; // No special content, return original text
    }

    // Reset regex lastIndex properties to ensure we start from the beginning
    imageRegex.lastIndex = 0;
    mermaidRegex.lastIndex = 0;
    chartRegex.lastIndex = 0;

    // Split images, mermaid blocks, and text
    const parts: (string | { type: string; [key: string]: any })[] = [];
    let lastIndex = 0;

    // Function to process a match and add it to the parts array
    const processMatch = (match: RegExpExecArray, type: string) => {
        if (match.index > lastIndex) {
            parts.push(text.substring(lastIndex, match.index));
        }

        if (type === 'image') {
            parts.push({
                type: 'image',
                alt: match[1],
                src: match[2],
            });
        } else if (type === 'mermaid') {
            parts.push({
                type: 'mermaid',
                content: match[1],
            });
        } else if (type === 'chart-data') {
            parts.push({
                type: 'chart-data',
                content: match[0], // Include the entire match with the markers
            });
        }

        lastIndex = match.index + match[0].length;
    };

    // Find all matches and process them in order of appearance
    let imageMatch: RegExpExecArray | null;
    let mermaidMatch: RegExpExecArray | null;
    let chartMatch: RegExpExecArray | null;

    // Initialize the first matches
    imageMatch = imageRegex.exec(text);
    mermaidMatch = mermaidRegex.exec(text);
    chartMatch = chartRegex.exec(text);

    while (imageMatch || mermaidMatch || chartMatch) {
        // Find the match that appears first in the text
        let firstMatch: RegExpExecArray | null = null;
        let matchType = '';

        if (
            imageMatch &&
            (!mermaidMatch || imageMatch.index < mermaidMatch.index) &&
            (!chartMatch || imageMatch.index < chartMatch.index)
        ) {
            firstMatch = imageMatch;
            matchType = 'image';
            imageMatch = imageRegex.exec(text);
        } else if (mermaidMatch && (!chartMatch || mermaidMatch.index < chartMatch.index)) {
            firstMatch = mermaidMatch;
            matchType = 'mermaid';
            mermaidMatch = mermaidRegex.exec(text);
        } else if (chartMatch) {
            firstMatch = chartMatch;
            matchType = 'chart-data';
            chartMatch = chartRegex.exec(text);
        }

        if (firstMatch) {
            processMatch(firstMatch, matchType);
        }
    }

    // Add any remaining text
    if (lastIndex < text.length) {
        parts.push(text.substring(lastIndex));
    }

    return parts;
};

const ChatMessageV2 = ({
    message,
    previousMessage,
    nextMessage,
    getGroupedMessages,
    isTyping,
    threadId,
    isStreamingMessage,
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

    const messageContent = useMemo(() => {
        const content = processMessageText(message.text);
        return Array.isArray(content) ? content : message.text;
    }, [message.text]);

    const agentMode = useMemo(() => getAgentModeDisplayName(mode, intl), [intl, mode]);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: <Image src="./SreAgent.svg" width={28} height={28} alt={intl.formatMessage(SreAgentResources.azureSreAgent)} />,
            loadingState: 'none',
            mode: 'canvas',
            name: (
                <div style={nameAndTimestampContainerStyle}>
                    <span>Azure SRE Agent</span>
                    {mode && <span className={chatStyles.modePill}>{agentMode}</span>}
                    {!isTyping && (
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
    const hideMessageHeader = useMemo(() => shouldGroupWithPreviousMessage(message, previousMessage), [message, previousMessage]);
    // Show feedback buttons if the message is from SREAgent, not typing and it is the last message in the group
    const showFeedbackButtons = useMemo(
        () => message.author.role === 'SREAgent' && !isTyping && !shouldGroupWithPreviousMessage(nextMessage, message),
        [message, nextMessage, isTyping]
    );
    const showCopyMessageButton = useMemo(
        () => message.author.role === 'SREAgent' && !isTyping && message.text && !shouldGroupWithPreviousMessage(nextMessage, message),
        [message, nextMessage, isTyping]
    );

    const filteredMessageContentToCopy = useMemo(() => {
        if (!showCopyMessageButton || !getGroupedMessages) return '';

        const groupedMessages = getGroupedMessages();
        return groupedMessages
            .map(msg =>
                msg.text.trim().replace(imageRegex, '[Image]').replace(mermaidRegex, '[Mermaid Diagram]').replace(chartRegex, '[Chart]')
            )
            .join('\n\n');
    }, [showCopyMessageButton, getGroupedMessages]);

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

    // Helper function to extract title from mermaid content
    const extractMermaidTitle = (content: string): string => {
        const lines = content.trim().split('\n');
        if (lines.length === 0) return 'Diagram';

        const firstLine = lines[0];

        if (firstLine.startsWith('%%')) {
            return firstLine.substring(2).trim();
        }

        if (firstLine.startsWith('title:')) {
            return firstLine.substring(6).trim();
        }

        if (firstLine.length < 50 && !firstLine.includes('->') && !firstLine.includes('--')) {
            return firstLine.trim();
        }

        return 'Diagram';
    };

    // Render specific content types
    const renderContentPart = (part: any, index: number): React.ReactNode => {
        // Plain text markdown
        if (typeof part === 'string') {
            return <ReactMarkdownComponent key={index} content={part} />;
        }

        // Handle different content types
        switch (part.type) {
            case 'image':
                return (
                    <div key={index} style={{ margin: '10px 0' }}>
                        <img src={part.src} alt={part.alt || 'Embedded image'} style={{ maxWidth: '100%', borderRadius: '4px' }} />
                        {part.alt && <div style={{ textAlign: 'center', fontSize: '12px', color: '#666' }}>{part.alt}</div>}
                    </div>
                );

            case 'mermaid':
                return <MermaidChart key={index} chart={part.content} title={extractMermaidTitle(part.content)} />;

            case 'chart-data':
                return <AgentChart key={index} messageText={part.content} />;

            default:
                return null;
        }
    };

    // Main content rendering function
    const RegularMessage = ({ isUserMessage }: { isUserMessage?: boolean }): React.ReactNode => {
        // Special case: if the whole message is an incident alert, render it directly
        if (typeof message.text === 'string') {
            const incidentMatch = message.text.match(incidentAlertRegex);
            if (incidentMatch && incidentMatch[1]) {
                return <IncidentAlert messageText={message.text} />;
            }

            // Special case: Check for investigation-summaries format (multiple summaries in one container)
            const summariesMatch = message.text.match(investigationSummariesRegex);
            if (summariesMatch && summariesMatch[1]) {
                try {
                    const summariesData = JSON.parse(summariesMatch[1].trim());
                    // Always render the panel even if there are no summaries yet
                    if (summariesData) {
                        // Pass the entire message text directly to the panel component
                        return <InvestigationSummaryPanel messageText={message.text} />;
                    }
                } catch (error) {
                    console.error('Failed to parse investigation summaries:', error);
                }
            }

            // Special case: Check for a single investigation-summary block
            const singleMatch = message.text.match(investigationSummaryRegex);
            if (singleMatch) {
                return <InvestigationSummary messageText={message.text} />;
            }
        }

        // Special case 3: if the whole message is a chart, render it directly
        if (
            typeof message.text === 'string' &&
            chartRegex.test(message.text) &&
            message.text.trim().replace(/\s+/g, ' ').match(chartRegex)?.[0].length === message.text.trim().length
        ) {
            return <AgentChart messageText={message.text} />;
        }

        // Normal markdown content
        if (!Array.isArray(messageContent)) {
            return <ReactMarkdownComponent key={message.id} content={messageContent} isUserMessage={isUserMessage} />;
        }

        // Mixed content with special blocks
        return <>{messageContent.map(renderContentPart)}</>;
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
                        {/* For messages with approval - text content may be empty, so we may only need to render approval UI */}
                        {message.approval ? (
                            <ApprovalMessage message={message} threadId={threadId} />
                        ) : message.isDailyReport ? (
                            <DailyReportMessage message={message} />
                        ) : message.azCliExecution ? (
                            <AzCliExecutionMessage execution={message.azCliExecution} threadId={threadId} />
                        ) : message.kubectlExecution ? (
                            <KubectlExecutionMessage execution={message.kubectlExecution} threadId={threadId} />
                        ) : message.text || isTyping ? (
                            <RegularMessage />
                        ) : null}

                        <ToolCallTextComponent
                            toolCallText={message.toolCallText}
                            hasText={!!message.text || !!message.approval || !!message.azCliExecution || !!message.kubectlExecution}
                            isTyping={isTyping}
                        />

                        <div style={{ display: 'flex', flexDirection: 'row', marginTop: '8px' }}>
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
                        <RegularMessage isUserMessage={true} />
                    </UserMessage>
                </div>
            );
    }
};

const ReactMarkdownComponent = ({ content, isUserMessage }: { content?: string | null; isUserMessage?: boolean }) => {
    const aLinkRenderer = useCallback((props: any) => {
        return (
            <a href={props.href} target="_blank" rel="noopener noreferrer">
                {props.children}
            </a>
        );
    }, []);

    const codeRenderer = useCallback((props: any) => {
        // Check if this code element is inside a pre element (code block)
        const isInPre = props.node?.parent?.tagName === 'pre';
        const className = isInPre ? chatMessageStyles.codeBlockInPre : chatMessageStyles.codeBlock;
        return <code className={className}>{props.children}</code>;
    }, []);

    const preRenderer = useCallback((props: any) => {
        return <pre className={chatMessageStyles.preBlock}>{props.children}</pre>;
    }, []);

    return (
        <div className={mergeClasses('markdown-content', isUserMessage ? undefined : chatMessageStyles.regularMessageContent)}>
            <ReactMarkdown
                components={{
                    a: aLinkRenderer,
                    code: codeRenderer,
                    pre: preRenderer,
                }}
                remarkPlugins={[remarkGfm]}
                rehypePlugins={[rehypeRaw]}
            >
                {content}
            </ReactMarkdown>
        </div>
    );
};

const ToolCallTextComponent = ({ toolCallText, hasText, isTyping }: { toolCallText?: string; hasText: boolean; isTyping?: boolean }) => {
    const toolCallTextContent = toolCallText || (hasText ? '' : 'Analyzing...');

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
        toolCallTextContent && (
            <>
                <style>{styles}</style>
                <Text className={chatMessageStyles.toolCallText}>{toolCallTextContent}</Text>
            </>
        )
    );
};

export default memo(ChatMessageV2);
