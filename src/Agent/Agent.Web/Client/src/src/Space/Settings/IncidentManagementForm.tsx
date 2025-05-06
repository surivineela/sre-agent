import { DefaultButton, Dropdown, PrimaryButton, TextField } from '@fluentui/react';
import { Spinner } from '@fluentui/react-components';
import { FC, useMemo } from 'react';
import { useIntl } from 'react-intl';
import {
    AzMonitorResources,
    IncidentManagementPlatformResources,
    IncidentManagementResources,
    PagerDutyResources,
    SettingsTabResources,
    SreAgentResources,
} from '../../Strings/SREAgentResources';
import { IncidentManagementFormProps, IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { incidentManagementDropdownStyles, incidentManagementMaskedTextFieldStyles, useSettingsStyles } from './Styles/Settings.styles';

const IncidentManagementForm: FC<IncidentManagementFormProps> = ({
    formikProps,
    loading,
    loadFailure,
    saving,
}: IncidentManagementFormProps) => {
    const styles = useSettingsStyles();
    const { setFieldValue, setFieldTouched, submitForm, resetForm, values, isValid, isValidating, isSubmitting, dirty, initialValues } =
        formikProps;
    const intl = useIntl();

    const incidentPlatformDropdownOptions = useMemo(
        () => [
            { key: IncidentManagementPlatform.Disconnected, text: intl.formatMessage(IncidentManagementPlatformResources.disconnected) },
            { key: IncidentManagementPlatform.PagerDuty, text: intl.formatMessage(IncidentManagementPlatformResources.pagerDuty) },
            { key: IncidentManagementPlatform.AzMonitor, text: intl.formatMessage(IncidentManagementPlatformResources.azMonitor) },
        ],
        [intl]
    );

    const isDirty = useMemo(() => {
        if (
            initialValues.platform === IncidentManagementPlatform.Disconnected &&
            values.platform === IncidentManagementPlatform.Disconnected
        ) {
            return false;
        }
        return dirty;
    }, [dirty, values.platform, initialValues.platform]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.incidentManagement)}</div>
            <div>
                <div style={styles.incidentManagementDescriptionStyle}>
                    {intl.formatMessage(IncidentManagementResources.incidentManagementDescription)}
                </div>
                <Dropdown
                    id="platform"
                    options={incidentPlatformDropdownOptions}
                    label={intl.formatMessage(IncidentManagementResources.incidentPlatform)}
                    required={true}
                    styles={incidentManagementDropdownStyles}
                    selectedKey={values.platform}
                    onChange={(_event, option, _index) => {
                        setFieldValue('platform', option?.key);
                        setFieldTouched('platform', true);
                        if (option?.key !== IncidentManagementPlatform.PagerDuty) {
                            setFieldValue('connectionKey', undefined, false);
                        }
                    }}
                    disabled={loading || !!loadFailure || saving}
                />

                {values.platform === IncidentManagementPlatform.PagerDuty && (
                    <>
                        <div>
                            <img src="./PagerDuty.svg" alt="PagerDuty" style={styles.pagerDutyLogoStyle} />
                        </div>
                        <div style={styles.incidentManagementDescriptionStyle}>
                            {intl.formatMessage(PagerDutyResources.pagerDutyDescription)}
                        </div>
                        <TextField
                            id="connectionKey"
                            label={intl.formatMessage(PagerDutyResources.pagerDutyApiKey)}
                            required={true}
                            styles={incidentManagementMaskedTextFieldStyles}
                            value={values.connectionKey}
                            onChange={(_, value) => {
                                setFieldValue('connectionKey', value);
                                setFieldTouched('connectionKey', true, false);
                            }}
                            disabled={saving}
                            errorMessage={formikProps.touched.connectionKey && !isValidating ? formikProps.errors.connectionKey : undefined}
                            type="password"
                            canRevealPassword={true}
                            onRenderSuffix={isValidating && !isSubmitting ? () => <Spinner size={'tiny'} /> : undefined}
                        />
                    </>
                )}

                {values.platform === IncidentManagementPlatform.AzMonitor && (
                    <>
                        <div style={{ display: 'flex', alignItems: 'center' }}>
                            <img src="./AzMonitor.svg" alt="Azure Monitor" style={styles.azMonitorLogoStyle} />
                            <span style={{ marginLeft: '10px', fontWeight: 'bold' }}>Azure Monitor</span>
                        </div>
                        <div style={styles.incidentManagementDescriptionStyle}>
                            {intl.formatMessage(AzMonitorResources.azMonitorDescription)}
                        </div>
                    </>
                )}

                <div>
                    <PrimaryButton
                        style={{ borderRadius: 5 }}
                        onClick={() => submitForm()}
                        text={intl.formatMessage(SreAgentResources.save)}
                        disabled={
                            !isDirty ||
                            saving ||
                            isValidating ||
                            !isValid ||
                            (values.platform === IncidentManagementPlatform.PagerDuty && !values.connectionKey)
                        }
                    />
                    <DefaultButton
                        style={{ borderRadius: 5, marginLeft: 10 }}
                        onClick={() => resetForm()}
                        text={intl.formatMessage(SreAgentResources.discard)}
                        disabled={!isDirty || saving}
                    />
                </div>
            </div>
        </>
    );
};

export default IncidentManagementForm;
