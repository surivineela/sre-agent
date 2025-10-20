import { InteractionStatus, RedirectRequest } from '@azure/msal-browser';
import { useAccount, useIsAuthenticated, useMsal } from '@azure/msal-react';
import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo } from 'react';
import { loginRequest } from '../Auth/msalConfig';

// TODO: Token acquisition

export type AuthStatus = 'authenticated' | 'unauthenticated' | 'pending';

interface AuthenticatedUser {
    name?: string;
    username?: string;
    tenantId?: string;
}

interface AuthContextValue {
    status: AuthStatus;
    user: AuthenticatedUser | null;
    signIn: (requestOverrides?: Partial<RedirectRequest>) => Promise<void>;
    signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const { instance, accounts, inProgress } = useMsal();
    const activeAccount = instance.getActiveAccount();
    const primaryAccount = activeAccount ?? accounts[0] ?? null;

    useEffect(() => {
        if (primaryAccount && !activeAccount) {
            instance.setActiveAccount(primaryAccount);
        }
    }, [instance, activeAccount, primaryAccount]);

    const account = useAccount(primaryAccount ?? undefined);
    const isAuthenticated = useIsAuthenticated();

    const status: AuthStatus = useMemo(() => {
        if (isAuthenticated) {
            return 'authenticated';
        }

        return inProgress !== InteractionStatus.None ? 'pending' : 'unauthenticated';
    }, [isAuthenticated, inProgress]);

    const user = useMemo<AuthenticatedUser | null>(() => {
        if (!account) {
            return null;
        }

        return {
            name: account.name ?? account.username,
            username: account.username,
            tenantId: account.tenantId,
        };
    }, [account]);

    const signIn = useCallback(
        async (requestOverrides?: Partial<RedirectRequest>) => {
            await instance.loginRedirect({ ...loginRequest, ...requestOverrides });
        },
        [instance]
    );

    const signOut = useCallback(async () => {
        const accountToLogout = instance.getActiveAccount();
        await instance.logoutRedirect({ account: accountToLogout ?? undefined });
    }, [instance]);

    const value = useMemo<AuthContextValue>(
        () => ({
            status,
            user,
            signIn,
            signOut,
        }),
        [status, user, signIn, signOut]
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
