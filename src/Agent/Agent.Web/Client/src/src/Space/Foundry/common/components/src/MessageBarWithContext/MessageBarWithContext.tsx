import { mergeClasses, type MessageBarIntent } from '@fluentui/react-components';
import type { JSX } from 'react';
import { useCallback, useEffect, useState } from 'react';
import { IMessage } from '../../../../packages/core/contexts/message/src/IMessage';
import { MessageType } from '../../../../packages/core/contexts/message/src/MessageType';
import { useMessageContext } from '../../../../packages/core/contexts/message/src/useMessageContext';
import { MessageBar } from '../MessageBar/MessageBar';
import { useMessageBarWithContextStyles } from './MessageBarWithContext.Styles';

// Constants for string literals
const ERROR = 'error';
const WARNING = 'warning';
const INFO = 'info';
const SUCCESS = 'success';

interface IMessageBarWithContextProps {
    /**
     * Additional CSS classes to apply to the component
     */
    className?: string;
    emptyElement?: JSX.Element;
}

/**
 * MessageBarWithContext component displays messages from the message context.
 * This is a wrapper around MessageBar that automatically displays and manages messages
 * from the message context using the useMessageContext hook.
 */
export function MessageBarWithContext({ className, emptyElement }: IMessageBarWithContextProps): JSX.Element | null {
    const styles = useMessageBarWithContextStyles();
    const messageContext = useMessageContext();
    const [contextMessages, setContextMessages] = useState<IMessage[]>([]);

    // Refresh messages from context
    const refreshMessages = useCallback(() => {
        setContextMessages(messageContext.getMessages());
    }, [messageContext]);

    // Set up periodic refresh of messages and listener if available
    useEffect(() => {
        refreshMessages();

        // Use listeners if available for better performance
        messageContext.addListener(refreshMessages);
        return () => {
            messageContext.removeListener(refreshMessages);
            messageContext.dismissAllMessages();
        };
    }, [messageContext, refreshMessages]);

    // Handle message dismissal
    const handleDismiss = useCallback(
        (message: IMessage) => {
            if (!message.unDismissable && message.id) {
                messageContext.dismissMessage(message.id);
                refreshMessages();
            }
        },
        [messageContext, refreshMessages]
    );

    const getIntent = (messageType: IMessage['messageType']): MessageBarIntent => {
        switch (messageType) {
            case MessageType.Error: {
                return ERROR;
            }
            case MessageType.Warning:
            case MessageType.SevereWarning: {
                return WARNING;
            }
            case MessageType.Success: {
                return SUCCESS;
            }
            case MessageType.Info: {
                return INFO;
            }
        }
    };

    // Filter out messages that don't have meaningful content to display
    const validMessages = contextMessages.filter(
        message => Boolean(message.messageComponent) || Boolean(message.message) || Boolean(message.title)
    );

    // If no valid messages, render emptyElement or nothing
    if (validMessages.length === 0) {
        return emptyElement ?? null;
    }

    return (
        <div className={mergeClasses(styles.messageBarContainer, className)}>
            {validMessages.map(message => {
                const hasTitle = message.title.trim().length > 0;
                const messageContent = message.messageComponent ?? message.message;
                return (
                    <MessageBar
                        key={message.id}
                        additionalActions={message.actions}
                        dismissible={!message.unDismissable}
                        intent={getIntent(message.messageType)}
                        message={hasTitle ? messageContent : undefined}
                        onDismiss={
                            message.unDismissable
                                ? undefined
                                : () => {
                                      handleDismiss(message);
                                  }
                        }
                        title={hasTitle ? message.title : messageContent}
                    />
                );
            })}
        </div>
    );
}
