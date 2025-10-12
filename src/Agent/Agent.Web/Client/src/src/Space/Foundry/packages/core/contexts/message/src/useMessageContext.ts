import { useContext } from 'react';
import type { IMessageService } from './IMessageService';
import { MessageContext } from './MessageContext';

/**
 * Hook to access the message service
 */
export function useMessageContext(): IMessageService {
    const context = useContext(MessageContext);
    if (context === undefined) {
        throw new Error('useMessageContext must be used within a MessageProvider');
    }
    return context;
}
