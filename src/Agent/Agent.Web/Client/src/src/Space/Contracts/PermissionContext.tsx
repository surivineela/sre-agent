import React, { createContext, useContext, useMemo } from 'react';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';

export type PermissionContextValue = {
    canWriteThreads: boolean;
    canDeleteThreads: boolean;
    canApproveThreads: boolean;
    loading: boolean;
    error: boolean; // mirrors hook return type
    refresh: () => void;
};

export const PermissionContext = createContext<PermissionContextValue>({
    canWriteThreads: false,
    canDeleteThreads: false,
    canApproveThreads: false,
    loading: true,
    error: false,
    refresh: () => {},
});

export const PermissionProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
    const { canWriteThreads, canDeleteThreads, canApproveThreads, loading, error, refresh } = useUserPermissions();

    const value: PermissionContextValue = useMemo(
        () => ({ canWriteThreads, canDeleteThreads, canApproveThreads, loading, error, refresh }),
        [canWriteThreads, canDeleteThreads, canApproveThreads, loading, error, refresh]
    );

    return <PermissionContext.Provider value={value}>{children}</PermissionContext.Provider>;
};

export const usePermissionContext = () => useContext(PermissionContext);
