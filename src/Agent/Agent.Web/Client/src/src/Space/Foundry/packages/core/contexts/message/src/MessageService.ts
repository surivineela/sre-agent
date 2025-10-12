import { v4 as uuidv4 } from 'uuid';
import type { IMessage } from './IMessage';
import type { IMessageService } from './IMessageService';
import { MessageType } from './MessageType';

export class MessageService implements IMessageService {
    private messages: IMessage[] = [];
    private listeners = new Set<() => void>();

    private notifyListeners(): void {
        // On dev mode, show error if there are no listeners.
        // Note that in some cases this may be a false positive if MessageBarWithContext
        // has not yet mounted when messages are added (e.g., during initial page load).
        if (import.meta.env.DEV && this.listeners.size === 0) {
            console.error(
                `No listeners registered for MessageService. \
        Did you forget to wrap your page in \`BasicPage\` or another page template? \
        (This may be a false positive if your messages were added on initial render.)`
            );
        }

        for (const listener of this.listeners) {
            try {
                listener();
            } catch (error) {
                console.error('Error in message listener:', error);
            }
        }
    }

    getMessages(): IMessage[] {
        return [...this.messages];
    }

    addMessage(message: IMessage): void {
        const messageWithId = {
            ...message,
            id: message.id && message.id.trim() !== '' ? message.id : uuidv4(),
        };
        // Replace any existing message with the same ID
        this.messages = [...this.messages.filter(m => m.id !== messageWithId.id), messageWithId];
        this.notifyListeners();
    }

    addErrorMessage(title: string, message = '', options?: Partial<IMessage>): void {
        this.addMessage({
            messageType: MessageType.Error,
            title,
            message,
            ...options,
        });
    }

    addWarningMessage(title: string, message = '', options?: Partial<IMessage>): void {
        this.addMessage({
            messageType: MessageType.Warning,
            title,
            message,
            ...options,
        });
    }

    addInfoMessage(title: string, message = '', options?: Partial<IMessage>): void {
        this.addMessage({
            messageType: MessageType.Info,
            title,
            message,
            ...options,
        });
    }

    addSuccessMessage(title: string, message = '', options?: Partial<IMessage>): void {
        this.addMessage({
            messageType: MessageType.Success,
            title,
            message,
            ...options,
        });
    }

    dismissMessage(id: string): void {
        const messageToRemove = this.messages.find(m => m.id === id);
        this.messages = this.messages.filter(m => m.id !== id);

        // Call onDismiss callback if it exists
        if (messageToRemove?.onDismiss) {
            try {
                messageToRemove.onDismiss(messageToRemove);
            } catch (error) {
                console.error('Error in onDismiss callback:', error);
            }
        }

        this.notifyListeners();
    }

    dismissAllMessages(): void {
        // Only dismiss non-sticky messages
        const messagesToDismiss = this.messages.filter(m => !m.sticky);
        this.messages = this.messages.filter(m => m.sticky);

        // Call onDismiss callback for dismissed messages
        for (const message of messagesToDismiss) {
            if (message.onDismiss) {
                try {
                    message.onDismiss(message);
                } catch (error) {
                    console.error('Error in onDismiss callback:', error);
                }
            }
        }

        this.notifyListeners();
    }

    addListener(listener: () => void): void {
        this.listeners.add(listener);
    }

    removeListener(listener: () => void): void {
        this.listeners.delete(listener);
    }
}
