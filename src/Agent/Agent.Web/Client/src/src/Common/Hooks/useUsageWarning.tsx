import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../Clients/ArmClient';
import SreAgentClient from '../Clients/SreAgentClient';
import { MonthlyUsage } from '../Contracts/Azure/SreAgent';

const processUsageResult = (montlyUsage: MonthlyUsage | null | undefined) => {
    if (!montlyUsage) {
        return {
            reachedLimit: false,
            approachingLimit: false,
        };
    }

    const { currentValue, limit } = montlyUsage;

    return {
        reachedLimit: currentValue >= limit,
        approachingLimit: currentValue / limit >= 0.9,
    };
};

export const useUsageWarning = () => {
    const [reachedLimit, setReachedLimit] = useState<boolean>(false);
    const [approachingLimit, setApproachingLimit] = useState<boolean>(false);
    const [isCheckingUsage, setIsCheckingUsage] = useState<boolean>(true);

    const proxy = useContext(AzPortalContext);
    const { resourceId } = useContext(EnvironmentContext);

    const [isDismissed, setIsDismissed] = useState<boolean>(false);

    const onUsageUpdate = useCallback((newUsages: MonthlyUsage | null | undefined) => {
        const { reachedLimit, approachingLimit } = processUsageResult(newUsages);
        setReachedLimit(reachedLimit);
        setApproachingLimit(approachingLimit);
    }, []);

    const handleDismiss = useCallback(() => {
        setIsDismissed(true);
    }, []);

    const showUsageWarning = useMemo(
        () => (reachedLimit || approachingLimit) && !isDismissed,
        [reachedLimit, approachingLimit, isDismissed]
    );

    useEffect(() => {
        if (resourceId) {
            setIsCheckingUsage(true);
            SreAgentClient.getMonthlyUsage(resourceId)
                .then(response => {
                    const result = response.data.value?.[0];

                    const { reachedLimit, approachingLimit } = processUsageResult(result);
                    setReachedLimit(reachedLimit);
                    setApproachingLimit(approachingLimit);
                })
                .catch(error => {
                    proxy.log({
                        action: 'getMonthlyUsage',
                        actionModifier: 'failed',
                        resourceId,
                        logLevel: 'error',
                        data: {
                            error: getErrorMessage(error),
                        },
                    });
                })
                .finally(() => {
                    setIsCheckingUsage(false);
                });
        }
    }, [resourceId]);

    return {
        onUsageUpdate,
        reachedLimit,
        approachingLimit,
        showUsageWarning,
        handleDismiss,
        isCheckingUsage,
    };
};
