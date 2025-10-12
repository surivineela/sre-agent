import type { JSX, PropsWithChildren } from 'react';
import { useMemo } from 'react';
import type { IMessageService } from './IMessageService';
import { MessageContext } from './MessageContext';
import { MessageService } from './MessageService';

type IMessageProviderProps = PropsWithChildren<{
    messageService?: IMessageService;
}>;

export function MessageProvider({ messageService, children }: IMessageProviderProps): JSX.Element {
    const contextValue = useMemo<IMessageService>(() => messageService ?? new MessageService(), [messageService]);

    return <MessageContext.Provider value={contextValue}>{children}</MessageContext.Provider>;
}
