import { InteractionStatus, RedirectRequest } from '@azure/msal-browser';
import { useAccount, useIsAuthenticated, useMsal } from '@azure/msal-react';
import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo } from 'react';
import { loginRequest } from '../Auth/msalConfig';

interface AuthenticatedUser {
    name?: string;
    username?: string;
    tenantId?: string;
}

interface AuthContextValue {
    isAuthenticated: boolean;
    isLoading: boolean;
    user: AuthenticatedUser | null;
    signIn: (requestOverrides?: Partial<RedirectRequest>) => Promise<void>;
    signOut: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const { instance, inProgress } = useMsal();
    const isAuthenticated = useIsAuthenticated();
    const account = useAccount();

    const isLoadingAuth = useMemo(() => inProgress !== InteractionStatus.None, [inProgress]);

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
        await instance.logoutRedirect({ account: account ?? undefined, logoutHint: account?.idTokenClaims?.login_hint });
    }, [account, instance]);

    const value = useMemo<AuthContextValue>(
        () => ({
            isAuthenticated,
            isLoading: isLoadingAuth,
            user,
            signIn,
            signOut,
        }),
        [isAuthenticated, user, signIn, signOut, isLoadingAuth]
    );

    // If there are accounts but no active account, set one
    useEffect(() => {
        if (inProgress === InteractionStatus.None) {
            const accounts = instance.getAllAccounts();
            if (accounts.length > 0 && !instance.getActiveAccount()) {
                instance.setActiveAccount(accounts[0]);
            }
        }
    }, [inProgress, instance]);

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }

    return context;
};
