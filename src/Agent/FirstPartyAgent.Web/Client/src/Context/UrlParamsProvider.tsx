import React, { ReactNode, createContext, useContext } from 'react';
import useUrlParams from '../Hooks/useUrlParams'; // Your existing hook

// Define the shape of the context data
type UrlParamsContextType = Record<string, string>;

// Create the context with a default undefined value
const UrlParamsContext = createContext<UrlParamsContextType | undefined>(undefined);

// Create the Provider component
export const UrlParamsProvider: React.FC<{ children: ReactNode }> = ({ children }) => {
    const params = useUrlParams(); // The hook is called once here
    return (
        <UrlParamsContext.Provider value={params}>
            {children}
        </UrlParamsContext.Provider>
    );
};

// Create a custom hook to consume the context easily
export const useSharedUrlParams = (): UrlParamsContextType => {
    const context = useContext(UrlParamsContext);
    if (context === undefined) {
        throw new Error('useSharedUrlParams must be used within a UrlParamsProvider');
    }
    return context;
};
