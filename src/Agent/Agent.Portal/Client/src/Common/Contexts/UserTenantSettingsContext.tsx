import { ReactNode, createContext, useCallback, useContext, useMemo } from 'react';
import { TelemetrySource } from '../Constants/Telemetry';
import { useLocalStorage } from '../Hooks/useLocalStorage';
import { useAuth } from './AuthContext';

export interface UserTenantSettings {
    /** Last accessed agent resource ID for quick navigation */
    lastAccessedAgentRscId?: string;
}

interface UserTenantSettingsContextValue {
    settings: UserTenantSettings;
    updateSetting: <K extends keyof UserTenantSettings>(key: K, value: UserTenantSettings[K]) => void;
    lastAccessedAgentRscId?: string;
    setLastAccessedAgentRscId: (agentRscId: string | undefined) => void;
}

const STORAGE_KEY_PREFIX = 'sre-agent-portal-tenant-settings';

const defaultSettings: UserTenantSettings = {};

const UserTenantSettingsContext = createContext<UserTenantSettingsContextValue | undefined>(undefined);

export const UserTenantSettingsProvider = ({ children }: { children: ReactNode }) => {
    const { user } = useAuth();

    // Tenant-scoped storage key
    const storageKey = useMemo(() => `${STORAGE_KEY_PREFIX}-${user?.tenantId || 'default'}`, [user?.tenantId]);

    const { value: settings, setValue: setSettings } = useLocalStorage<UserTenantSettings>(
        storageKey,
        defaultSettings,
        TelemetrySource.PortalLayout
    );

    const updateSetting = useCallback(
        <K extends keyof UserTenantSettings>(key: K, value: UserTenantSettings[K]) => {
            setSettings(prev => ({
                ...prev,
                [key]: value,
            }));
        },
        [setSettings]
    );

    const setLastAccessedAgentRscId = useCallback(
        (agentRscId: string | undefined) => updateSetting('lastAccessedAgentRscId', agentRscId),
        [updateSetting]
    );

    const value = useMemo<UserTenantSettingsContextValue>(
        () => ({
            settings,
            updateSetting,
            lastAccessedAgentRscId: settings.lastAccessedAgentRscId,
            setLastAccessedAgentRscId,
        }),
        [settings, updateSetting, setLastAccessedAgentRscId]
    );

    return <UserTenantSettingsContext.Provider value={value}>{children}</UserTenantSettingsContext.Provider>;
};

export const useUserTenantSettings = () => {
    const context = useContext(UserTenantSettingsContext);

    if (!context) {
        throw new Error('useUserTenantSettings must be used within a UserTenantSettingsProvider');
    }

    return context;
};
