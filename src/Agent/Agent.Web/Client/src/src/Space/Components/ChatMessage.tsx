import {
    CopilotMessageV2 as CopilotMessage,
    CopilotMessageV2Props as CopilotMessageProps,
    UserMessageV2 as UserMessage,
} from '@fluentui-copilot/react-copilot-chat';
import { Button, Text, tokens } from '@fluentui/react-components';
import { SquareDismissRegular } from '@fluentui/react-icons';
import axios from 'axios';
import mermaid from 'mermaid';
import { memo, useCallback, useMemo, useState } from 'react';
import { FormattedMessage, useIntl } from 'react-intl';
import ReactMarkdown from 'react-markdown';
import { ApprovalDecision } from '../../Common/Contracts/Azure/SreAgent';
import { getSafeDateTime } from '../../Common/Helpers/Date';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ActivitiesResources, SreAgentResources } from '../../Strings/SREAgentResources';
import { IChatMessageProps } from '../Contracts/Activities';
import { useAuthenticatedUserInfo } from '../Hooks/useAuthenticatedUserInfo';
import { ChatBoxStyles, useChatBoxStyles } from '../Styles/Activities.styles';
import AgentChart from './Charts';
import MermaidChart from './Mermaid';

// Initialize mermaid with default configuration
// This should be called once when the app loads
mermaid.initialize({
    startOnLoad: false,
    theme: 'neutral',
    flowchart: { useMaxWidth: false },
    securityLevel: 'loose',
});

// Helper function to parse and render markdown with images and mermaid diagrams
const renderMarkdownWithImagesAndMermaid = (text: string) => {
    if (!text) return text;

    // Check for markdown image syntax with base64 data
    const imageRegex = /!\[(.*?)\]\((data:image\/[a-z]+;base64,[A-Za-z0-9+/=]+)\)/g;
    // Check for mermaid code blocks
    const mermaidRegex = /```mermaid\n([\s\S]*?)\n```/g;
    // Check for chart data blocks
    const chartRegex = /```chart-data\n([\s\S]*?)\n```/g;

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

const sendMessageFeedback = async (threadId: string, isPositive: boolean, feedbackText: string) => {
    try {
        const url = `../api/v1/threads/${threadId}/feedbacks`;
        await axios.post(
            url,
            {
                isPositive: isPositive,
                feedbackText: feedbackText,
            },
            {
                headers: getAgentHeaders(),
            }
        );
    } catch {
        // ToDo: handle error
        return undefined;
    }
};

const ChatMessage = ({ message, isTyping, threadId, cancelResponse }: IChatMessageProps) => {
    const chatStyles = useChatBoxStyles();
    const intl = useIntl();
    const [showFeedbackPopup, setShowFeedbackPopup] = useState(false); // State to control popup visibility
    const [feedbackText, setFeedbackText] = useState(''); // State to store feedback text
    const [isPositiveFeedback, setIsPositiveFeedback] = useState<boolean | null>(null); // State to store thumbs-up/down info
    const [approvalStatus, setApprovalStatus] = useState<ApprovalDecision | null>(message.approval ? message.approval.status : null);

    const { userIdAndDisplayName } = useAuthenticatedUserInfo();

    const messageContent = useMemo(() => {
        // Make sure we have a text property and it's not empty
        if (!message.text && !isTyping) {
            return 'No message content to display';
        }
        const content = renderMarkdownWithImagesAndMermaid(message.text);
        return Array.isArray(content) ? content : message.text;
    }, [message.text, isTyping]);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: (
                <svg width="28" height="28" viewBox="0 0 48 48" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g clipPath="url(#clip0_2107_205494)">
                        <path
                            d="M44.8924 13.5312L36.2324 17.8659L25.7814 12.6462C24.6554 12.0839 23.3294 12.0839 22.2034 12.6462L11.7724 17.8559L3.10244 13.5312C2.36244 13.1616 2.00244 12.4525 2.00244 11.7433C2.00244 11.0342 2.36244 10.335 3.10244 9.96546L11.7624 5.6307L22.1924 0.426984C23.3224 -0.142328 24.6624 -0.142328 25.8024 0.426984L36.2324 5.64068L44.8924 9.96546C46.3624 10.7046 46.3624 12.802 44.8924 13.5312Z"
                            fill="url(#paint0_linear_2107_205494)"
                        />
                        <path
                            d="M46.0025 36.2037C46.0025 36.9028 45.6325 37.612 44.8925 37.9815L36.2325 42.3063L25.8125 47.52C24.6725 48.0893 23.3325 48.0893 22.1925 47.52L11.7725 42.3063L3.1025 37.9815C1.6325 37.2424 1.6325 35.1449 3.1025 34.4158L11.7625 30.0811L22.2135 35.3007C23.3395 35.8631 24.6655 35.8631 25.7915 35.3007L36.2225 30.091H36.2325L44.8925 34.4158C45.6325 34.7854 46.0025 35.4945 46.0025 36.2037Z"
                            fill="url(#paint1_linear_2107_205494)"
                        />
                        <path
                            d="M46.0025 36.2037C46.0025 36.9028 45.6325 37.612 44.8925 37.9815L36.2325 42.3063L25.8125 47.52C24.6725 48.0893 23.3325 48.0893 22.1925 47.52L11.7725 42.3063L3.1025 37.9815C1.6325 37.2424 1.6325 35.1449 3.1025 34.4158L11.7625 30.0811L22.2135 35.3007C23.3395 35.8631 24.6655 35.8631 25.7915 35.3007L36.2225 30.091H36.2325L44.8925 34.4158C45.6325 34.7854 46.0025 35.4945 46.0025 36.2037Z"
                            fill="url(#paint2_radial_2107_205494)"
                        />
                        <path
                            d="M36.2224 30.091L25.7914 35.3007C24.6654 35.863 23.3394 35.863 22.2134 35.3007L3.10244 25.7562C2.36244 25.3867 2.00244 24.6775 2.00244 23.9684V11.7432C2.00244 12.4523 2.36244 13.1615 3.10244 13.531L11.7724 17.8558L23.9924 23.9684H24.0024L36.2224 30.091Z"
                            fill="url(#paint3_linear_2107_205494)"
                        />
                        <path
                            d="M46.0025 23.9782V36.2034C46.0025 35.4943 45.6325 34.7851 44.8925 34.4156L36.2325 30.0908H36.2225L24.0025 23.9682H23.9925L11.7725 17.8556L22.2035 12.6459C23.3295 12.0835 24.6555 12.0835 25.7815 12.6459L36.2325 17.8656L44.8925 22.1903C45.6325 22.5599 46.0025 23.269 46.0025 23.9782Z"
                            fill="url(#paint4_linear_2107_205494)"
                        />
                        <path
                            d="M46.0025 23.9782V36.2034C46.0025 35.4943 45.6325 34.7851 44.8925 34.4156L36.2325 30.0908H36.2225L24.0025 23.9682H23.9925L11.7725 17.8556L22.2035 12.6459C23.3295 12.0835 24.6555 12.0835 25.7815 12.6459L36.2325 17.8656L44.8925 22.1903C45.6325 22.5599 46.0025 23.269 46.0025 23.9782Z"
                            fill="url(#paint5_linear_2107_205494)"
                        />
                    </g>
                    <defs>
                        <linearGradient
                            id="paint0_linear_2107_205494"
                            x1="46.0024"
                            y1="11.4337"
                            x2="-4.49756"
                            y2="11.4337"
                            gradientUnits="userSpaceOnUse"
                        >
                            <stop stopColor="#26CFE8" />
                            <stop offset="0.315306" stopColor="#0094F0" />
                            <stop offset="0.612264" stopColor="#2764E7" />
                            <stop offset="0.862742" stopColor="#163697" />
                        </linearGradient>
                        <linearGradient
                            id="paint1_linear_2107_205494"
                            x1="21.0025"
                            y1="56.8787"
                            x2="31.979"
                            y2="26.407"
                            gradientUnits="userSpaceOnUse"
                        >
                            <stop offset="0.185812" stopColor="#EA71EF" />
                            <stop offset="0.507099" stopColor="#8B52F4" />
                            <stop offset="0.796793" stopColor="#5B2AB5" />
                            <stop offset="1" stopColor="#30116E" />
                        </linearGradient>
                        <radialGradient
                            id="paint2_radial_2107_205494"
                            cx="0"
                            cy="0"
                            r="1"
                            gradientUnits="userSpaceOnUse"
                            gradientTransform="translate(25.0025 31.9088) rotate(-90) scale(7.99034 14.8369)"
                        >
                            <stop stopColor="#312A9A" />
                            <stop offset="1" stopColor="#312A9A" stopOpacity="0" />
                        </radialGradient>
                        <linearGradient
                            id="paint3_linear_2107_205494"
                            x1="2.00244"
                            y1="13.7147"
                            x2="30.4825"
                            y2="31.94"
                            gradientUnits="userSpaceOnUse"
                        >
                            <stop stopColor="#6FE8F5" />
                            <stop offset="0.468701" stopColor="#29C3FF" />
                            <stop offset="1" stopColor="#0094F0" />
                        </linearGradient>
                        <linearGradient
                            id="paint4_linear_2107_205494"
                            x1="12.0025"
                            y1="20.9219"
                            x2="50.5025"
                            y2="20.9219"
                            gradientUnits="userSpaceOnUse"
                        >
                            <stop stopColor="#3D35B1" />
                            <stop offset="0.452813" stopColor="#8B52F4" />
                            <stop offset="0.895822" stopColor="#F08AF4" />
                        </linearGradient>
                        <linearGradient
                            id="paint5_linear_2107_205494"
                            x1="22.5025"
                            y1="26.4152"
                            x2="29.9913"
                            y2="16.9179"
                            gradientUnits="userSpaceOnUse"
                        >
                            <stop offset="0.0276412" stopColor="#5B2AB5" />
                            <stop offset="0.808566" stopColor="#8B52F4" stopOpacity="0" />
                        </linearGradient>
                        <clipPath id="clip0_2107_205494">
                            <rect width="48" height="48" fill="white" />
                        </clipPath>
                    </defs>
                </svg>
            ),
            loadingState: isTyping ? 'loading' : 'none',
            mode: 'canvas',
            name: intl.formatMessage(ActivitiesResources.sreAgentDisplayName),
            disclaimer: null,
        };

        return messageProps;
    }, [intl, isTyping]);

    const aLinkRenderer = useCallback((props: any) => {
        return (
            <a href={props.href} target="_blank" rel="noopener noreferrer">
                {props.children}
            </a>
        );
    }, []);

    const handleFeedbackClick = (isPositive: boolean) => {
        setIsPositiveFeedback(isPositive); // Set thumbs-up or thumbs-down
        setShowFeedbackPopup(true); // Show the feedback popup
    };

    const handleFeedbackSubmit = async () => {
        try {
            await sendMessageFeedback(threadId, isPositiveFeedback!, feedbackText); // Send feedback to the API
            console.log(`Feedback sent for message ID: ${message.id}, isPositive: ${isPositiveFeedback}, feedbackText: ${feedbackText}`);
            setShowFeedbackPopup(false); // Hide the popup
            setFeedbackText(''); // Clear the feedback text
        } catch (error) {
            console.error(`Failed to send feedback for message ID: ${message.id}`, error);
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
            return (
                <ReactMarkdown key={index} components={{ a: aLinkRenderer }}>
                    {part}
                </ReactMarkdown>
            );
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
    const renderContent = (): React.ReactNode => {
        // Check if the entire message is just a chart-data block
        const chartRegex = /```chart-data\n([\s\S]*?)\n```/;

        // Special case: if the whole message is a chart, render it directly
        if (
            typeof message.text === 'string' &&
            chartRegex.test(message.text) &&
            message.text.trim().replace(/\s+/g, ' ').match(chartRegex)?.[0].length === message.text.trim().length
        ) {
            return <AgentChart messageText={message.text} />;
        }

        // Normal markdown content
        if (!Array.isArray(messageContent)) {
            return <ReactMarkdown components={{ a: aLinkRenderer }}>{messageContent}</ReactMarkdown>;
        }

        // Mixed content with special blocks
        return <>{messageContent.map(renderContentPart)}</>;
    };

    const sendApprovalDecision = async (threadId: string, approvalId: string, decision: ApprovalDecision) => {
        const url = `../api/v1/approvals/${threadId}/${approvalId}/decision`;

        const response = await axios.post(
            url,
            {
                Status: decision,
                User: userIdAndDisplayName.userId,
            },
            {
                headers: getAgentHeaders(),
            }
        );

        return response.data;
    };

    const handleApprovalDecision = async (approved: boolean) => {
        try {
            if (message.approval) {
                // Check if already approved/rejected
                if (message.approval.status !== ApprovalDecision.Pending) {
                    console.warn(`Approval ${message.approval.id} is already ${message.approval.status}`);
                    return; // Exit early if already decided
                }

                const approvalData = await sendApprovalDecision(
                    threadId,
                    message.approval.id,
                    approved ? ApprovalDecision.Approved : ApprovalDecision.Rejected
                );

                console.log(`Approval decision sent for message ID: ${message.id}, approved: ${approved}`);

                setApprovalStatus(approvalData.status as ApprovalDecision);
                message.approval = {
                    ...message.approval,
                    status: approvalData.status as ApprovalDecision,
                    decisionUser: {
                        displayName: approvalData.decisionMakerName || approvalData.decisionMaker || 'Web Client User',
                        userId: approvalData.decisionMakerId || approvalData.decisionMaker,
                        role: 'User',
                    },
                    decisionTimestamp: approvalData.decisionTimestamp,
                };
            }
        } catch (error: any) {
            console.error(`Failed to send approval decision for message ID: ${message.id}`, error);

            // Handle specific error cases
            if (error.response?.status === 409) {
                // Conflict - already approved/rejected
                const errorData = error.response?.data;

                if (message.approval && errorData) {
                    message.approval = {
                        ...message.approval,
                        status: errorData.status as ApprovalDecision,
                        decisionUser: {
                            displayName: errorData.decisionMakerName || 'Unknown User',
                            userId: errorData.decisionMakerId || '',
                            role: 'User',
                        },
                        decisionTimestamp: errorData.decisionTimestamp,
                    };

                    setApprovalStatus(errorData.status as ApprovalDecision);
                }

                const formattedDate = errorData.decisionTimestamp ? new Date(errorData.decisionTimestamp).toLocaleString() : 'unknown date';
                alert(
                    `This operation was already ${errorData.status?.toLowerCase()} by ${errorData.decisionMakerName || 'Unknown User'} on ${formattedDate}`
                );
            } else {
                alert('Failed to process approval decision. Please try again.');
            }
        }
    };

    const renderApprovalContent = () => {
        if (!message.approval) return null;

        // Use the local state for status to ensure UI updates immediately after user action
        const status = approvalStatus || message.approval.status;
        const { title, description } = message.approval;

        // Log approval information to help with debugging
        console.log('Rendering approval with status:', status, 'and title:', title);

        if (status === ApprovalDecision.Pending) {
            return (
                <div
                    style={{
                        border: '1px solid #ececec',
                        borderRadius: '8px',
                        padding: '16px',
                        marginTop: '16px',
                        backgroundColor: '#f9f9f9',
                    }}
                >
                    <h4 style={{ margin: '0 0 16px 0' }}>{description}</h4>
                    <div style={{ display: 'flex', gap: '8px' }}>
                        <button
                            style={{
                                backgroundColor: '#0078D4',
                                color: 'white',
                                border: 'none',
                                padding: '8px 16px',
                                borderRadius: '4px',
                                cursor: 'pointer',
                                fontWeight: 'bold',
                            }}
                            onClick={() => handleApprovalDecision(true)}
                        >
                            <FormattedMessage {...SreAgentResources.approve} />
                        </button>
                        <button
                            style={{
                                backgroundColor: '#ffffff',
                                color: '#333',
                                border: '1px solid #ccc',
                                padding: '8px 16px',
                                borderRadius: '4px',
                                cursor: 'pointer',
                                fontWeight: 'bold',
                            }}
                            onClick={() => handleApprovalDecision(false)}
                        >
                            <FormattedMessage {...SreAgentResources.deny} />
                        </button>
                    </div>
                    <p
                        style={{
                            fontSize: '11px',
                            color: '#666',
                            marginTop: '16px',
                            marginBottom: '0',
                        }}
                    >
                        <FormattedMessage {...SreAgentResources.approveUsingCreds} />
                    </p>
                </div>
            );
        } else {
            // For Approved or Denied status
            const statusColor = status === ApprovalDecision.Approved ? '#107C10' : '#A4262C';

            return (
                <div
                    style={{
                        border: '1px solid #ececec',
                        borderRadius: '8px',
                        padding: '16px',
                        marginTop: '16px',
                        backgroundColor: '#f9f9f9',
                    }}
                >
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '12px' }}>
                        <h4 style={{ margin: '0', fontWeight: '600', maxWidth: '75%' }}>{description}</h4>
                        <span
                            style={{
                                color: statusColor,
                                fontWeight: 'bold',
                                padding: '4px 12px',
                                borderRadius: '4px',
                                backgroundColor: `${statusColor}15`,
                                display: 'inline-block',
                            }}
                        >
                            {status === ApprovalDecision.Approved ? (
                                <FormattedMessage {...SreAgentResources.approved} />
                            ) : (
                                <FormattedMessage {...SreAgentResources.denied} />
                            )}
                        </span>
                    </div>
                    <p style={{ margin: '0 0 16px 0' }}>
                        {' '}
                        <FormattedMessage {...SreAgentResources.requestedAt} />
                        {': '}
                        {message.approval.createdTimestamp ? new Date(message.approval.createdTimestamp).toLocaleString() : 'N/A'}
                    </p>

                    {message.approval.decisionUser && (
                        <div style={{ fontSize: '14px', color: '#666' }}>
                            <p style={{ margin: '4px 0' }}>
                                <strong>
                                    {status === ApprovalDecision.Approved ? (
                                        <FormattedMessage {...SreAgentResources.approvedBy} />
                                    ) : (
                                        <FormattedMessage {...SreAgentResources.deniedBy} />
                                    )}
                                    :
                                </strong>{' '}
                                {message.approval.decisionUser.displayName}
                            </p>
                            {message.approval.decisionTimestamp && (
                                <p style={{ margin: '4px 0' }}>
                                    <strong>
                                        <FormattedMessage {...SreAgentResources.decisionTime} />:
                                    </strong>{' '}
                                    {new Date(message.approval.decisionTimestamp).toLocaleString()}
                                </p>
                            )}
                        </div>
                    )}

                    {status === ApprovalDecision.Approved && (
                        <p
                            style={{
                                fontSize: '11px',
                                color: '#666',
                                marginTop: '16px',
                                marginBottom: '0',
                            }}
                        >
                            <FormattedMessage {...SreAgentResources.beingExecutedUsingCreds} />
                        </p>
                    )}
                </div>
            );
        }
    };

    switch (message.author.role) {
        case 'SREAgent':
            return (
                <div>
                    <CopilotMessage
                        {...agentMessageProps}
                        key={message.id}
                        style={{ font: 'Segoe UI', lineHeight: '20px', wordBreak: 'unset', maxWidth: '90%' }}
                        className={ChatBoxStyles.agentMessage}
                    >
                        {!isTyping && (
                            <Text block={true} size={200} color={tokens.colorNeutralForeground3}>
                                {getSafeDateTime(message.timeStamp).toLocaleString()}
                            </Text>
                        )}
                        {/* For messages with approval - text content may be empty, so we may only need to render approval UI */}
                        {message.approval ? (
                            <>{renderApprovalContent()}</>
                        ) : (
                            /* For regular messages, just render the content */
                            renderContent()
                        )}

                        {!isTyping && ( // Only show buttons when the agent is not typing
                            <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '8px' }}>
                                <button
                                    className="feedback-button"
                                    style={{
                                        background: 'transparent', // Transparent background
                                        border: 'none', // No border
                                        cursor: 'pointer',
                                        marginRight: '8px',
                                        padding: '4px 8px',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        transition: 'transform 0.1s',
                                    }}
                                    onClick={() => handleFeedbackClick(true)} // Thumbs up
                                >
                                    <svg
                                        width="16"
                                        height="16"
                                        viewBox="0 0 24 24"
                                        fill={isPositiveFeedback === true ? '#0057b8' : 'none'}
                                        stroke={isPositiveFeedback === true ? 'black' : '#cccccc'}
                                        strokeWidth="2"
                                        onMouseOver={e => {
                                            e.currentTarget.setAttribute('stroke', 'black');
                                        }}
                                        onMouseOut={e => {
                                            if (isPositiveFeedback !== true) {
                                                e.currentTarget.setAttribute('stroke', '#cccccc');
                                            }
                                        }}
                                        onMouseDown={e => {
                                            e.currentTarget.setAttribute('fill', '#003d8f');
                                        }}
                                        onMouseUp={e => {
                                            e.currentTarget.setAttribute('fill', isPositiveFeedback === true ? '#0057b8' : 'none');
                                        }}
                                    >
                                        <path d="M14 9V5a3 3 0 0 0-3-3l-4 9v11h11.28a2 2 0 0 0 2-1.7l1.38-9a2 2 0 0 0-2-2.3H14z"></path>
                                        <path d="M7 22H4a2 2 0 0 1-2-2v-7a2 2 0 0 1 2-2h3"></path>
                                    </svg>
                                </button>
                                <button
                                    className="feedback-button"
                                    style={{
                                        background: 'transparent', // Transparent background
                                        border: 'none', // No border
                                        cursor: 'pointer',
                                        padding: '4px 8px',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        transition: 'transform 0.1s',
                                    }}
                                    onClick={() => handleFeedbackClick(false)} // Thumbs down
                                >
                                    <svg
                                        width="16"
                                        height="16"
                                        viewBox="0 0 24 24"
                                        fill={isPositiveFeedback === false ? '#c01c28' : 'none'}
                                        stroke={isPositiveFeedback === false ? 'black' : '#cccccc'}
                                        strokeWidth="2"
                                        onMouseOver={e => {
                                            e.currentTarget.setAttribute('stroke', 'black');
                                        }}
                                        onMouseOut={e => {
                                            if (isPositiveFeedback !== false) {
                                                e.currentTarget.setAttribute('stroke', '#cccccc');
                                            }
                                        }}
                                        onMouseDown={e => {
                                            e.currentTarget.setAttribute('fill', '#a51419');
                                        }}
                                        onMouseUp={e => {
                                            e.currentTarget.setAttribute('fill', isPositiveFeedback === false ? '#c01c28' : 'none');
                                        }}
                                    >
                                        <path d="M10 15v4a3 3 0 0 0 3 3l4-9V2H5.72a2 2 0 0 0-2 1.7l-1.38 9a2 2 0 0 0 2 2.3H10z"></path>
                                        <path d="M17 2h3a2 2 0 0 1 2 2v7a2 2 0 0 1-2 2h-3"></path>
                                    </svg>
                                </button>
                            </div>
                        )}
                        {showFeedbackPopup && (
                            <div
                                style={{
                                    position: 'absolute',
                                    bottom: '10px',
                                    right: '10px',
                                    backgroundColor: 'white',
                                    color: 'black',
                                    padding: '16px',
                                    borderRadius: '8px',
                                    boxShadow: '0px 4px 6px rgba(0, 0, 0, 0.1)',
                                    zIndex: 1000,
                                    width: '300px',
                                }}
                            >
                                <h4>
                                    <FormattedMessage {...SreAgentResources.feedbackDialogTitle} />
                                </h4>
                                <textarea
                                    style={{
                                        width: '100%',
                                        height: '60px',
                                        marginTop: '8px',
                                        padding: '8px',
                                        borderRadius: '4px',
                                        border: '1px solid #ccc',
                                    }}
                                    placeholder={intl.formatMessage(SreAgentResources.enterFeedbackPlaceholder)}
                                    value={feedbackText}
                                    onChange={e => setFeedbackText(e.target.value)}
                                />
                                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '8px' }}>
                                    <button
                                        style={{
                                            backgroundColor: '#0078D4',
                                            color: 'white',
                                            border: 'none',
                                            padding: '8px 12px',
                                            borderRadius: '4px',
                                            cursor: 'pointer',
                                            marginRight: '8px',
                                        }}
                                        onClick={handleFeedbackSubmit}
                                    >
                                        <FormattedMessage {...SreAgentResources.submit} />
                                    </button>
                                    <button
                                        style={{
                                            backgroundColor: '#ccc',
                                            color: 'black',
                                            border: 'none',
                                            padding: '8px 12px',
                                            borderRadius: '4px',
                                            cursor: 'pointer',
                                        }}
                                        onClick={() => setShowFeedbackPopup(false)}
                                    >
                                        <FormattedMessage {...SreAgentResources.cancel} />
                                    </button>
                                </div>
                            </div>
                        )}
                    </CopilotMessage>
                    {isTyping && (
                        <Button
                            icon={<SquareDismissRegular />}
                            onClick={() => cancelResponse?.()}
                            appearance="transparent"
                            style={{ width: '90%' }}
                        >
                            <FormattedMessage {...SreAgentResources.cancel} />
                        </Button>
                    )}
                </div>
            );
        default:
            return (
                <div className={ChatBoxStyles.userMessage} key={message.id}>
                    <Text block={true} weight={'semibold'} className={chatStyles.userName}>
                        {message.author.displayName}
                    </Text>
                    <UserMessage
                        className={chatStyles.userBubble}
                        message={{ className: chatStyles.userBubbleMessage }}
                        key={message.id}
                        timestamp={getSafeDateTime(message.timeStamp).toLocaleString()}
                    >
                        {renderContent()}
                    </UserMessage>
                </div>
            );
    }
};

export default memo(ChatMessage);
