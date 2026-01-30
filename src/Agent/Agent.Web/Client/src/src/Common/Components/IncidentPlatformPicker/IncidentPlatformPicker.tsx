import { Input, Label, makeStyles, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { DismissCircleFilled } from '@fluentui/react-icons';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { SreAgentContext } from '../../../Space/Contracts/Context';
import { IncidentManagementPlatformResources, OnboardingWizardResources, PagerDutyResources } from '../../../Strings/SREAgentResources';
import { IncidentManagementType } from '../../Contracts/Azure/SreAgent';
import { FirstPartyHelper } from '../../Helpers/FirstPartyHelper';

export interface PlatformOption {
    type: IncidentManagementType;
    name: string;
    /** Path to SVG image, or undefined for icon-only display */
    imagePath?: string;
    /** Whether to use a Fluent icon instead of image */
    useIcon?: boolean;
}

export interface IncidentPlatformValues {
    incidentPlatformType?: IncidentManagementType;
    pagerDutyApiKey: string;
    serviceNowEndpoint: string;
    serviceNowUsername: string;
    serviceNowPassword: string;
}

export interface IncidentPlatformPickerProps {
    values: IncidentPlatformValues;
    onPlatformSelect: (type: IncidentManagementType) => void;
    onPagerDutyApiKeyChange: (value: string) => void;
    onServiceNowEndpointChange: (value: string) => void;
    onServiceNowUsernameChange: (value: string) => void;
    onServiceNowPasswordChange: (value: string) => void;
    /** Optional description text to show at the top */
    showDescription?: boolean;
}

const useIncidentPlatformPickerStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    description: {
        fontSize: tokens.fontSizeBase300,
        color: tokens.colorNeutralForeground2,
    },
    platformGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fill, minmax(140px, 1fr))',
        gap: tokens.spacingHorizontalM,
    },
    platformCard: {
        padding: tokens.spacingVerticalM,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusLarge,
        cursor: 'pointer',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalS,
        transitionProperty: 'all',
        transitionDuration: '0.2s',
        transitionTimingFunction: 'ease',
        backgroundColor: tokens.colorNeutralBackground1,
        '&:hover': {
            borderTopColor: tokens.colorBrandStroke1,
            borderRightColor: tokens.colorBrandStroke1,
            borderBottomColor: tokens.colorBrandStroke1,
            borderLeftColor: tokens.colorBrandStroke1,
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    platformCardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
        backgroundColor: tokens.colorBrandBackground2,
    },
    platformIcon: {
        width: '40px',
        height: '40px',
        color: tokens.colorNeutralForeground3,
    },
    platformImage: {
        width: '40px',
        height: '40px',
        objectFit: 'contain',
    },
    platformName: {
        fontSize: tokens.fontSizeBase200,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        textAlign: 'center',
    },
    configForm: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        marginTop: tokens.spacingVerticalM,
        padding: tokens.spacingVerticalM,
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusLarge,
    },
    formField: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
});

interface PlatformConfigFormProps {
    platformType: IncidentManagementType | undefined;
    pagerDutyApiKey: string;
    serviceNowEndpoint: string;
    serviceNowUsername: string;
    serviceNowPassword: string;
    onPagerDutyApiKeyChange: (value: string) => void;
    onServiceNowEndpointChange: (value: string) => void;
    onServiceNowUsernameChange: (value: string) => void;
    onServiceNowPasswordChange: (value: string) => void;
}

/**
 * Configuration form for the selected incident platform.
 * Renders platform-specific input fields (PagerDuty API key, ServiceNow credentials, etc.)
 */
const PlatformConfigForm: FC<PlatformConfigFormProps> = ({
    platformType,
    pagerDutyApiKey,
    serviceNowEndpoint,
    serviceNowUsername,
    serviceNowPassword,
    onPagerDutyApiKeyChange,
    onServiceNowEndpointChange,
    onServiceNowUsernameChange,
    onServiceNowPasswordChange,
}) => {
    const intl = useIntl();
    const styles = useIncidentPlatformPickerStyles();

    // Platforms that don't require configuration
    if (
        !platformType ||
        platformType === IncidentManagementType.None ||
        platformType === IncidentManagementType.AzMonitor ||
        platformType === IncidentManagementType.Icm
    ) {
        return null;
    }

    return (
        <div className={styles.configForm}>
            {platformType === IncidentManagementType.PagerDuty && (
                <div className={styles.formField}>
                    <Label required>{intl.formatMessage(PagerDutyResources.pagerDutyApiKey)}</Label>
                    <Input
                        type="password"
                        placeholder={intl.formatMessage(OnboardingWizardResources.apiKeyPlaceholder)}
                        value={pagerDutyApiKey}
                        onChange={(_, data) => onPagerDutyApiKeyChange(data.value)}
                    />
                </div>
            )}

            {platformType === IncidentManagementType.ServiceNow && (
                <>
                    <div className={styles.formField}>
                        <Label required>{intl.formatMessage(OnboardingWizardResources.endpoint)}</Label>
                        <Input
                            placeholder={intl.formatMessage(OnboardingWizardResources.endpointPlaceholder)}
                            value={serviceNowEndpoint}
                            onChange={(_, data) => onServiceNowEndpointChange(data.value)}
                        />
                    </div>
                    <div className={styles.formField}>
                        <Label required>{intl.formatMessage(OnboardingWizardResources.username)}</Label>
                        <Input
                            placeholder={intl.formatMessage(OnboardingWizardResources.usernamePlaceholder)}
                            value={serviceNowUsername}
                            onChange={(_, data) => onServiceNowUsernameChange(data.value)}
                        />
                    </div>
                    <div className={styles.formField}>
                        <Label required>{intl.formatMessage(OnboardingWizardResources.password)}</Label>
                        <Input
                            type="password"
                            placeholder={intl.formatMessage(OnboardingWizardResources.passwordPlaceholder)}
                            value={serviceNowPassword}
                            onChange={(_, data) => onServiceNowPasswordChange(data.value)}
                        />
                    </div>
                </>
            )}
        </div>
    );
};

/**
 * Shared incident platform picker component used by both OnboardingWizard and Overview dialogs.
 * Renders platform selection grid and configuration forms based on selected platform.
 */
export const IncidentPlatformPicker: FC<IncidentPlatformPickerProps> = ({
    values,
    onPlatformSelect,
    onPagerDutyApiKeyChange,
    onServiceNowEndpointChange,
    onServiceNowUsernameChange,
    onServiceNowPasswordChange,
    showDescription = true,
}) => {
    const intl = useIntl();
    const styles = useIncidentPlatformPickerStyles();
    const { agentObj } = useContext(SreAgentContext);

    const tenantId = agentObj?.identity?.tenantId ?? '';

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

        if (FirstPartyHelper.shouldEnableForIcm(tenantId ?? '')) {
            options.splice(2, 0, {
                type: IncidentManagementType.Icm,
                name: intl.formatMessage(IncidentManagementPlatformResources.icm),
                imagePath: './IcM.svg',
            });
        }

        return options;
    }, [intl, tenantId]);

    return (
        <div className={styles.container}>
            {showDescription && (
                <Text className={styles.description}>{intl.formatMessage(OnboardingWizardResources.incidentPlatformDescription)}</Text>
            )}

            <div className={styles.platformGrid}>
                {platformOptions.map(platform => (
                    <div
                        key={platform.type}
                        className={mergeClasses(
                            styles.platformCard,
                            values.incidentPlatformType === platform.type && styles.platformCardSelected
                        )}
                        onClick={() => onPlatformSelect(platform.type)}
                        role="button"
                        tabIndex={0}
                        onKeyDown={e => e.key === 'Enter' && onPlatformSelect(platform.type)}
                        aria-pressed={values.incidentPlatformType === platform.type}
                    >
                        {platform.useIcon ? (
                            <DismissCircleFilled className={styles.platformIcon} aria-hidden="true" />
                        ) : (
                            <img src={platform.imagePath} alt={platform.name} className={styles.platformImage} aria-hidden="true" />
                        )}
                        <Text className={styles.platformName}>{platform.name}</Text>
                    </div>
                ))}
            </div>

            <PlatformConfigForm
                platformType={values.incidentPlatformType}
                pagerDutyApiKey={values.pagerDutyApiKey}
                serviceNowEndpoint={values.serviceNowEndpoint}
                serviceNowUsername={values.serviceNowUsername}
                serviceNowPassword={values.serviceNowPassword}
                onPagerDutyApiKeyChange={onPagerDutyApiKeyChange}
                onServiceNowEndpointChange={onServiceNowEndpointChange}
                onServiceNowUsernameChange={onServiceNowUsernameChange}
                onServiceNowPasswordChange={onServiceNowPasswordChange}
            />
        </div>
    );
};

/**
 * Validates if the incident platform form is complete
 */
export const isIncidentPlatformFormValid = (values: IncidentPlatformValues): boolean => {
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
};
