import { FeedbackButtons } from '@fluentui-copilot/react-copilot';
import axios from 'axios';
import { memo, useContext, useState } from 'react';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import CopyButton from '../../Common/Components/CopyButton';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { FeedbackDialog } from './FeedbackDialog';

const MessageFooter = ({
    threadId,
    threadSource,
    messageContent,
    isTyping,
}: {
    threadId: string;
    threadSource?: string;
    messageContent: string;
    isTyping?: boolean;
}) => {
    const [showFeedbackDialog, setShowFeedbackDialog] = useState(false);
    const [selectedFeedback, setSelectedFeedback] = useState<'positive' | 'negative'>();
    const [hasSubmittedFeedback, setHasSubmittedFeedback] = useState(false);

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { logAmplitudeControlEvent } = useAzPortalContext();

    const handleFeedbackClick = async (isPositive: boolean) => {
        setSelectedFeedback(isPositive ? 'positive' : 'negative');

        logAmplitudeControlEvent({
            targetType: 'button',
            targetAction: 'clicked',
            targetName: `thumbs${isPositive ? 'Up' : 'Down'}`,
            targetFriendlyName: `Thumbs ${isPositive ? 'up' : 'down'}`,
            valueObjectName: SpecialControlValue.DoAction,
            valueObjectFriendlyName: SpecialControlValue.DoAction,
            metadata: {
                threadId,
                threadType: threadSource,
            },
        });

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
            {!isTyping && messageContent && (
                <div style={{ display: 'flex', flexDirection: 'row' }}>
                    <FeedbackButtons
                        positiveFeedbackButton={{ onClick: () => handleFeedbackClick(true) }}
                        negativeFeedbackButton={{ onClick: () => handleFeedbackClick(false) }}
                        selected={selectedFeedback}
                        disabled={hasSubmittedFeedback}
                    />
                    <CopyButton textToCopy={messageContent} />
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
