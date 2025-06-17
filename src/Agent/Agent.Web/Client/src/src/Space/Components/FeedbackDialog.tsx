import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Textarea } from '@fluentui/react-components';
import axios from 'axios';
import { useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { FeedbackResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface FeedbackDialogProps {
    isOpen: boolean;
    setIsOpen: (isOpen: boolean) => void;
    threadId: string;
    clearSelectedFeedback?: () => void;
    setHasSubmittedFeedback?: (hasSubmitted: boolean) => void;
}

export const FeedbackDialog = (props: FeedbackDialogProps) => {
    const { isOpen, setIsOpen, threadId, clearSelectedFeedback, setHasSubmittedFeedback } = props;

    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [feedbackText, setFeedbackText] = useState('');
    // const [isOkToContact, setIsOkToContact] = useState(false);

    const sendMessageFeedback = useCallback(
        async (threadId: string, feedbackText: string) => {
            try {
                const url = `${sreAgentEndpoint}/api/v1/threads/${threadId}/feedbacks`;
                await axios.post(
                    url,
                    {
                        isPositive: false,
                        feedbackText: feedbackText,
                    },
                    {
                        headers: getAgentHeaders(),
                    }
                );
                setHasSubmittedFeedback?.(true);
            } catch (error) {
                console.error('Failed to send feedback:', error);
                return undefined;
            }
        },
        [sreAgentEndpoint, setHasSubmittedFeedback]
    );

    const handleFeedbackSubmit = useCallback(async () => {
        await sendMessageFeedback(threadId, feedbackText);

        setIsOpen(false);
        setFeedbackText('');
    }, [threadId, setIsOpen, feedbackText, sendMessageFeedback]);

    const handleUnfinishedClose = useCallback(() => {
        setIsOpen(false);
        clearSelectedFeedback?.();
    }, [setIsOpen, clearSelectedFeedback]);

    return (
        <Dialog open={isOpen} onOpenChange={(_e, data) => (data.open ? setIsOpen(true) : handleUnfinishedClose())}>
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

                        {/* TODO: Awaiting proper text <Text block>{intl.formatMessage(FeedbackResources.feedbackPrivacyStatement)}</Text>*/}
                    </DialogContent>

                    <DialogActions>
                        <Button appearance="primary" onClick={handleFeedbackSubmit}>
                            {intl.formatMessage(SreAgentResources.submit)}
                        </Button>

                        <Button onClick={handleUnfinishedClose}>{intl.formatMessage(SreAgentResources.cancel)}</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
