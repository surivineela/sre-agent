import { FeedbackButtons } from '@fluentui-copilot/react-copilot';
import axios from 'axios';
import { memo, useContext, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import CopyButton from '../../Common/Components/CopyButton';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { isAgentMessage, isChatMessageEmpty, shouldGroupWithPreviousMessage } from '../Activities/Utility';
import { AgentMessageRegex, ChatMessage } from '../Contracts/Activities';
import { ChatBoxContext } from '../Contracts/Context';
import { FeedbackDialog } from './FeedbackDialog';

const MessageFooter = ({
    threadId,
    message,
    nextMessage,
    isTyping,
    isStreamingMessage,
}: {
    threadId: string;
    message: ChatMessage;
    nextMessage?: ChatMessage;
    isTyping?: boolean;
    isStreamingMessage?: boolean;
}) => {
    const [showFeedbackDialog, setShowFeedbackDialog] = useState(false);
    const [selectedFeedback, setSelectedFeedback] = useState<'positive' | 'negative'>();
    const [hasSubmittedFeedback, setHasSubmittedFeedback] = useState(false);

    const { getGroupedChatMessages } = useContext(ChatBoxContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    // Do not use useEffect to calculate groupedMessages, canShowFooter, hasFooterContentToShow, and messagesToCopy because it will
    // compute after the render which might cause incorrect predefined scroll position handled by useLayoutEffect in ChatBoxV2 when the footer is shown after the render.
    // ToDo: upadte useLayoutEffect to handle special situation when footer is shown after the render.
    const groupedMessages = useMemo(
        () => getGroupedChatMessages(message, isStreamingMessage),
        [getGroupedChatMessages, message, isStreamingMessage]
    );

    const canShowFooter = useMemo(
        () => isAgentMessage(message) && !isTyping && !shouldGroupWithPreviousMessage(nextMessage, message),
        [message, nextMessage, isTyping]
    );

    const hasFooterContentToShow = useMemo(() => {
        return groupedMessages.some(msg => !isChatMessageEmpty(msg));
    }, [groupedMessages]);

    const messagesToCopy = useMemo(() => {
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
    }, [groupedMessages]);

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

    return (
        <>
            {canShowFooter && hasFooterContentToShow && (
                <div style={{ display: 'flex', flexDirection: 'row' }}>
                    <FeedbackButtons
                        positiveFeedbackButton={{ onClick: () => handleFeedbackClick(true) }}
                        negativeFeedbackButton={{ onClick: () => handleFeedbackClick(false) }}
                        selected={selectedFeedback}
                        disabled={hasSubmittedFeedback}
                    />
                    {messagesToCopy && <CopyButton textToCopy={messagesToCopy} />}
                </div>
            )}
            <FeedbackDialog
                isOpen={showFeedbackDialog}
                setIsOpen={setShowFeedbackDialog}
                threadId={threadId}
                isMessageFeedback={true}
                clearSelectedFeedback={() => setSelectedFeedback(undefined)}
                setHasSubmittedFeedback={setHasSubmittedFeedback}
            />
        </>
    );
};

export default memo(MessageFooter);
