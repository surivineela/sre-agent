import type { IMessageService } from './IMessageService';

import { createContext } from 'react';

export const MessageContext = createContext<IMessageService | undefined>(undefined);
