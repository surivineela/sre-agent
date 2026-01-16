import { InteractionStatus, RedirectRequest } from '@azure/msal-browser';
import { useAccount, useIsAuthenticated, useMsal } from '@azure/msal-react';
import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo } from 'react';
import { loginRequest } from '../Auth/msalConfig';

interface AuthenticatedUser {
    name: string;
    username: string;
    email: string;
    tenantId: string;
    /**
     * `homeAccountId`: `{oid}.{tid}` - A unique identifier for the user across tenants
     * `localAccountId`: `{oid}` - guaranteed to be the same as the JWT OID claim (`idTokenClaims.oid`)
     */
    objectId: string;
}

interface AuthContextValue {
    isAuthenticated: boolean;
    isLoading: boolean;
    user: AuthenticatedUser | null;
    signIn: (requestOverrides?: Partial<RedirectRequest>) => void;
    signOut: () => void;
    switchTenant: (tenantId: string) => void;
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

        const idTokenClaims = account.idTokenClaims;
        const email = idTokenClaims?.email && typeof idTokenClaims.email === 'string' ? idTokenClaims.email : '';

        return {
            name: account.name ?? account.username,
            username: account.username,
            email,
            // Tenant/Directory ID
            tenantId: account.tenantId,
            objectId: account.localAccountId || idTokenClaims?.oid || '',
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
    }, [instance, account]);

    const switchTenant = useCallback(
        async (tenantId: string) => {
            await instance.loginRedirect({
                ...loginRequest,
                authority: `https://login.microsoftonline.com/${tenantId}`,
            });
        },
        [instance]
    );

    // If there are accounts but no active account, set one
    useEffect(() => {
        if (!isLoadingAuth) {
            const accounts = instance.getAllAccounts();
            if (accounts.length > 0 && !instance.getActiveAccount()) {
                instance.setActiveAccount(accounts[0]);
            }
        }

        // TODO: There's something weird about what I assume to be expired auth sessions.
        // MSAL treats you as still logged in, but you get 400/401s for everything. Need to
        // see what the best way to check for and handle that is at some point
    }, [isLoadingAuth, instance]);

    const value = useMemo<AuthContextValue>(
        () => ({
            isAuthenticated,
            isLoading: isLoadingAuth,
            user,
            signIn,
            signOut,
            switchTenant,
        }),
        [isAuthenticated, isLoadingAuth, user, signIn, signOut, switchTenant]
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
