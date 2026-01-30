import { AuthError, InteractionRequiredAuthError, InteractionStatus, RedirectRequest } from '@azure/msal-browser';
import { useAccount, useIsAuthenticated, useMsal } from '@azure/msal-react';
import { createContext, ReactNode, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { loginRequest, msalInstance } from '../Auth/msalConfig';
import { TelemetrySource } from '../Constants/Telemetry';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from '../Hooks/useTelemetry';
import { SESSION_EXPIRED_EVENT } from '../Utilities/Client';

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
    /** Indicates the session has expired and user needs to re-authenticate */
    isSessionExpired: boolean;
    /** Set session expired state - typically called when token acquisition fails */
    setIsSessionExpired: (expired: boolean) => void;
    signIn: (requestOverrides?: Partial<RedirectRequest>) => void;
    signOut: () => void;
    switchTenant: (tenantId: string) => void;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const { instance, inProgress } = useMsal();
    const isAuthenticated = useIsAuthenticated();
    const account = useAccount();

    const [isSessionExpired, setIsSessionExpired] = useState(false);
    const sessionValidatedRef = useRef(false);

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
            // Clear the current account from cache to ensure clean tenant switch
            // This prevents useAccount() from returning the old tenant's account
            if (account) {
                await msalInstance.clearCache({ account });
            }

            await instance.loginRedirect({
                ...loginRequest,
                prompt: 'none',
                loginHint: account?.username,
                authority: `https://login.microsoftonline.com/${tenantId}`,
            });
        },
        [instance, account]
    );

    // If there are accounts but no active account, set one
    useEffect(() => {
        if (!isLoadingAuth) {
            const accounts = instance.getAllAccounts();
            if (accounts.length > 0 && !instance.getActiveAccount()) {
                instance.setActiveAccount(accounts[0]);
            }
        }
    }, [isLoadingAuth, instance]);

    // Validate session once on load using ssoSilent to check if the Entra session is still valid.
    // This handles the case where MSAL has cached accounts but the refresh token has expired.
    useEffect(() => {
        const validateSession = async () => {
            if (sessionValidatedRef.current || isLoadingAuth) {
                return;
            }

            sessionValidatedRef.current = true;

            const accounts = msalInstance.getAllAccounts();
            if (accounts.length === 0) {
                // No cached accounts - user needs to sign in
                return;
            }

            const activeAccount = msalInstance.getActiveAccount() || accounts[0];
            // Use the account's tenant-specific authority
            const authority = `https://login.microsoftonline.com/${activeAccount.tenantId}`;

            try {
                // Attempt silent token acquisition to validate the session
                // This will use the refresh token if available, or fail if expired
                await msalInstance.acquireTokenSilent({
                    scopes: loginRequest.scopes,
                    account: activeAccount,
                    authority,
                });
                // Session is valid, user can continue
            } catch (error) {
                const errorName = error instanceof Error ? error.constructor.name : typeof error;
                // Use errorCode instead of message to avoid potential PII (e.g., user email in error messages)
                const errorCode = error instanceof AuthError ? error.errorCode : 'unknown';

                logTelemetryEvent({
                    telemetrySource: TelemetrySource.Auth,
                    action: 'validateSession',
                    actionModifier: 'acquireTokenSilent-failed',
                    logLevel: LogLevel.Error,
                    additionalData: { errorName, errorCode },
                });

                if (error instanceof InteractionRequiredAuthError) {
                    // Refresh token expired or revoked - try ssoSilent as a fallback
                    // ssoSilent checks for an active Entra session in a hidden iframe
                    try {
                        await msalInstance.ssoSilent({
                            scopes: loginRequest.scopes,
                            loginHint: activeAccount.username,
                            authority,
                        });
                        // SSO succeeded - user has an active Entra session
                    } catch (ssoError) {
                        const ssoErrorName = ssoError instanceof Error ? ssoError.constructor.name : typeof ssoError;
                        const ssoErrorCode = ssoError instanceof AuthError ? ssoError.errorCode : 'unknown';

                        logTelemetryEvent({
                            telemetrySource: TelemetrySource.Auth,
                            action: 'validateSession',
                            actionModifier: 'ssoSilent-failed',
                            logLevel: LogLevel.Error,
                            additionalData: { errorName: ssoErrorName, errorCode: ssoErrorCode },
                        });

                        // SSO also failed - user needs to re-authenticate interactively
                        // Show the session expired dialog instead of auto-redirecting
                        setIsSessionExpired(true);
                    }
                } else {
                    // Log non-InteractionRequiredAuthError errors for visibility
                    logTelemetryEvent({
                        telemetrySource: TelemetrySource.Auth,
                        action: 'validateSession',
                        actionModifier: 'unexpected-error',
                        logLevel: LogLevel.Error,
                        additionalData: { errorName, errorCode },
                    });
                }
                // For other errors (network, etc.), don't show dialog - let normal flow handle it
            }
        };

        validateSession();
    }, [isLoadingAuth]);

    // Listen for session expired events dispatched from non-React code (e.g., Client.ts)
    useEffect(() => {
        const handleSessionExpired = () => {
            setIsSessionExpired(true);
        };

        window.addEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);

        return () => {
            window.removeEventListener(SESSION_EXPIRED_EVENT, handleSessionExpired);
        };
    }, []);

    const value = useMemo<AuthContextValue>(
        () => ({
            isAuthenticated,
            isLoading: isLoadingAuth,
            user,
            isSessionExpired,
            setIsSessionExpired,
            signIn,
            signOut,
            switchTenant,
        }),
        [isAuthenticated, isLoadingAuth, user, isSessionExpired, signIn, signOut, switchTenant]
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
