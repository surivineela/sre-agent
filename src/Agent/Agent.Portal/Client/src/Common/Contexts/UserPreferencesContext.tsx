import { ReactNode, createContext, useCallback, useContext, useMemo } from 'react';
import { TelemetrySource } from '../Constants/Telemetry';
import { useLocalStorage } from '../Hooks/useLocalStorage';
import { useSystemTheme } from '../Hooks/useSystemTheme';

export interface UserPreferences {
    /** UI theme - defaults to 'system' */
    theme: 'system' | 'dark' | 'light';

    /** User's locale/language (e.g., 'en', 'en-US') - defaults to browser locale */
    locale: string;

    /** Selected Azure tenant/directory ID */
    tenantId?: string;

    /** Filtered Azure subscription IDs */
    selectedSubscriptions?: string[];

    /** Filtered Azure resource groups */
    selectedResourceGroups?: string[];

    /** Last accessed agent resource ID for quick navigation */
    lastAccessedAgentRscId?: string;
}

interface UserPreferencesContextValue {
    preferences: UserPreferences;
    updatePreference: <K extends keyof UserPreferences>(key: K, value: UserPreferences[K]) => void;
    updatePreferences: (updates: Partial<UserPreferences>) => void;
    theme: UserPreferences['theme'];
    /** The actual resolved theme to use (resolves 'system' to 'dark' or 'light') */
    resolvedTheme: 'dark' | 'light';
    locale: string;
    tenantId?: string;
    selectedSubscriptions?: string[];
    selectedResourceGroups?: string[];
    lastAccessedAgentRscId?: string;
    setTheme: (theme: UserPreferences['theme']) => void;
    setLocale: (locale: string) => void;
    setTenantId: (tenantId: string | undefined) => void;
    setSelectedSubscriptions: (subscriptions: string[] | undefined) => void;
    setSelectedResourceGroups: (resourceGroups: string[] | undefined) => void;
    setLastAccessedAgentRscId: (agentRscId: string | undefined) => void;
}

const STORAGE_KEY = 'sre-agent-portal-preferences';

const defaultPreferences: UserPreferences = {
    theme: 'system',
    locale: typeof navigator !== 'undefined' ? navigator.language || 'en' : 'en',
};

const UserPreferencesContext = createContext<UserPreferencesContextValue | undefined>(undefined);

export const UserPreferencesProvider = ({ children }: { children: ReactNode }) => {
    const { value: preferences, setValue: setPreferences } = useLocalStorage<UserPreferences>(
        STORAGE_KEY,
        defaultPreferences,
        TelemetrySource.PortalLayout
    );
    const systemTheme = useSystemTheme();

    // Resolve 'system' theme to actual dark/light based on system preference
    const resolvedTheme = useMemo<'dark' | 'light'>(() => {
        if (preferences.theme === 'system') {
            return systemTheme;
        }
        return preferences.theme;
    }, [preferences.theme, systemTheme]);

    const updatePreference = useCallback(
        <K extends keyof UserPreferences>(key: K, value: UserPreferences[K]) => {
            setPreferences(prev => ({
                ...prev,
                [key]: value,
            }));
        },
        [setPreferences]
    );

    const updatePreferences = useCallback(
        (updates: Partial<UserPreferences>) => {
            setPreferences(prev => ({
                ...prev,
                ...updates,
            }));
        },
        [setPreferences]
    );

    const setTheme = useCallback((theme: UserPreferences['theme']) => updatePreference('theme', theme), [updatePreference]);

    const setLocale = useCallback((locale: string) => updatePreference('locale', locale), [updatePreference]);

    const setTenantId = useCallback((tenantId: string | undefined) => updatePreference('tenantId', tenantId), [updatePreference]);

    const setSelectedSubscriptions = useCallback(
        (subscriptions: string[] | undefined) => updatePreference('selectedSubscriptions', subscriptions),
        [updatePreference]
    );

    const setSelectedResourceGroups = useCallback(
        (resourceGroups: string[] | undefined) => updatePreference('selectedResourceGroups', resourceGroups),
        [updatePreference]
    );

    const setLastAccessedAgentRscId = useCallback(
        (agentRscId: string | undefined) => updatePreference('lastAccessedAgentRscId', agentRscId),
        [updatePreference]
    );

    const value = useMemo<UserPreferencesContextValue>(
        () => ({
            preferences,
            updatePreference,
            updatePreferences,
            theme: preferences.theme,
            resolvedTheme,
            locale: preferences.locale,
            tenantId: preferences.tenantId,
            selectedSubscriptions: preferences.selectedSubscriptions,
            selectedResourceGroups: preferences.selectedResourceGroups,
            lastAccessedAgentRscId: preferences.lastAccessedAgentRscId,
            setTheme,
            setLocale,
            setTenantId,
            setSelectedSubscriptions,
            setSelectedResourceGroups,
            setLastAccessedAgentRscId,
        }),
        [
            preferences,
            resolvedTheme,
            updatePreference,
            updatePreferences,
            setTheme,
            setLocale,
            setTenantId,
            setSelectedSubscriptions,
            setSelectedResourceGroups,
            setLastAccessedAgentRscId,
        ]
    );

    return <UserPreferencesContext.Provider value={value}>{children}</UserPreferencesContext.Provider>;
};

export const useUserPreferences = () => {
    const context = useContext(UserPreferencesContext);

    if (!context) {
        throw new Error('useUserPreferences must be used within a UserPreferencesProvider');
    }

    return context;
};
