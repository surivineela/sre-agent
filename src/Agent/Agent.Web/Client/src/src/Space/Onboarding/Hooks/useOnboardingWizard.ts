import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { StepStatus } from '../../../Common/Components/Wizard/WizardStepper';
import { WizardStepHorizontal } from '../../../Common/Components/Wizard/WizardStepperHorizontal';
import { Agent, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { LocalStorageFlags } from '../../../Common/Hooks/useLocalStorage';
import { OnboardingWizardResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';

/**
 * Enum representing each step of the onboarding wizard.
 * Values are used for ordering, storage, and step navigation.
 */
export enum WizardStep {
    InfrastructureScope = 1,
    IncidentPlatform = 2,
    KnowledgeBase = 3,
    GrantPermissions = 4,
}

const WIZARD_STEPS = [
    WizardStep.InfrastructureScope,
    WizardStep.IncidentPlatform,
    WizardStep.KnowledgeBase,
    WizardStep.GrantPermissions,
] as const;

const FIRST_STEP = WizardStep.InfrastructureScope;
const LAST_STEP = WizardStep.GrantPermissions;

export interface UseOnboardingWizardResult {
    // Steps
    currentStep: WizardStep;
    steps: WizardStepHorizontal[];
    isLastStep: boolean;
    isFirstStep: boolean;

    // Actions
    goToNextStep: () => void;
    goToPreviousStep: () => void;
    skipWizard: () => void;
    finishWizard: () => void;

    // State
    isSaving: boolean;
    setIsSaving: (saving: boolean) => void;
}

const getStoredStep = (): WizardStep => {
    try {
        const stored = localStorage.getItem(LocalStorageFlags.OnboardingWizardCurrentStep);
        if (stored) {
            const step = parseInt(stored, 10) as WizardStep;
            if (!isNaN(step) && step >= FIRST_STEP && step <= LAST_STEP) {
                return step;
            }
        }
    } catch {
        // Ignore localStorage errors
    }
    return WizardStep.InfrastructureScope;
};

const setStoredStep = (step: WizardStep) => {
    try {
        localStorage.setItem(LocalStorageFlags.OnboardingWizardCurrentStep, step.toString());
    } catch {
        // Ignore localStorage errors
    }
};

const setWizardSkippedForResource = (resourceId: string | undefined) => {
    if (!resourceId) return;
    try {
        const skippedResources = localStorage.getItem(LocalStorageFlags.OnboardingWizardSkipped);
        let skippedSet: string[] = [];
        if (skippedResources) {
            try {
                skippedSet = JSON.parse(skippedResources);
            } catch {
                skippedSet = [];
            }
        }
        const normalizedId = resourceId.toLowerCase();
        if (!skippedSet.includes(normalizedId)) {
            skippedSet.push(normalizedId);
        }
        localStorage.setItem(LocalStorageFlags.OnboardingWizardSkipped, JSON.stringify(skippedSet));
        localStorage.removeItem(LocalStorageFlags.OnboardingWizardCurrentStep);
    } catch {
        // Ignore localStorage errors
    }
};

const isStepComplete = (step: WizardStep, agent: Agent | undefined): boolean => {
    if (!agent) return false;

    switch (step) {
        case WizardStep.InfrastructureScope:
            return (agent.knowledgeGraphConfiguration?.managedResources?.length ?? 0) > 0;
        case WizardStep.IncidentPlatform:
            return (
                agent.incidentManagementConfiguration?.type !== undefined &&
                agent.incidentManagementConfiguration?.type !== IncidentManagementType.None
            );
        case WizardStep.KnowledgeBase:
            // Knowledge base step is optional - considered complete if any knowledge sources exist
            return false;
        case WizardStep.GrantPermissions:
            return (agent.permissions?.length ?? 0) > 0;
        default:
            return false;
    }
};

const determineInitialStep = (agent: Agent | undefined): WizardStep => {
    const storedStep = getStoredStep();

    if (storedStep > WizardStep.InfrastructureScope) {
        return storedStep;
    }

    for (const step of WIZARD_STEPS) {
        if (!isStepComplete(step, agent)) {
            return step;
        }
    }

    return WizardStep.InfrastructureScope;
};

/**
 * Hook to manage onboarding wizard step navigation.
 * Visibility is managed by the parent component (SREAgentSpace).
 */
export const useOnboardingWizard = (): UseOnboardingWizardResult => {
    const intl = useIntl();
    const azPortalContext = useContext(AzPortalContext);
    const { agentObj } = useContext(SreAgentContext);

    const [currentStep, setCurrentStep] = useState<WizardStep>(() => determineInitialStep(agentObj?.properties));
    const [isSaving, setIsSaving] = useState(false);

    const steps = useMemo<WizardStepHorizontal[]>(() => {
        const getStepStatus = (step: WizardStep): StepStatus => {
            if (step === currentStep) {
                return StepStatus.Active;
            }
            if (step < currentStep) {
                return StepStatus.Completed;
            }
            return StepStatus.Pending;
        };

        return [
            {
                id: WizardStep.InfrastructureScope,
                title: intl.formatMessage(OnboardingWizardResources.infrastructureScope),
                description: intl.formatMessage(OnboardingWizardResources.infrastructureScopeStepDescription),
                status: getStepStatus(WizardStep.InfrastructureScope),
            },
            {
                id: WizardStep.IncidentPlatform,
                title: intl.formatMessage(OnboardingWizardResources.incidentPlatform),
                description: intl.formatMessage(OnboardingWizardResources.incidentPlatformStepDescription),
                status: getStepStatus(WizardStep.IncidentPlatform),
            },
            {
                id: WizardStep.KnowledgeBase,
                title: intl.formatMessage(OnboardingWizardResources.knowledgeBase),
                description: intl.formatMessage(OnboardingWizardResources.knowledgeBaseStepDescription),
                status: getStepStatus(WizardStep.KnowledgeBase),
            },
            {
                id: WizardStep.GrantPermissions,
                title: intl.formatMessage(OnboardingWizardResources.grantPermissions),
                status: getStepStatus(WizardStep.GrantPermissions),
            },
        ];
    }, [currentStep, intl]);

    const isLastStep = currentStep === LAST_STEP;
    const isFirstStep = currentStep === FIRST_STEP;

    const goToNextStep = useCallback(() => {
        if (currentStep < LAST_STEP) {
            const nextStep = (currentStep + 1) as WizardStep;
            setCurrentStep(nextStep);
            setStoredStep(nextStep);
        }
    }, [currentStep]);

    const goToPreviousStep = useCallback(() => {
        if (currentStep > FIRST_STEP) {
            const prevStep = (currentStep - 1) as WizardStep;
            setCurrentStep(prevStep);
            setStoredStep(prevStep);
        }
    }, [currentStep]);

    const skipWizard = useCallback(() => {
        setWizardSkippedForResource(agentObj?.id);

        azPortalContext.log({
            action: 'onboarding-wizard',
            actionModifier: 'skipped',
            logLevel: 'info',
            data: { atStep: WizardStep[currentStep] },
        });
    }, [currentStep, azPortalContext, agentObj?.id]);

    const finishWizard = useCallback(() => {
        setWizardSkippedForResource(agentObj?.id);

        azPortalContext.log({
            action: 'onboarding-wizard',
            actionModifier: 'finished',
            logLevel: 'info',
            data: { completedSteps: WIZARD_STEPS.length },
        });
    }, [azPortalContext, agentObj?.id]);

    return {
        currentStep,
        steps,
        isLastStep,
        isFirstStep,
        goToNextStep,
        goToPreviousStep,
        skipWizard,
        finishWizard,
        isSaving,
        setIsSaving,
    };
};
