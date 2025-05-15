import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    Dropdown,
    Field,
    Input,
    Option,
    Spinner,
} from '@fluentui/react-components';
import { FC, useMemo, useState } from 'react';
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
import { useDialogStyles, useSettingsStyles } from './Styles/Settings.styles';

const IncidentManagementForm: FC<IncidentManagementFormProps> = ({
    formikProps,
    loading,
    loadFailure,
    saving,
    disconnect,
}: IncidentManagementFormProps) => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const { dangerButton } = useDialogStyles();
    const { setFieldValue, setFieldTouched, submitForm, resetForm, values, isValid, isValidating, isSubmitting, dirty, initialValues } =
        formikProps;
    const [editingApiKey, setEditingApiKey] = useState(false);
    const isSetupScenario = useMemo(() => initialValues.platform === IncidentManagementPlatform.Disconnected, [initialValues.platform]);
    const isApiKeyEditable = useMemo(() => isSetupScenario || editingApiKey, [initialValues.platform, editingApiKey]);

    const incidentPlatformDropdownOptions = useMemo(
        () => [
            { key: IncidentManagementPlatform.PagerDuty, text: intl.formatMessage(IncidentManagementPlatformResources.pagerDuty) },
            { key: IncidentManagementPlatform.AzMonitor, text: intl.formatMessage(IncidentManagementPlatformResources.azMonitor) },
        ],
        [intl]
    );

    const selectedPlatformDisplayName = useMemo(() => {
        const selectedPlatform = incidentPlatformDropdownOptions.find(option => option.key === values.platform);
        return selectedPlatform?.text || '';
    }, [incidentPlatformDropdownOptions, values.platform]);

    const isDirty = useMemo(() => {
        if (values.platform !== IncidentManagementPlatform.PagerDuty && initialValues.platform === values.platform) {
            return false;
        }
        return dirty;
    }, [dirty, values.platform, initialValues.platform]);

    const { disconnectConfirmationTitle, disconnectConfirmationMessage } = useMemo(() => {
        if (initialValues.platform === IncidentManagementPlatform.PagerDuty) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(PagerDutyResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(PagerDutyResources.disconnectConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementPlatform.AzMonitor) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(AzMonitorResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(AzMonitorResources.disconnectConfirmationMessage),
            };
        }

        return {
            disconnectConfirmationTitle: '',
            disconnectConfirmationMessage: '',
        };
    }, [initialValues.platform, intl.formatMessage]);

    return (
        <>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.incidentManagement)}</div>
            <div>
                <div style={styles.incidentManagementDescriptionStyle}>
                    {intl.formatMessage(IncidentManagementResources.incidentManagementDescription)}
                </div>
                {loading ? (
                    <Spinner />
                ) : (
                    <>
                        <Field
                            id="platformField"
                            label={intl.formatMessage(IncidentManagementResources.incidentPlatform)}
                            orientation="horizontal"
                            required={true}
                        >
                            <Dropdown
                                id="platform"
                                style={styles.dropdownStyles}
                                value={selectedPlatformDisplayName}
                                placeholder={intl.formatMessage(IncidentManagementPlatformResources.disconnected)}
                                onOptionSelect={(_event, data) => {
                                    setFieldTouched('platform', true, false);
                                    setFieldValue('platform', data?.optionValue);
                                    if (data?.optionValue !== IncidentManagementPlatform.PagerDuty) {
                                        setFieldValue('connectionKey', undefined, false);
                                        setFieldTouched('connectionKey', false, false);
                                    }
                                }}
                                disabled={loading || !!loadFailure || saving || !isSetupScenario}
                            >
                                {incidentPlatformDropdownOptions.map(option => (
                                    <Option value={option.key} checkIcon={null}>
                                        {option.text}
                                    </Option>
                                ))}
                            </Dropdown>
                        </Field>

                        {values.platform === IncidentManagementPlatform.PagerDuty && (
                            <>
                                <div style={styles.pagerDutyWrapperStyle}>
                                    <img src="./PagerDuty.svg" alt="PagerDuty" style={styles.pagerDutyLogoStyle} />
                                </div>
                                <div style={styles.incidentManagementDescriptionStyle}>
                                    {intl.formatMessage(PagerDutyResources.description)}
                                </div>
                                {!isSetupScenario && (
                                    <div style={styles.connectedWrapperStyle}>
                                        <img src="./success.svg" alt="Connected" style={styles.connectedImageStyle} />
                                        <span>{intl.formatMessage(PagerDutyResources.connectedMessage)}</span>
                                    </div>
                                )}
                                <Field
                                    id="connectionKeyField"
                                    label={intl.formatMessage(PagerDutyResources.pagerDutyApiKey)}
                                    orientation="horizontal"
                                    required={true}
                                    validationMessage={
                                        formikProps.touched.connectionKey && !isValidating ? formikProps.errors.connectionKey : undefined
                                    }
                                >
                                    <Input
                                        style={styles.textFieldStyles}
                                        id="connectionKey"
                                        value={isApiKeyEditable ? values.connectionKey : undefined}
                                        placeholder={isApiKeyEditable ? undefined : '********************'}
                                        onChange={(_event, newValue) => {
                                            setFieldTouched('connectionKey', true, false);
                                            setFieldValue('connectionKey', newValue?.value);
                                        }}
                                        disabled={saving || !isApiKeyEditable}
                                        contentAfter={isValidating && !isSubmitting ? <Spinner size={'tiny'} /> : null}
                                    />
                                </Field>
                            </>
                        )}

                        {values.platform === IncidentManagementPlatform.AzMonitor && (
                            <>
                                <div style={styles.azMonitorWrapperStyle}>
                                    <img src="./AzMonitor.svg" alt="Azure Monitor" style={styles.azMonitorLogoStyle} />
                                    <span style={styles.azMonitorNameStyle}>
                                        {intl.formatMessage(IncidentManagementPlatformResources.azMonitor)}
                                    </span>
                                </div>
                                <div style={styles.incidentManagementDescriptionStyle}>
                                    {intl.formatMessage(AzMonitorResources.description)}
                                </div>
                                {!isSetupScenario && (
                                    <div style={styles.connectedWrapperStyle}>
                                        <img src="./success.svg" alt="Connected" style={styles.connectedImageStyle} />
                                        <span>{intl.formatMessage(AzMonitorResources.connectedMessage)}</span>
                                    </div>
                                )}
                            </>
                        )}

                        <div style={styles.buttonsWrapperStyle}>
                            {initialValues.platform === IncidentManagementPlatform.PagerDuty && !editingApiKey && (
                                <Button
                                    appearance="secondary"
                                    style={{ borderRadius: 5, marginRight: 10 }}
                                    onClick={() => {
                                        setEditingApiKey(true);
                                    }}
                                >
                                    {intl.formatMessage(PagerDutyResources.changeKey)}
                                </Button>
                            )}

                            {(isSetupScenario || editingApiKey) && (
                                <Button
                                    appearance="primary"
                                    style={{ borderRadius: 5, marginRight: 10 }}
                                    onClick={() => {
                                        setEditingApiKey(false);
                                        submitForm();
                                    }}
                                    disabled={
                                        !isDirty ||
                                        saving ||
                                        isValidating ||
                                        !isValid ||
                                        (values.platform === IncidentManagementPlatform.PagerDuty && !values.connectionKey)
                                    }
                                >
                                    {intl.formatMessage(SreAgentResources.save)}
                                </Button>
                            )}

                            {(isSetupScenario || editingApiKey) && (
                                <Button
                                    appearance="secondary"
                                    style={{ borderRadius: 5, marginRight: 10 }}
                                    onClick={() => {
                                        setEditingApiKey(false);
                                        resetForm();
                                    }}
                                    disabled={(!isDirty && !editingApiKey) || saving}
                                >
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                            )}

                            {!isSetupScenario && (
                                <Dialog modalType="alert">
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary" style={{ borderRadius: 5 }} disabled={saving}>
                                            {intl.formatMessage(SreAgentResources.disconnect)}
                                        </Button>
                                    </DialogTrigger>
                                    <DialogSurface>
                                        <DialogBody>
                                            <DialogTitle>{disconnectConfirmationTitle}</DialogTitle>
                                            <DialogContent>{disconnectConfirmationMessage}</DialogContent>
                                            <DialogActions>
                                                <DialogTrigger>
                                                    <Button className={dangerButton} onClick={() => disconnect()}>
                                                        {intl.formatMessage(SreAgentResources.yes)}
                                                    </Button>
                                                </DialogTrigger>
                                                <DialogTrigger disableButtonEnhancement>
                                                    <Button appearance="secondary">{intl.formatMessage(SreAgentResources.no)}</Button>
                                                </DialogTrigger>
                                            </DialogActions>
                                        </DialogBody>
                                    </DialogSurface>
                                </Dialog>
                            )}
                        </div>
                    </>
                )}
            </div>
        </>
    );
};

export default IncidentManagementForm;
