import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Text,
    Textarea,
} from '@fluentui/react-components';
import axios from 'axios';
import { useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { FeedbackResources, SreAgentResources } from '../../Strings/SREAgentResources';

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
    } catch (error) {
        // ToDo: handle error
        console.error('Failed to send feedback:', error);
        return undefined;
    }
};

interface FeedbackDialogProps {
    isOpen: boolean;
    setIsOpen: (isOpen: boolean) => void;
    threadId: string;
    isPositiveFeedback: boolean;
}

export const FeedbackDialog = (props: FeedbackDialogProps) => {
    const { isOpen, setIsOpen, threadId, isPositiveFeedback } = props;

    const intl = useIntl();

    const [feedbackText, setFeedbackText] = useState('');
    // const [isOkToContact, setIsOkToContact] = useState(false);

    const handleFeedbackSubmit = useCallback(async () => {
        await sendMessageFeedback(threadId, isPositiveFeedback!, feedbackText);

        setIsOpen(false);
        setFeedbackText('');
    }, [threadId, isPositiveFeedback, setIsOpen, feedbackText]);

    return (
        <Dialog open={isOpen} onOpenChange={(_e, data) => setIsOpen(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>{intl.formatMessage(FeedbackResources.submitFeedbackTitle)}</DialogTitle>

                    <DialogContent
                        style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px', marginBottom: '12px' }}
                    >
                        <Textarea
                            placeholder={intl.formatMessage(FeedbackResources.feedbackPlaceholder)}
                            value={feedbackText}
                            onChange={(_e, data) => setFeedbackText(data.value)}
                            style={{ width: '100%' }}
                        />

                        {/* TODO: Backend support for "ok to contact" property
                        <Checkbox
                            label={intl.formatMessage(FeedbackResources.feedbackContactMe)}
                            checked={isOkToContact}
                            onChange={(_e, data) => setIsOkToContact(!!data.checked)}
                        />
                        */}

                        <Text block>{intl.formatMessage(FeedbackResources.feedbackPrivacyStatement)}</Text>
                    </DialogContent>

                    <DialogActions>
                        <Button appearance="primary" onClick={handleFeedbackSubmit}>
                            {intl.formatMessage(SreAgentResources.submit)}
                        </Button>

                        <Button onClick={() => setIsOpen(false)}>{intl.formatMessage(SreAgentResources.cancel)}</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
