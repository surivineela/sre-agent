import { useCallback, useContext, useEffect, useState } from 'react';
import { AzPortalContext } from '../AzPortalProxy/Providers/AzPortalProxyContext';

export enum LocalStorageFlags {
    IncidentManagementPopoverDismissed = 'sreagent-incident-management-popover-dismissed',
    OnboardingWizardSkipped = 'sreagent-onboarding-wizard-skipped',
    OnboardingWizardCurrentStep = 'sreagent-onboarding-wizard-current-step',
}

export const useLocalStorage = (flag: LocalStorageFlags) => {
    const azPortalContext = useContext(AzPortalContext);
    const [value, setValue] = useState<string | null>(null);

    const getItem = useCallback(() => {
        let item: string | null = null;
        try {
            azPortalContext.log({
                action: 'get-localStorage-item',
                actionModifier: 'start',
                logLevel: 'info',
                data: { flag },
            });
            item = localStorage.getItem(flag);
            azPortalContext.log({
                action: 'get-localStorage-item',
                actionModifier: 'success',
                logLevel: 'info',
                data: { flag },
            });
        } catch (error) {
            azPortalContext.log({
                action: 'get-localStorage-item',
                actionModifier: 'failed',
                logLevel: 'error',
                data: { flag },
            });
        }
        setValue(item);
        return item;
    }, [flag]);

    const setItem = useCallback(
        (newValue: string) => {
            setValue(newValue);
            try {
                azPortalContext.log({
                    action: 'set-localStorage-item',
                    actionModifier: 'start',
                    logLevel: 'info',
                    data: { flag },
                });
                localStorage.setItem(flag, newValue);
                azPortalContext.log({
                    action: 'set-localStorage-item',
                    actionModifier: 'success',
                    logLevel: 'info',
                    data: { flag },
                });
            } catch (error) {
                azPortalContext.log({
                    action: 'set-localStorage-item',
                    actionModifier: 'failed',
                    logLevel: 'error',
                    data: { flag },
                });
            }
        },
        [flag]
    );

    const removeItem = useCallback(() => {
        try {
            azPortalContext.log({
                action: 'remove-localStorage-item',
                actionModifier: 'start',
                logLevel: 'info',
                data: { flag },
            });
            localStorage.removeItem(flag);
            setValue(null);
            azPortalContext.log({
                action: 'remove-localStorage-item',
                actionModifier: 'success',
                logLevel: 'info',
                data: { flag },
            });
        } catch (error) {
            azPortalContext.log({
                action: 'remove-localStorage-item',
                actionModifier: 'failed',
                logLevel: 'error',
                data: { flag },
            });
        }
    }, [flag]);

    useEffect(() => {
        getItem();
    }, [getItem]);

    return {
        item: value,
        setItem,
        removeItem,
    };
};
