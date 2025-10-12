import { createContext, useContext } from 'react';

export interface ILazyDialogContext {
    isOpen?: boolean;
    onOpenChange?: (isOpen: boolean) => void;
}

export const LazyDialogContext = createContext<ILazyDialogContext>({});

export function useLazyDialogContext(): ILazyDialogContext {
    return useContext(LazyDialogContext);
}
