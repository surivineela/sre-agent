import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    makeStyles,
    Textarea,
    tokens,
    Tooltip,
} from '@fluentui/react-components';
import { CommentMultiple32Regular, Dismiss24Regular, ThumbDislike24Filled, ThumbLike24Filled } from '@fluentui/react-icons';
import { useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { TelemetrySource } from '../../Common/Constants/Telemetry';
import { useAuth } from '../../Common/Contexts/AuthContext';
import { useNotifications } from '../../Common/Contexts/NotificationContext';
import { useTelemetry } from '../../Common/Hooks/useTelemetry';
import { PortalResources } from '../../Strings/Resources';

const useStyles = makeStyles({
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    sentimentButtons: {
        display: 'flex',
        gap: tokens.spacingHorizontalM,
    },
    textarea: {
        minHeight: '120px',
    },
});

export const FeedbackButton = () => {
    const intl = useIntl();
    const styles = useStyles();
    const { isAuthenticated } = useAuth();
    const { info } = useNotifications();
    const { logEvent } = useTelemetry(TelemetrySource.PortalFeedback, undefined);

    const [open, setOpen] = useState(false);
    const [sentiment, setSentiment] = useState<'positive' | 'negative'>('positive');
    const [message, setMessage] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false);

    const resetForm = useCallback(() => {
        setMessage('');
        setSentiment('positive');
        setOpen(false);
    }, []);

    const handleSubmit = useCallback(() => {
        if (!message.trim()) {
            return;
        }

        setIsSubmitting(true);

        logEvent({
            action: 'submit-feedback',
            actionModifier: sentiment,
            additionalData: {
                sentiment,
                message: message,
            },
        });

        info(intl.formatMessage(PortalResources.feedbackSubmitted));

        resetForm();
        setIsSubmitting(false);
    }, [intl, info, logEvent, message, sentiment, resetForm]);

    return (
        <Dialog open={open} onOpenChange={(_, data) => setOpen(data.open)}>
            <DialogTrigger disableButtonEnhancement>
                <Tooltip content={intl.formatMessage(PortalResources.portalFeedback)} relationship="label">
                    <Button
                        icon={<CommentMultiple32Regular />}
                        appearance="subtle"
                        disabled={!isAuthenticated}
                        aria-label={intl.formatMessage(PortalResources.portalFeedback)}
                    />
                </Tooltip>
            </DialogTrigger>

            <DialogSurface>
                <DialogBody>
                    <DialogTitle
                        action={
                            <DialogTrigger action="close">
                                <Button
                                    appearance="subtle"
                                    aria-label={intl.formatMessage(PortalResources.close)}
                                    icon={<Dismiss24Regular />}
                                />
                            </DialogTrigger>
                        }
                    >
                        {intl.formatMessage(PortalResources.portalFeedback)}
                    </DialogTitle>

                    <DialogContent className={styles.dialogContent}>
                        <div className={styles.sentimentButtons}>
                            <Button
                                icon={<ThumbLike24Filled />}
                                appearance={sentiment === 'positive' ? 'primary' : 'secondary'}
                                onClick={() => setSentiment('positive')}
                            >
                                {intl.formatMessage(PortalResources.positive)}
                            </Button>
                            <Button
                                icon={<ThumbDislike24Filled />}
                                appearance={sentiment === 'negative' ? 'primary' : 'secondary'}
                                onClick={() => setSentiment('negative')}
                            >
                                {intl.formatMessage(PortalResources.negative)}
                            </Button>
                        </div>

                        <Textarea
                            className={styles.textarea}
                            placeholder={intl.formatMessage(PortalResources.feedbackPlaceholder)}
                            value={message}
                            onChange={(_, data) => setMessage(data.value)}
                            resize="vertical"
                        />
                    </DialogContent>

                    <DialogActions>
                        <DialogTrigger disableButtonEnhancement>
                            <Button appearance="secondary" onClick={() => resetForm()}>
                                {intl.formatMessage(PortalResources.cancel)}
                            </Button>
                        </DialogTrigger>
                        <Button appearance="primary" onClick={handleSubmit} disabled={!message.trim() || isSubmitting}>
                            {intl.formatMessage(PortalResources.submit)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
