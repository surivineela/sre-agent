import { Input, Label, mergeClasses, Text } from '@fluentui/react-components';
import { DismissCircleFilled } from '@fluentui/react-icons';
import { useFormikContext } from 'formik';
import { FC, useCallback, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { FirstPartyHelper } from '../../../Common/Helpers/FirstPartyHelper';
import { IncidentManagementPlatformResources, OnboardingWizardResources, PagerDutyResources } from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { WizardFormValues } from '../OnboardingWizard';
import { useIncidentPlatformStepStyles } from '../OnboardingWizard.styles';

interface PlatformOption {
    type: IncidentManagementType;
    name: string;
    /** Path to SVG image, or undefined for icon-only display */
    imagePath?: string;
    /** Whether to use a Fluent icon instead of image */
    useIcon?: boolean;
}

export const IncidentPlatformStep: FC = () => {
    const intl = useIntl();
    const styles = useIncidentPlatformStepStyles();
    const { agentObj } = useContext(SreAgentContext);
    const { values, setFieldValue } = useFormikContext<WizardFormValues>();

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

    const handlePlatformSelect = useCallback(
        (type: IncidentManagementType) => {
            setFieldValue('incidentPlatformType', type);
        },
        [setFieldValue]
    );

    const renderPlatformConfig = () => {
        if (
            !values.incidentPlatformType ||
            values.incidentPlatformType === IncidentManagementType.None ||
            values.incidentPlatformType === IncidentManagementType.AzMonitor ||
            values.incidentPlatformType === IncidentManagementType.Icm
        ) {
            return null;
        }

        return (
            <div className={styles.configForm}>
                {values.incidentPlatformType === IncidentManagementType.PagerDuty && (
                    <div className={styles.formField}>
                        <Label required>{intl.formatMessage(PagerDutyResources.pagerDutyApiKey)}</Label>
                        <Input
                            type="password"
                            placeholder={intl.formatMessage(OnboardingWizardResources.apiKeyPlaceholder)}
                            value={values.pagerDutyApiKey}
                            onChange={(_, data) => setFieldValue('pagerDutyApiKey', data.value)}
                        />
                    </div>
                )}

                {values.incidentPlatformType === IncidentManagementType.ServiceNow && (
                    <>
                        <div className={styles.formField}>
                            <Label required>{intl.formatMessage(OnboardingWizardResources.endpoint)}</Label>
                            <Input
                                placeholder={intl.formatMessage(OnboardingWizardResources.endpointPlaceholder)}
                                value={values.serviceNowEndpoint}
                                onChange={(_, data) => setFieldValue('serviceNowEndpoint', data.value)}
                            />
                        </div>
                        <div className={styles.formField}>
                            <Label required>{intl.formatMessage(OnboardingWizardResources.username)}</Label>
                            <Input
                                placeholder={intl.formatMessage(OnboardingWizardResources.usernamePlaceholder)}
                                value={values.serviceNowUsername}
                                onChange={(_, data) => setFieldValue('serviceNowUsername', data.value)}
                            />
                        </div>
                        <div className={styles.formField}>
                            <Label required>{intl.formatMessage(OnboardingWizardResources.password)}</Label>
                            <Input
                                type="password"
                                placeholder={intl.formatMessage(OnboardingWizardResources.passwordPlaceholder)}
                                value={values.serviceNowPassword}
                                onChange={(_, data) => setFieldValue('serviceNowPassword', data.value)}
                            />
                        </div>
                    </>
                )}
            </div>
        );
    };

    return (
        <div className={styles.container}>
            <Text className={styles.description}>{intl.formatMessage(OnboardingWizardResources.incidentPlatformDescription)}</Text>

            <div className={styles.platformGrid}>
                {platformOptions.map(platform => (
                    <div
                        key={platform.type}
                        className={mergeClasses(
                            styles.platformCard,
                            values.incidentPlatformType === platform.type && styles.platformCardSelected
                        )}
                        onClick={() => handlePlatformSelect(platform.type)}
                        role="button"
                        tabIndex={0}
                        onKeyDown={e => e.key === 'Enter' && handlePlatformSelect(platform.type)}
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

            {renderPlatformConfig()}
        </div>
    );
};
