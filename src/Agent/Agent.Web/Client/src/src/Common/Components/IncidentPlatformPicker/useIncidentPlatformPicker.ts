import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentContext } from '../../../Space/Contracts/Context';
import { getIncidentManagementConfguration } from '../../../Space/Onboarding/Utilities';
import {
    IncidentManagementNotificationResources,
    IncidentManagementPlatformResources,
    OnboardingWizardResources,
} from '../../../Strings/SREAgentResources';
import { AzPortalContext, useAzPortalContext } from '../../AzPortalProxy/Providers/AzPortalProxyContext';
import { getErrorMessage } from '../../Clients/ArmClient';
import { IncidentManagementType } from '../../Contracts/Azure/SreAgent';
import { FirstPartyHelper } from '../../Helpers/FirstPartyHelper';
import { IncidentPlatformConfig } from './IncidentPlatformDialog';
import { PlatformOption } from './IncidentPlatformPicker';

export interface UseIncidentPlatformPickerResult {
    initialConfig: IncidentPlatformConfig;

    // Dialog state
    isDialogOpen: boolean;
    setIsDialogOpen: (open: boolean) => void;

    // Platform selection
    selectedPlatformType: IncidentManagementType | undefined;
    setSelectedPlatformType: (type: IncidentManagementType) => void;
    platformOptions: PlatformOption[];

    // Form values
    pagerDutyApiKey: string;
    setPagerDutyApiKey: (value: string) => void;
    serviceNowEndpoint: string;
    setServiceNowEndpoint: (value: string) => void;
    serviceNowUsername: string;
    setServiceNowUsername: (value: string) => void;
    serviceNowPassword: string;
    setServiceNowPassword: (value: string) => void;

    // Computed values
    pickerValues: IncidentPlatformConfig;
    isFormValid: boolean;

    // Save handlers
    saveIncidentPlatform: (values: IncidentPlatformConfig) => Promise<boolean>;
    isSaving: boolean;

    isIncidentPlatformConfigured: boolean;
}

export interface UseIncidentPlatformPickerProps {
    initialPlatformType?: IncidentManagementType;
    initialPagerDutyApiKey?: string;
    initialServiceNowEndpoint?: string;
    initialServiceNowUsername?: string;
    initialServiceNowPassword?: string;
}

export const useIncidentPlatformPicker = (props?: UseIncidentPlatformPickerProps): UseIncidentPlatformPickerResult => {
    const {
        initialPlatformType,
        initialPagerDutyApiKey = '',
        initialServiceNowEndpoint = '',
        initialServiceNowUsername = '',
        initialServiceNowPassword = '',
    } = props ?? {};

    const intl = useIntl();
    const azPortalContext = useAzPortalContext();
    const { agentObj, patchAgent } = useContext(SreAgentContext);
    const proxy = useContext(AzPortalContext);

    const tenantId = agentObj?.identity?.tenantId ?? '';

    // Dialog state
    const [isDialogOpen, setIsDialogOpen] = useState(false);
    const [isSaving, setIsSaving] = useState(false);

    // Platform selection state
    const [selectedPlatformType, setSelectedPlatformTypeState] = useState<IncidentManagementType | undefined>(initialPlatformType);

    const [isIncidentPlatformConfigured, setIsIncidentPlatformConfigured] = useState<boolean>(false);

    // Form values state
    const [pagerDutyApiKey, setPagerDutyApiKey] = useState(initialPagerDutyApiKey);
    const [serviceNowEndpoint, setServiceNowEndpoint] = useState(initialServiceNowEndpoint);
    const [serviceNowUsername, setServiceNowUsername] = useState(initialServiceNowUsername);
    const [serviceNowPassword, setServiceNowPassword] = useState(initialServiceNowPassword);

    const setSelectedPlatformType = useCallback((type: IncidentManagementType) => {
        setSelectedPlatformTypeState(type);
    }, []);

    const initialConfig: IncidentPlatformConfig = useMemo(
        () => ({
            type: initialPlatformType || IncidentManagementType.None,
            pagerDutyApiKey: initialPagerDutyApiKey,
            serviceNowEndpoint: initialServiceNowEndpoint,
            serviceNowUsername: initialServiceNowUsername,
            serviceNowPassword: initialServiceNowPassword,
        }),
        [initialPlatformType, initialPagerDutyApiKey, initialServiceNowEndpoint, initialServiceNowUsername, initialServiceNowPassword]
    );

    const platformOptions = useMemo<PlatformOption[]>(() => {
        const options: PlatformOption[] = [
            {
                type: IncidentManagementType.PagerDuty,
                name: intl.formatMessage(IncidentManagementPlatformResources.pagerDuty),
                imagePath: './PagerDuty.svg',
            },
            {
                type: IncidentManagementType.AzMonitor,
                name: intl.formatMessage(IncidentManagementPlatformResources.azMonitor),
                imagePath: './AzMonitor.svg',
            },
            {
                type: IncidentManagementType.ServiceNow,
                name: intl.formatMessage(IncidentManagementPlatformResources.serviceNow),
                imagePath: './ServiceNow.svg',
            },
            {
                type: IncidentManagementType.None,
                name: intl.formatMessage(OnboardingWizardResources.noIncidentPlatform),
                useIcon: true,
            },
        ];

        if (FirstPartyHelper.shouldEnableForIcm(tenantId)) {
            options.splice(2, 0, {
                type: IncidentManagementType.Icm,
                name: intl.formatMessage(IncidentManagementPlatformResources.icm),
                imagePath: './IcM.svg',
            });
        }

        return options;
    }, [intl, tenantId]);

    const pickerValues = useMemo<IncidentPlatformConfig>(
        () => ({
            type: selectedPlatformType || IncidentManagementType.None,
            pagerDutyApiKey,
            serviceNowEndpoint,
            serviceNowUsername,
            serviceNowPassword,
        }),
        [selectedPlatformType, pagerDutyApiKey, serviceNowEndpoint, serviceNowUsername, serviceNowPassword]
    );

    const isFormValid = useMemo(() => {
        if (!selectedPlatformType) return false;

        switch (selectedPlatformType) {
            case IncidentManagementType.None:
            case IncidentManagementType.AzMonitor:
            case IncidentManagementType.Icm:
                return true;
            case IncidentManagementType.PagerDuty:
                return pagerDutyApiKey.trim().length > 0;
            case IncidentManagementType.ServiceNow:
                return serviceNowEndpoint.trim().length > 0 && serviceNowUsername.trim().length > 0 && serviceNowPassword.trim().length > 0;
            default:
                return false;
        }
    }, [selectedPlatformType, pagerDutyApiKey, serviceNowEndpoint, serviceNowUsername, serviceNowPassword]);

    const saveIncidentPlatform = useCallback(
        async (values: IncidentPlatformConfig) => {
            if (!values.type) return false;
            let success = true;

            const id = proxy.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.saveTitle),
                intl.formatMessage(IncidentManagementNotificationResources.saveInProgress)
            );

            setIsSaving(true);
            setIsDialogOpen(false);

            const config = getIncidentManagementConfguration({
                incidentPlatformType: values.type,
                pagerDutyApiKey: values.pagerDutyApiKey,
                serviceNowEndpoint: values.serviceNowEndpoint,
                serviceNowUsername: values.serviceNowUsername,
                serviceNowPassword: values.serviceNowPassword,
            });

            const response = await patchAgent({
                properties: {
                    incidentManagementConfiguration: config,
                },
            });

            if (response.metadata.success) {
                setIsIncidentPlatformConfigured(true);
                proxy.stopNotification(id, true, intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded));
                azPortalContext.log({
                    action: 'suggested-actions-infrastructure',
                    actionModifier: 'saved',
                    logLevel: 'info',
                    data: { incidentPlatformType: values.type },
                });
            } else {
                const errorMessage = getErrorMessage(response.metadata.error);
                proxy.stopNotification(
                    id,
                    false,
                    intl.formatMessage(IncidentManagementNotificationResources.saveFailed, {
                        errorMessage,
                    })
                );
                azPortalContext.log({
                    action: 'suggested-actions-infrastructure',
                    actionModifier: 'save-failed',
                    logLevel: 'error',
                    data: { error: errorMessage },
                });
                success = false;
            }

            setIsSaving(false);

            return success;
        },
        [patchAgent, azPortalContext, proxy, intl]
    );

    return {
        initialConfig,

        // Dialog state
        isDialogOpen,
        setIsDialogOpen,

        // Platform selection
        selectedPlatformType,
        setSelectedPlatformType,
        platformOptions,

        // Form values
        pagerDutyApiKey,
        setPagerDutyApiKey,
        serviceNowEndpoint,
        setServiceNowEndpoint,
        serviceNowUsername,
        setServiceNowUsername,
        serviceNowPassword,
        setServiceNowPassword,

        // Computed values
        pickerValues,
        isFormValid,

        // Save handlers
        saveIncidentPlatform,
        isSaving,

        isIncidentPlatformConfigured,
    };
};
