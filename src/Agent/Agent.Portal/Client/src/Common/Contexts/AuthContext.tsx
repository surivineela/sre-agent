import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { tokenCache } from '../Clients/TokenCache';

interface AuthenticatedUser {
    name: string;
    username: string;
    email: string;
    tenantId: string;
    objectId: string;
}

interface AuthContextValue {
    isAuthenticated: boolean;
    isLoading: boolean;
    user: AuthenticatedUser | null;
    signIn: (returnUrl?: string) => void;
    signOut: () => void;
    switchTenant: (tenantId: string, returnUrl?: string) => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [isAuthenticated, setIsAuthenticated] = useState(false);
    const [isLoading, setIsLoading] = useState(true);
    const [user, setUser] = useState<AuthenticatedUser | null>(null);

    // Check authentication status on mount
    useEffect(() => {
        const checkAuthStatus = async () => {
            try {
                const response = await fetch('/api/auth/user', {
                    credentials: 'include',
                });

                if (response.ok) {
                    const data = await response.json();
                    if (data.isAuthenticated) {
                        // Get tenant ID from ARM token if not provided by the user endpoint
                        let tenantId = data.tenantId || '';

                        if (!tenantId) {
                            try {
                                const token = await tokenCache.getAccessToken('arm');
                                tenantId = token.tenantId ?? tenantId;
                            } catch (error) {
                                console.warn('Failed to extract tenant ID from ARM token:', error);
                            }
                        }

                        setIsAuthenticated(true);
                        setUser({
                            name: data.name || '',
                            username: data.username || '',
                            email: data.email || data.username || '',
                            tenantId,
                            objectId: data.objectId || '',
                        });
                    } else {
                        setIsAuthenticated(false);
                        setUser(null);
                    }
                }
            } catch (error) {
                console.error('Failed to check auth status:', error);
                setIsAuthenticated(false);
                setUser(null);
            } finally {
                setIsLoading(false);
            }
        };

        checkAuthStatus();
    }, []);

    const signIn = useCallback((returnUrl?: string) => {
        const url = returnUrl ? `/api/auth/login?returnUrl=${encodeURIComponent(returnUrl)}` : '/api/auth/login';
        window.location.href = url;
    }, []);

    const signOut = useCallback(() => {
        window.location.href = '/api/auth/logout';
    }, []);

    const switchTenant = useCallback((tenantId: string, returnUrl?: string) => {
        const url = returnUrl
            ? `/api/auth/switch-tenant?tenantId=${encodeURIComponent(tenantId)}&returnUrl=${encodeURIComponent(returnUrl)}`
            : `/api/auth/switch-tenant?tenantId=${encodeURIComponent(tenantId)}`;
        window.location.href = url;
    }, []);

    const value = useMemo<AuthContextValue>(
        () => ({
            isAuthenticated,
            isLoading,
            user,
            signIn,
            signOut,
            switchTenant,
        }),
        [isAuthenticated, isLoading, user, signIn, signOut, switchTenant]
    );

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }

    return context;
};
