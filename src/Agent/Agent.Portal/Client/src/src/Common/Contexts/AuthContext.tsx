import { createContext, ReactNode, useCallback, useContext, useMemo, useState } from 'react';

type AuthStatus = 'authenticated' | 'unauthenticated' | 'pending';

interface AuthContextValue {
    status: AuthStatus;
    user: { name: string } | null;
    signIn: () => Promise<void>;
    signOut: () => Promise<void>;
}

const dummyUser = { name: 'Nicolas Layne' };

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export const AuthProvider = ({ children }: { children: ReactNode }) => {
    const [status, setStatus] = useState<AuthStatus>('unauthenticated');
    const [user, setUser] = useState<AuthContextValue['user']>(null);

    const signIn = useCallback(async () => {
        setStatus('authenticated');
        setUser(dummyUser);
    }, []);

    const signOut = useCallback(async () => {
        setStatus('unauthenticated');
        setUser(null);
    }, []);

    const value = useMemo<AuthContextValue>(() => ({ status, user, signIn, signOut }), [status, user, signIn, signOut]);

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
};

export const useAuth = () => {
    const context = useContext(AuthContext);

    if (!context) {
        throw new Error('useAuth must be used within an AuthProvider');
    }

    return context;
};
