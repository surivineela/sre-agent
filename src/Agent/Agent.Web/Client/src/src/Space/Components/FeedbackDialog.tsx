import { Button, Dialog, DialogActions, DialogBody, DialogContent, DialogSurface, DialogTitle, Textarea } from '@fluentui/react-components';
import axios from 'axios';
import { useCallback, useContext, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../Common/Clients/ArmClient';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { FeedbackResources, SreAgentResources } from '../../Strings/SREAgentResources';

interface FeedbackDialogProps {
    isOpen: boolean;
    setIsOpen: (isOpen: boolean) => void;
    threadId?: string;
    isMessageFeedback?: boolean;
    clearSelectedFeedback?: () => void;
    setHasSubmittedFeedback?: (hasSubmitted: boolean) => void;
}

export const FeedbackDialog = (props: FeedbackDialogProps) => {
    const { isOpen, setIsOpen, threadId = '', isMessageFeedback = false, clearSelectedFeedback, setHasSubmittedFeedback } = props;

    const intl = useIntl();
    const { sreAgentEndpoint, resourceId } = useContext(EnvironmentContext);
    const azPortalContext = useContext(AzPortalContext);

    const [feedbackText, setFeedbackText] = useState('');
    // const [isOkToContact, setIsOkToContact] = useState(false);

    const sendGeneralFeedback = useCallback(
        async (feedbackText: string) => {
            azPortalContext.log({
                action: 'send-general-feedback',
                actionModifier: 'sent',
                resourceId,
                data: { feedbackText: feedbackText },
            });
        },
        [azPortalContext, resourceId]
    );

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
                azPortalContext.log({
                    action: 'send-message-feedback',
                    actionModifier: 'failed',
                    resourceId,
                    data: { threadId: threadId, feedbackText: feedbackText, error: getErrorMessage(error) },
                });
                return undefined;
            }
        },
        [sreAgentEndpoint, setHasSubmittedFeedback, azPortalContext, resourceId]
    );

    const handleFeedbackSubmit = useCallback(async () => {
        if (isMessageFeedback) {
            await sendMessageFeedback(threadId, feedbackText);
        } else {
            await sendGeneralFeedback(feedbackText);
        }

        setIsOpen(false);
        setFeedbackText('');
    }, [isMessageFeedback, setIsOpen, sendMessageFeedback, threadId, feedbackText, sendGeneralFeedback]);

    const handleUnfinishedClose = useCallback(() => {
        setIsOpen(false);
        clearSelectedFeedback?.();
    }, [setIsOpen, clearSelectedFeedback]);

    return (
        <Dialog open={isOpen} onOpenChange={(_e, data) => (data.open ? setIsOpen(true) : handleUnfinishedClose())}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle>
                        {isMessageFeedback
                            ? intl.formatMessage(FeedbackResources.provideResponseFeedback)
                            : intl.formatMessage(FeedbackResources.provideAgentFeedback)}
                    </DialogTitle>

                    <DialogContent
                        style={{ display: 'flex', flexDirection: 'column', gap: '12px', marginTop: '12px', marginBottom: '12px' }}
                    >
                        <Textarea
                            placeholder={
                                isMessageFeedback
                                    ? intl.formatMessage(FeedbackResources.threadFeedbackPlaceholder)
                                    : intl.formatMessage(FeedbackResources.generalFeedbackPlaceholder)
                            }
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
                        <Button appearance="primary" onClick={handleFeedbackSubmit} disabled={!isMessageFeedback && !feedbackText}>
                            {intl.formatMessage(SreAgentResources.submit)}
                        </Button>

                        <Button onClick={handleUnfinishedClose}>{intl.formatMessage(SreAgentResources.cancel)}</Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
