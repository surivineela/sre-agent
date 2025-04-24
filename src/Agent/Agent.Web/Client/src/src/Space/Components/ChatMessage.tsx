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
                <svg width="28" height="28" viewBox="0 0 48 48" fill="none" xmlns="http://www.w3.org/2000/svg">
                    <g clip-path="url(#clip0_2107_205494)">
                        <path d="M44.8924 13.5312L36.2324 17.8659L25.7814 12.6462C24.6554 12.0839 23.3294 12.0839 22.2034 12.6462L11.7724 17.8559L3.10244 13.5312C2.36244 13.1616 2.00244 12.4525 2.00244 11.7433C2.00244 11.0342 2.36244 10.335 3.10244 9.96546L11.7624 5.6307L22.1924 0.426984C23.3224 -0.142328 24.6624 -0.142328 25.8024 0.426984L36.2324 5.64068L44.8924 9.96546C46.3624 10.7046 46.3624 12.802 44.8924 13.5312Z" fill="url(#paint0_linear_2107_205494)" />
                        <path d="M46.0025 36.2037C46.0025 36.9028 45.6325 37.612 44.8925 37.9815L36.2325 42.3063L25.8125 47.52C24.6725 48.0893 23.3325 48.0893 22.1925 47.52L11.7725 42.3063L3.1025 37.9815C1.6325 37.2424 1.6325 35.1449 3.1025 34.4158L11.7625 30.0811L22.2135 35.3007C23.3395 35.8631 24.6655 35.8631 25.7915 35.3007L36.2225 30.091H36.2325L44.8925 34.4158C45.6325 34.7854 46.0025 35.4945 46.0025 36.2037Z" fill="url(#paint1_linear_2107_205494)" />
                        <path d="M46.0025 36.2037C46.0025 36.9028 45.6325 37.612 44.8925 37.9815L36.2325 42.3063L25.8125 47.52C24.6725 48.0893 23.3325 48.0893 22.1925 47.52L11.7725 42.3063L3.1025 37.9815C1.6325 37.2424 1.6325 35.1449 3.1025 34.4158L11.7625 30.0811L22.2135 35.3007C23.3395 35.8631 24.6655 35.8631 25.7915 35.3007L36.2225 30.091H36.2325L44.8925 34.4158C45.6325 34.7854 46.0025 35.4945 46.0025 36.2037Z" fill="url(#paint2_radial_2107_205494)" />
                        <path d="M36.2224 30.091L25.7914 35.3007C24.6654 35.863 23.3394 35.863 22.2134 35.3007L3.10244 25.7562C2.36244 25.3867 2.00244 24.6775 2.00244 23.9684V11.7432C2.00244 12.4523 2.36244 13.1615 3.10244 13.531L11.7724 17.8558L23.9924 23.9684H24.0024L36.2224 30.091Z" fill="url(#paint3_linear_2107_205494)" />
                        <path d="M46.0025 23.9782V36.2034C46.0025 35.4943 45.6325 34.7851 44.8925 34.4156L36.2325 30.0908H36.2225L24.0025 23.9682H23.9925L11.7725 17.8556L22.2035 12.6459C23.3295 12.0835 24.6555 12.0835 25.7815 12.6459L36.2325 17.8656L44.8925 22.1903C45.6325 22.5599 46.0025 23.269 46.0025 23.9782Z" fill="url(#paint4_linear_2107_205494)" />
                        <path d="M46.0025 23.9782V36.2034C46.0025 35.4943 45.6325 34.7851 44.8925 34.4156L36.2325 30.0908H36.2225L24.0025 23.9682H23.9925L11.7725 17.8556L22.2035 12.6459C23.3295 12.0835 24.6555 12.0835 25.7815 12.6459L36.2325 17.8656L44.8925 22.1903C45.6325 22.5599 46.0025 23.269 46.0025 23.9782Z" fill="url(#paint5_linear_2107_205494)" />
                    </g>
                    <defs>
                        <linearGradient id="paint0_linear_2107_205494" x1="46.0024" y1="11.4337" x2="-4.49756" y2="11.4337" gradientUnits="userSpaceOnUse">
                            <stop stop-color="#26CFE8" />
                            <stop offset="0.315306" stop-color="#0094F0" />
                            <stop offset="0.612264" stop-color="#2764E7" />
                            <stop offset="0.862742" stop-color="#163697" />
                        </linearGradient>
                        <linearGradient id="paint1_linear_2107_205494" x1="21.0025" y1="56.8787" x2="31.979" y2="26.407" gradientUnits="userSpaceOnUse">
                            <stop offset="0.185812" stop-color="#EA71EF" />
                            <stop offset="0.507099" stop-color="#8B52F4" />
                            <stop offset="0.796793" stop-color="#5B2AB5" />
                            <stop offset="1" stop-color="#30116E" />
                        </linearGradient>
                        <radialGradient id="paint2_radial_2107_205494" cx="0" cy="0" r="1" gradientUnits="userSpaceOnUse" gradientTransform="translate(25.0025 31.9088) rotate(-90) scale(7.99034 14.8369)">
                            <stop stop-color="#312A9A" />
                            <stop offset="1" stop-color="#312A9A" stop-opacity="0" />
                        </radialGradient>
                        <linearGradient id="paint3_linear_2107_205494" x1="2.00244" y1="13.7147" x2="30.4825" y2="31.94" gradientUnits="userSpaceOnUse">
                            <stop stop-color="#6FE8F5" />
                            <stop offset="0.468701" stop-color="#29C3FF" />
                            <stop offset="1" stop-color="#0094F0" />
                        </linearGradient>
                        <linearGradient id="paint4_linear_2107_205494" x1="12.0025" y1="20.9219" x2="50.5025" y2="20.9219" gradientUnits="userSpaceOnUse">
                            <stop stop-color="#3D35B1" />
                            <stop offset="0.452813" stop-color="#8B52F4" />
                            <stop offset="0.895822" stop-color="#F08AF4" />
                        </linearGradient>
                        <linearGradient id="paint5_linear_2107_205494" x1="22.5025" y1="26.4152" x2="29.9913" y2="16.9179" gradientUnits="userSpaceOnUse">
                            <stop offset="0.0276412" stop-color="#5B2AB5" />
                            <stop offset="0.808566" stop-color="#8B52F4" stop-opacity="0" />
                        </linearGradient>
                        <clipPath id="clip0_2107_205494">
                            <rect width="48" height="48" fill="white" />
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
                                <svg width="16" height="16" viewBox="0 0 24 24"
                                    fill={isPositiveFeedback === true ? "#0057b8" : "none"}
                                    stroke={isPositiveFeedback === true ? "black" : "#cccccc"}
                                    strokeWidth="2"
                                    onMouseOver={(e) => {
                                        e.currentTarget.setAttribute('stroke', 'black');
                                    }}
                                    onMouseOut={(e) => {
                                        if (isPositiveFeedback !== true) {
                                            e.currentTarget.setAttribute('stroke', '#cccccc');
                                        }
                                    }}
                                    onMouseDown={(e) => {
                                        e.currentTarget.setAttribute('fill', '#003d8f');
                                    }}
                                    onMouseUp={(e) => {
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
                                <svg width="16" height="16" viewBox="0 0 24 24"
                                    fill={isPositiveFeedback === false ? "#c01c28" : "none"}
                                    stroke={isPositiveFeedback === false ? "black" : "#cccccc"}
                                    strokeWidth="2"
                                    onMouseOver={(e) => {
                                        e.currentTarget.setAttribute('stroke', 'black');
                                    }}
                                    onMouseOut={(e) => {
                                        if (isPositiveFeedback !== false) {
                                            e.currentTarget.setAttribute('stroke', '#cccccc');
                                        }
                                    }}
                                    onMouseDown={(e) => {
                                        e.currentTarget.setAttribute('fill', '#a51419');
                                    }}
                                    onMouseUp={(e) => {
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
