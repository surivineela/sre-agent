import { memo, useCallback, useMemo, useState } from 'react';
import { IChatMessageProps } from '../Contracts/Activities';
import {
    CopilotMessageV2 as CopilotMessage,
    CopilotMessageV2Props as CopilotMessageProps,
    UserMessageV2 as UserMessage,
} from '@fluentui-copilot/react-copilot-chat';
import { ChatBoxStyles, useChatBoxStyles } from '../Styles/Activities.styles';
import ReactMarkdown from 'react-markdown';
import { Activities } from '../../Strings/SREResources.resjson';
import { sendMessageFeedback } from '../Hooks/useChatBox'; // Import the function

// Helper function to parse and render markdown with images
const renderMarkdownWithImages = (text: string) => {
    // Check if the text contains markdown image syntax with base64 data
    const imageRegex = /!\[(.*?)\]\((data:image\/[a-z]+;base64,[A-Za-z0-9+/=]+)\)/g;
    if (!imageRegex.test(text)) {
        return text; // No images, return original text
    }

    // split ![](data:image)
    const parts = [];
    let lastIndex = 0;
    let match;
    const regex = /!\[(.*?)\]\((data:image\/[a-z]+;base64,[A-Za-z0-9+/=]+)\)/g;

    while ((match = regex.exec(text)) !== null) {
        if (match.index > lastIndex) {
            parts.push(text.substring(lastIndex, match.index));
        }

        parts.push({
            type: 'image',
            alt: match[1],
            src: match[2]
        });

        lastIndex = regex.lastIndex;
    }

    // Add any remaining text
    if (lastIndex < text.length) {
        parts.push(text.substring(lastIndex));
    }

    return parts;
};

const ChatMessage = ({ message, isTyping, threadId }: IChatMessageProps) => {
    const chatStyles = useChatBoxStyles();
    const [showFeedbackPopup, setShowFeedbackPopup] = useState(false); // State to control popup visibility
    const [feedbackText, setFeedbackText] = useState(''); // State to store feedback text
    const [isPositiveFeedback, setIsPositiveFeedback] = useState<boolean | null>(null); // State to store thumbs-up/down info

    const messageContent = useMemo(() => {
        const content = renderMarkdownWithImages(message.text);
        return Array.isArray(content) ? content : message.text;
    }, [message.text]);

    const agentMessageProps = useMemo(() => {
        const messageProps: CopilotMessageProps = {
            avatar: (
                <svg width="28" height="28" viewBox="0 0 16 16" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g clipPath="url(#clip0_3126_7657)">
                        <path
                            d="M14.48 7.276L14.414 7.243L14.246 6.405C14.447 5.897 14.5 5.322 14.5 4.75C14.5 3.879 14.372 2.981 13.807 2.265C13.228 1.532 12.313 1.141 11.083 1.004C9.877 0.869998 8.821 1.038 8.139 1.769C8.09 1.822 8.043 1.877 8 1.933C7.957 1.877 7.91 1.822 7.861 1.769C7.179 1.038 6.123 0.869998 4.917 1.004C3.687 1.141 2.772 1.532 2.193 2.265C1.628 2.981 1.5 3.879 1.5 4.75C1.5 5.322 1.553 5.897 1.754 6.405L1.586 7.243L1.52 7.276C0.588 7.742 0 8.694 0 9.736V11C0 11.24 0.086 11.438 0.156 11.567C0.231 11.704 0.325 11.828 0.415 11.933C0.595 12.143 0.819 12.346 1.02 12.513C1.225 12.684 1.427 12.835 1.577 12.943C1.816 13.116 2.062 13.275 2.318 13.423C2.625 13.6 3.066 13.832 3.614 14.065C4.705 14.528 6.245 15 8.001 15C9.757 15 11.297 14.528 12.388 14.065C12.936 13.833 13.377 13.6 13.684 13.423C13.94 13.276 14.186 13.116 14.425 12.943C14.574 12.835 14.777 12.684 14.982 12.513C15.183 12.346 15.407 12.143 15.587 11.933C15.677 11.828 15.771 11.704 15.846 11.567C15.916 11.438 16.002 11.24 16.002 11V9.736C16.002 8.694 15.413 7.742 14.482 7.276H14.48ZM3.37 3.195C3.604 2.899 4.063 2.609 5.083 2.495C6.127 2.379 6.571 2.586 6.764 2.793C6.968 3.011 7.123 3.471 7.006 4.407C6.915 5.133 6.704 5.637 6.388 5.959C6.089 6.264 5.604 6.5 4.75 6.5C3.828 6.5 3.47 6.301 3.308 6.12C3.129 5.92 3 5.542 3 4.75C3 3.984 3.123 3.508 3.37 3.195ZM13 12.085L12.935 12.123C12.672 12.275 12.285 12.479 11.801 12.684C10.83 13.096 9.494 13.499 8 13.499C6.506 13.499 5.171 13.096 4.199 12.684C3.716 12.479 3.329 12.274 3.065 12.123L3 12.085V7.824L3.023 7.708C3.513 7.918 4.098 7.999 4.75 7.999C5.896 7.999 6.81 7.671 7.46 7.008C7.679 6.784 7.857 6.534 8 6.265C8.144 6.534 8.321 6.784 8.54 7.008C9.19 7.672 10.103 7.999 11.25 7.999C11.902 7.999 12.487 7.917 12.977 7.708L13 7.824V12.085ZM12.692 6.12C12.53 6.301 12.172 6.5 11.25 6.5C10.396 6.5 9.911 6.264 9.612 5.959C9.297 5.637 9.085 5.133 8.994 4.407C8.877 3.471 9.032 3.011 9.236 2.793C9.429 2.586 9.873 2.379 10.917 2.495C11.937 2.608 12.396 2.899 12.63 3.195C12.877 3.508 13 3.984 13 4.75C13 5.542 12.871 5.921 12.692 6.12ZM7 9.75V11.25C7 11.664 6.664 12 6.25 12C5.836 12 5.5 11.664 5.5 11.25V9.75C5.5 9.336 5.836 9 6.25 9C6.664 9 7 9.336 7 9.75ZM10.5 9.75V11.25C10.5 11.664 10.164 12 9.75 12C9.336 12 9 11.664 9 11.25V9.75C9 9.336 9.336 9 9.75 9C10.164 9 10.5 9.336 10.5 9.75Z"
                            fill="#202020"
                        />
                    </g>
                    <defs>
                        <clipPath id="clip0_3126_7657">
                            <rect width="16" height="16" fill="white" />
                        </clipPath>
                    </defs>
                </svg>
            ),
            loadingState: isTyping ? 'loading' : 'none',
            mode: 'canvas',
            name: Activities.sreAgentDisplayName,
            disclaimer: null,
        };

        return messageProps;
    }, [isTyping]);

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

    // Render content based on type
    // This is a workaround for rendering markdown images.
    // We need to change how we save image content in the db to simplify this approach.
    const renderContent = () => {
        if (!Array.isArray(messageContent)) {
            return <ReactMarkdown components={{ a: aLinkRenderer }}>{messageContent}</ReactMarkdown>;
        }

        return (
            <>
                {messageContent.map((part, index) => {
                    if (typeof part === 'string') {
                        return <ReactMarkdown key={index} components={{ a: aLinkRenderer }}>{part}</ReactMarkdown>;
                    } else if (part.type === 'image') {
                        return (
                            <div key={index} style={{ margin: '10px 0' }}>
                                <img
                                    src={part.src}
                                    alt={part.alt || 'Embedded image'}
                                    style={{ maxWidth: '100%', borderRadius: '4px' }}
                                />
                                {part.alt && <div style={{ textAlign: 'center', fontSize: '12px', color: '#666' }}>{part.alt}</div>}
                            </div>
                        );
                    }
                    return null;
                })}
            </>
        );
    };

    switch (message.author.role) {
        case 'SREAgent':
            return (
                <CopilotMessage
                    {...agentMessageProps}
                    key={message.id}
                    style={{ font: 'Segoe UI', lineHeight: '20px', wordBreak: 'unset', maxWidth: '90%' }}
                    className={ChatBoxStyles.agentMessage}
                >
                    {renderContent()}
                    {!isTyping && ( // Only show buttons when the agent is not typing
                        <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '8px' }}>
                            <button
                                style={{
                                    background: 'none',
                                    border: 'none',
                                    cursor: 'pointer',
                                    marginRight: '8px',
                                    color: '#0078D4',
                                }}
                                onClick={() => handleFeedbackClick(true)} // Thumbs up
                            >
                                👍
                            </button>
                            <button
                                style={{
                                    background: 'none',
                                    border: 'none',
                                    cursor: 'pointer',
                                    color: '#0078D4',
                                }}
                                onClick={() => handleFeedbackClick(false)} // Thumbs down
                            >
                                👎
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
                            <h4>Thank you for your feedback!</h4>
                            <textarea
                                style={{
                                    width: '100%',
                                    height: '60px',
                                    marginTop: '8px',
                                    padding: '8px',
                                    borderRadius: '4px',
                                    border: '1px solid #ccc',
                                }}
                                placeholder="Enter your feedback here..."
                                value={feedbackText}
                                onChange={(e) => setFeedbackText(e.target.value)}
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
                                    Submit
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
                                    Cancel
                                </button>
                            </div>
                        </div>
                    )}
                </CopilotMessage>
            );
        default:
            return (
                <div className={ChatBoxStyles.userMessage} key={message.id}>
                    <UserMessage className={chatStyles.userBubble} key={message.id}>
                        {renderContent()}
                    </UserMessage>
                </div>
            );
    }
};

export default memo(ChatMessage);
