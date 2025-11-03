import { ReactNode } from 'react';
import { AgentWarningContext } from '../../Space/Contracts/Context';
import { useRbacWarning } from '../Hooks/useRbacWarning';
import { useUsageWarning } from '../Hooks/useUsageWarning';

export const AgentWarningProvider = ({ children }: { children?: ReactNode }) => {
    const { showRbacWarning, handleAddAdminClick, handleDismiss: handleDismissRbacWarning, isCheckingRbac } = useRbacWarning();

    const {
        onUsageUpdate,
        reachedLimit,
        approachingLimit,
        showUsageWarning,
        handleDismiss: handleDismissUsageWarning,
        isCheckingUsage,
    } = useUsageWarning();

    return (
        <AgentWarningContext.Provider
            value={{
                showRbacWarning,
                handleAddAdminClick,
                handleDismissRbacWarning,
                isCheckingRbac,
                showUsageWarning,
                approachingLimit,
                reachedLimit,
                handleDismissUsageWarning,
                onUsageUpdate,
                isCheckingUsage,
            }}
        >
            {children}
        </AgentWarningContext.Provider>
    );
};
