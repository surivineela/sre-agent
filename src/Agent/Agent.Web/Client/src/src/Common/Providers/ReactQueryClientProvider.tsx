import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

export const ReactQueryClientProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const queryClient = new QueryClient({
        defaultOptions: {
            queries: {
                refetchOnWindowFocus: false,
                refetchOnReconnect: false,
                staleTime: Infinity,
                gcTime: Infinity,
            },
        },
    });

    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
};
