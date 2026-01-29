import { useCallback, useContext, useEffect, useRef, useState } from 'react';
import AzPortalProxy from '../../../Common/AzPortalProxy/AzPortalProxy';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { getConfigSetting, SettingNames } from '../../../Common/Hooks/ConfigSettings';
import { LocalStorageFlags } from '../../../Common/Hooks/useLocalStorage';
import { SreAgentContext } from '../../Contracts/Context';

const inStandaloneMode = AzPortalProxy.inStandaloneMode;

const isOnboardingSkippedForResource = (resourceId: string | undefined): boolean => {
    if (!resourceId) return false;
    try {
        const skippedResources = localStorage.getItem(LocalStorageFlags.OnboardingWizardSkipped);
        if (!skippedResources) return false;
        const skippedSet: string[] = JSON.parse(skippedResources);
        return skippedSet.includes(resourceId.toLowerCase());
    } catch {
        return false;
    }
};

export interface UseOnboardingVisibilityResult {
    showWizard: boolean;
    onComplete: () => void;
}

export const useOnboardingVisibility = (): UseOnboardingVisibilityResult => {
    const azPortalContext = useContext(AzPortalContext);
    const { isCrossTenantPortalMode } = useContext(EnvironmentContext);
    const { agentObj, agentLoaded } = useContext(SreAgentContext);

    const [showWizard, setShowWizard] = useState(false);

    const hasEvaluatedOnboarding = useRef(false);

    useEffect(() => {
        if (inStandaloneMode || isCrossTenantPortalMode) {
            setShowWizard(false);
            return;
        }

        if (!agentLoaded) return;

        if (hasEvaluatedOnboarding.current) {
            return;
        }

        const showOnboardingWizardFlag = getConfigSetting(SettingNames.ShowOnboardingWizard);
        if (!showOnboardingWizardFlag) {
            hasEvaluatedOnboarding.current = true;
            setShowWizard(false);
            return;
        }

        if (isOnboardingSkippedForResource(agentObj?.id)) {
            hasEvaluatedOnboarding.current = true;
            setShowWizard(false);
            return;
        }

        const agent = agentObj?.properties;

        const hasManagedResources = (agent?.knowledgeGraphConfiguration?.managedResources?.length ?? 0) > 0;
        const hasIncidentPlatform =
            agent?.incidentManagementConfiguration?.type !== undefined &&
            agent?.incidentManagementConfiguration?.type !== IncidentManagementType.None;
        const isOnboardingNeeded = !hasManagedResources || !hasIncidentPlatform;

        hasEvaluatedOnboarding.current = true;
        setShowWizard(isOnboardingNeeded);

        if (isOnboardingNeeded) {
            azPortalContext.log({
                action: 'onboarding-wizard',
                actionModifier: 'show',
                logLevel: 'info',
                data: {
                    hasManagedResources,
                    hasIncidentPlatform,
                },
            });
        }
    }, [agentLoaded, agentObj, isCrossTenantPortalMode, azPortalContext]);

    const onComplete = useCallback(() => {
        setShowWizard(false);
    }, []);

    return {
        showWizard,
        onComplete,
    };
};
