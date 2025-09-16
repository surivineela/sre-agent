import { Button } from '@fluentui/react-components';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { MessageBar, MessageBarBody, MessageBarTitle } from '@fluentui/react-message-bar';
import { memo, useEffect } from 'react';
import { FormattedMessage, MessageDescriptor } from 'react-intl';

const SuccessNotification = ({
    show,
    title,
    content,
    actionText,
    onAction,
    onDismiss,
    autoHideDuration = 5000,
}: {
    show?: boolean;
    title?: MessageDescriptor;
    content?: MessageDescriptor;
    actionText?: string;
    onAction?: () => void;
    onDismiss?: () => void;
    autoHideDuration?: number;
}) => {
    useEffect(() => {
        if (show && autoHideDuration > 0 && onDismiss) {
            const timer = setTimeout(onDismiss, autoHideDuration);
            return () => clearTimeout(timer);
        }
    }, [show, autoHideDuration, onDismiss]);

    return (
        show && (
            <MessageBar
                intent={'success'}
                shape={'rounded'}
                layout={'multiline'}
                style={{
                    position: 'fixed',
                    top: '20px',
                    right: '20px',
                    zIndex: 1000,
                    maxWidth: '400px',
                    boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
                }}
            >
                <MessageBarBody>
                    {title && (
                        <MessageBarTitle>
                            <FormattedMessage {...title} />
                        </MessageBarTitle>
                    )}
                    {content && <FormattedMessage {...content} />}
                    <div style={{ display: 'flex', gap: '8px', marginTop: '8px', alignItems: 'center' }}>
                        {actionText && onAction && (
                            <Button appearance="primary" size="small" onClick={onAction}>
                                {actionText}
                            </Button>
                        )}
                        {onDismiss && (
                            <Button
                                appearance="subtle"
                                size="small"
                                icon={<Dismiss12Regular />}
                                onClick={onDismiss}
                                aria-label="Dismiss notification"
                            />
                        )}
                    </div>
                </MessageBarBody>
            </MessageBar>
        )
    );
};

export default memo(SuccessNotification);
