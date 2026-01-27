import { Button, Text } from '@fluentui/react-components';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import isEqual from 'lodash/isEqual';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import RocketImage from '../../../assets/Rocket.svg';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { WizardStepper } from '../../Common/Components/Wizard/WizardStepper';
import { AgentAccessLevel, IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import { OnboardingWizardResources } from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { useOnboardingWizard, WizardStep } from './Hooks/useOnboardingWizard';
import { useOnboardingWizardStyles } from './OnboardingWizard.styles';
import { GrantPermissionsStep } from './Steps/GrantPermissionsStep';
import { IncidentPlatformStep } from './Steps/IncidentPlatformStep';
import { InfrastructureScopeStep } from './Steps/InfrastructureScopeStep';
import { KnowledgeBaseStep } from './Steps/KnowledgeBaseStep';

const isStepDirty = (step: WizardStep, currentValues: WizardFormValues, initialValues: WizardFormValues): boolean => {
    switch (step) {
        case WizardStep.InfrastructureScope:
            return (
                !isEqual([...currentValues.selectedSubscriptionIds].sort(), [...initialValues.selectedSubscriptionIds].sort()) ||
                !isEqual([...currentValues.selectedResourceGroupIds].sort(), [...initialValues.selectedResourceGroupIds].sort())
            );
        case WizardStep.IncidentPlatform:
            return (
                currentValues.incidentPlatformType !== initialValues.incidentPlatformType ||
                currentValues.pagerDutyApiKey !== initialValues.pagerDutyApiKey ||
                currentValues.serviceNowEndpoint !== initialValues.serviceNowEndpoint ||
                currentValues.serviceNowUsername !== initialValues.serviceNowUsername ||
                currentValues.serviceNowPassword !== initialValues.serviceNowPassword
            );
        case WizardStep.KnowledgeBase:
            return !isEqual(currentValues.knowledgeSources, initialValues.knowledgeSources);
        case WizardStep.GrantPermissions:
            return false;
        default:
            return false;
    }
};

export type KnowledgeSourceType = 'repository' | 'file' | 'webpage';

export interface KnowledgeSource {
    id: string;
    type: KnowledgeSourceType;
    name: string;
    url?: string;
    lastModified?: string;
}

export interface WizardFormValues {
    selectedSubscriptionIds: string[];
    selectedResourceGroupIds: string[];
    resourceGroupLocations: Record<string, string>;
    incidentPlatformType: IncidentManagementType | undefined;
    pagerDutyApiKey: string;
    serviceNowEndpoint: string;
    serviceNowUsername: string;
    serviceNowPassword: string;
    permissionsLevel: AgentAccessLevel;
    knowledgeSources: KnowledgeSource[];
}

export interface OnboardingWizardProps {
    onComplete?: () => void;
}

export const OnboardingWizard: FC<OnboardingWizardProps> = ({ onComplete }) => {
    const { resourceId } = useContext(EnvironmentContext);
    const { agentObj } = useContext(SreAgentContext);

    const initialValues = useMemo<WizardFormValues>(() => {
        const agent = agentObj?.properties;
        const existingManagedResources = agent?.knowledgeGraphConfiguration?.managedResources ?? [];
        const existingIncidentConfig = agent?.incidentManagementConfiguration;
        const existingAccessLevel = agent?.actionConfiguration?.accessLevel ?? AgentAccessLevel.low;

        const existingSubscriptionIds = existingManagedResources
            .filter((r: string) => !r.includes('/resourceGroups/'))
            .map((r: string) => {
                const match = r.match(/\/subscriptions\/([^/]+)/i);
                return match ? match[1] : '';
            })
            .filter((id: string) => id.length > 0);

        const existingResourceGroupIds = existingManagedResources.filter((r: string) => r.includes('/resourceGroups/'));

        const descriptor = new ArmResourceDescriptor(resourceId);
        const currentSubscriptionId = descriptor.subscription;

        return {
            selectedSubscriptionIds: existingSubscriptionIds.length > 0 ? existingSubscriptionIds : [currentSubscriptionId],
            selectedResourceGroupIds: existingResourceGroupIds,
            resourceGroupLocations: {},
            incidentPlatformType: existingIncidentConfig?.type,
            pagerDutyApiKey: '',
            serviceNowEndpoint: existingIncidentConfig?.connectionUrl ?? '',
            serviceNowUsername: '',
            serviceNowPassword: '',
            permissionsLevel: existingAccessLevel,
            knowledgeSources: [],
        };
    }, [agentObj, resourceId]);

    const handleSubmit = useCallback((_values: WizardFormValues, _helpers: FormikHelpers<WizardFormValues>) => {}, []);

    return (
        <Formik<WizardFormValues> initialValues={initialValues} onSubmit={handleSubmit}>
            <OnboardingWizardContent onComplete={onComplete} />
        </Formik>
    );
};

interface OnboardingWizardContentProps {
    onComplete?: () => void;
}

const OnboardingWizardContent: FC<OnboardingWizardContentProps> = ({ onComplete }) => {
    const intl = useIntl();
    const styles = useOnboardingWizardStyles();
    const azPortalContext = useAzPortalContext();
    const { agentObj, patchAgent } = useContext(SreAgentContext);

    const { values, initialValues } = useFormikContext<WizardFormValues>();

    const { currentStep, steps, isLastStep, isFirstStep, goToNextStep, goToPreviousStep, skipWizard, finishWizard } = useOnboardingWizard();

    const saveInfrastructureScope = useCallback(async (): Promise<boolean> => {
        // Build managedResources from both subscription IDs and resource group IDs
        const subscriptionResources = values.selectedSubscriptionIds.map(id => `/subscriptions/${id}`);
        const managedResources = [...subscriptionResources, ...values.selectedResourceGroupIds];

        const response = await patchAgent({
            properties: {
                knowledgeGraphConfiguration: {
                    ...agentObj?.properties?.knowledgeGraphConfiguration,
                    managedResources,
                },
            },
        });

        return response.metadata.success;
    }, [values.selectedSubscriptionIds, values.selectedResourceGroupIds, patchAgent, agentObj]);

    const saveIncidentPlatform = useCallback(async (): Promise<boolean> => {
        if (!values.incidentPlatformType) return false;

        let config = null;
        if (values.incidentPlatformType !== IncidentManagementType.None) {
            config = {
                type: values.incidentPlatformType,
                connectionName: values.incidentPlatformType.toLowerCase(),
                ...(values.incidentPlatformType === IncidentManagementType.PagerDuty && {
                    connectionKey: values.pagerDutyApiKey,
                }),
                ...(values.incidentPlatformType === IncidentManagementType.ServiceNow && {
                    connectionUrl: values.serviceNowEndpoint,
                    connectionKey: JSON.stringify({
                        username: values.serviceNowUsername,
                        password: values.serviceNowPassword,
                    }),
                }),
            };
        }

        const response = await patchAgent({
            properties: {
                incidentManagementConfiguration: config,
            },
        });

        return response.metadata.success;
    }, [
        values.incidentPlatformType,
        values.pagerDutyApiKey,
        values.serviceNowEndpoint,
        values.serviceNowUsername,
        values.serviceNowPassword,
        patchAgent,
    ]);

    const saveKnowledgeBase = useCallback(async (): Promise<boolean> => {
        // Knowledge sources are saved individually via their dialogs
        // This function serves as a placeholder for any batch save logic
        return true;
    }, []);

    const savePermissions = useCallback(async (): Promise<boolean> => {
        return true;
    }, []);

    const isCurrentStepValid = useMemo(() => {
        switch (currentStep) {
            case WizardStep.InfrastructureScope:
                // At least one subscription or resource group must be selected
                return values.selectedSubscriptionIds.length > 0 || values.selectedResourceGroupIds.length > 0;
            case WizardStep.IncidentPlatform:
                if (!values.incidentPlatformType) return false;
                switch (values.incidentPlatformType) {
                    case IncidentManagementType.None:
                    case IncidentManagementType.AzMonitor:
                    case IncidentManagementType.Icm:
                        return true;
                    case IncidentManagementType.PagerDuty:
                        return values.pagerDutyApiKey.trim().length > 0;
                    case IncidentManagementType.ServiceNow:
                        return (
                            values.serviceNowEndpoint.trim().length > 0 &&
                            values.serviceNowUsername.trim().length > 0 &&
                            values.serviceNowPassword.trim().length > 0
                        );
                    default:
                        return false;
                }
            case WizardStep.KnowledgeBase:
                // Knowledge base step is always valid (optional step)
                return true;
            case WizardStep.GrantPermissions:
                return true;
            default:
                return false;
        }
    }, [currentStep, values]);

    const handleSaveAndNext = useCallback(() => {
        const stepHasChanges = isStepDirty(currentStep, values, initialValues);
        const saveCurrentStep = async () => {
            try {
                let saveSuccess = true;

                switch (currentStep) {
                    case WizardStep.InfrastructureScope:
                        saveSuccess = await saveInfrastructureScope();
                        break;
                    case WizardStep.IncidentPlatform:
                        saveSuccess = await saveIncidentPlatform();
                        break;
                    case WizardStep.KnowledgeBase:
                        saveSuccess = await saveKnowledgeBase();
                        break;
                    case WizardStep.GrantPermissions:
                        saveSuccess = await savePermissions();
                        break;
                }

                if (!saveSuccess) {
                    azPortalContext.log({
                        action: 'onboarding-wizard',
                        actionModifier: 'save-failed',
                        logLevel: 'error',
                        data: { step: WizardStep[currentStep] },
                    });
                }
            } catch (error) {
                azPortalContext.log({
                    action: 'onboarding-wizard',
                    actionModifier: 'save-error',
                    logLevel: 'error',
                    data: { step: WizardStep[currentStep], error: String(error) },
                });
            }
        };

        if (stepHasChanges) {
            saveCurrentStep();
        } else {
            azPortalContext.log({
                action: 'onboarding-wizard',
                actionModifier: 'skip-save',
                logLevel: 'info',
                data: { step: WizardStep[currentStep], reason: 'no-changes' },
            });
        }

        if (isLastStep) {
            finishWizard();
            onComplete?.();
        } else {
            goToNextStep();
        }
    }, [
        currentStep,
        isLastStep,
        goToNextStep,
        finishWizard,
        onComplete,
        azPortalContext,
        saveInfrastructureScope,
        saveIncidentPlatform,
        saveKnowledgeBase,
        savePermissions,
        values,
        initialValues,
    ]);

    const handleSkip = useCallback(() => {
        skipWizard();
        onComplete?.();
    }, [skipWizard, onComplete]);

    const handleBack = useCallback(() => {
        goToPreviousStep();
    }, [goToPreviousStep]);

    const getStepClassName = useCallback(
        (step: WizardStep) => (currentStep === step ? styles.stepVisible : styles.stepHidden),
        [currentStep, styles.stepVisible, styles.stepHidden]
    );

    return (
        <div className={styles.fullPageContainer}>
            <div className={styles.header}>
                <img src={RocketImage} alt="" className={styles.rocketIcon} aria-hidden="true" />
                <Text className={styles.welcomeTitle}>{intl.formatMessage(OnboardingWizardResources.welcomeTitle)}</Text>
                <Text className={styles.welcomeSubtitle}>{intl.formatMessage(OnboardingWizardResources.welcomeSubtitle)}</Text>
            </div>

            <div className={styles.cardContainer}>
                <div className={styles.wizardCard}>
                    <div className={styles.contentContainer}>
                        <div className={styles.stepperPanel}>
                            <WizardStepper steps={steps} />
                        </div>

                        <div className={styles.mainContent}>
                            <div className={getStepClassName(WizardStep.InfrastructureScope)}>
                                <InfrastructureScopeStep />
                            </div>
                            <div className={getStepClassName(WizardStep.IncidentPlatform)}>
                                <IncidentPlatformStep />
                            </div>
                            <div className={getStepClassName(WizardStep.KnowledgeBase)}>
                                <KnowledgeBaseStep />
                            </div>
                            <div className={getStepClassName(WizardStep.GrantPermissions)}>
                                <GrantPermissionsStep />
                            </div>
                        </div>
                    </div>

                    <div className={styles.footer}>
                        {!isFirstStep && (
                            <Button appearance="secondary" onClick={handleBack}>
                                {intl.formatMessage(OnboardingWizardResources.back)}
                            </Button>
                        )}
                        <div className={styles.footerSpacer} />
                        <Button appearance="primary" onClick={handleSaveAndNext} disabled={!isCurrentStepValid}>
                            {isLastStep
                                ? intl.formatMessage(OnboardingWizardResources.finish)
                                : intl.formatMessage(OnboardingWizardResources.saveAndNext)}
                        </Button>
                        <Button appearance="secondary" onClick={handleSkip}>
                            {intl.formatMessage(OnboardingWizardResources.skip)}
                        </Button>
                    </div>
                </div>
            </div>
        </div>
    );
};
