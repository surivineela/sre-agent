import { Dropdown, TextField } from '@fluentui/react';
import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
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
import {
    incidentManagementDropdownStyles,
    incidentManagementMaskedTextFieldStyles,
    useDialogStyles,
    useSettingsStyles,
} from './Styles/Settings.styles';

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
    const { setFieldValue, setFieldTouched, submitForm, resetForm, values, isValidating, isSubmitting, dirty, initialValues } = formikProps;
    const [editingApiKey, setEditingApiKey] = useState(false);
    const isSetupScenario = useMemo(() => initialValues.platform === IncidentManagementPlatform.Disconnected, [initialValues.platform]);
    const isApiKeyEditable = useMemo(() => isSetupScenario || editingApiKey, [initialValues.platform, editingApiKey]);

    const incidentPlatformDropdownOptions = useMemo(
        () => [
            { key: IncidentManagementPlatform.Disconnected, text: intl.formatMessage(IncidentManagementPlatformResources.disconnected) },
            { key: IncidentManagementPlatform.PagerDuty, text: intl.formatMessage(IncidentManagementPlatformResources.pagerDuty) },
            { key: IncidentManagementPlatform.AzMonitor, text: intl.formatMessage(IncidentManagementPlatformResources.azMonitor) },
        ],
        [intl]
    );

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
                        <Dropdown
                            id="platform"
                            options={incidentPlatformDropdownOptions}
                            label={intl.formatMessage(IncidentManagementResources.incidentPlatform)}
                            required={true}
                            styles={incidentManagementDropdownStyles}
                            selectedKey={values.platform}
                            onChange={(_event, option, _index) => {
                                setFieldTouched('platform', true, false);
                                setFieldValue('platform', option?.key);
                                if (option?.key !== IncidentManagementPlatform.PagerDuty) {
                                    setFieldValue('connectionKey', undefined, false);
                                    setFieldTouched('connectionKey', false, false);
                                }
                            }}
                            disabled={loading || !!loadFailure || saving || !isSetupScenario}
                        />

                        {values.platform === IncidentManagementPlatform.PagerDuty && (
                            <>
                                <div style={styles.pagerDutyWrapperStyle}>
                                    <img src="./PagerDuty.svg" alt="PagerDuty" style={styles.pagerDutyLogoStyle} />
                                </div>
                                <div style={styles.incidentManagementDescriptionStyle}>
                                    {intl.formatMessage(PagerDutyResources.pagerDutyApiKeyDescription)}
                                </div>
                                {!isSetupScenario && (
                                    <div style={styles.connectedWrapperStyle}>
                                        <img src="./success.svg" alt="Connected" style={styles.connectedImageStyle} />
                                        <span>{intl.formatMessage(PagerDutyResources.connectedMessage)}</span>
                                    </div>
                                )}
                                <TextField
                                    id="connectionKey"
                                    label={intl.formatMessage(PagerDutyResources.pagerDutyApiKey)}
                                    required={true}
                                    styles={incidentManagementMaskedTextFieldStyles}
                                    value={isApiKeyEditable ? values.connectionKey : undefined}
                                    placeholder={isApiKeyEditable ? undefined : '********************'}
                                    onChange={(_, value) => {
                                        setFieldTouched('connectionKey', true, false);
                                        setFieldValue('connectionKey', value);
                                    }}
                                    disabled={saving || !isApiKeyEditable}
                                    errorMessage={
                                        formikProps.touched.connectionKey && !isValidating ? formikProps.errors.connectionKey : undefined
                                    }
                                    onRenderSuffix={isValidating && !isSubmitting ? () => <Spinner size={'tiny'} /> : undefined}
                                />
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
                                    {intl.formatMessage(PagerDutyResources.editKey)}
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
                                        !!formikProps.errors.connectionKey ||
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
                                    {intl.formatMessage(editingApiKey ? SreAgentResources.cancel : SreAgentResources.discard)}
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
