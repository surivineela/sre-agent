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
    MessageBar,
    MessageBarActions,
    MessageBarBody,
    MessageBarTitle,
    Option,
    Spinner,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled } from '@fluentui/react-icons';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router';
import {
    AzMonitorResources,
    IncidentManagementPlatformResources,
    IncidentManagementResources,
    PagerDutyResources,
    SettingsTabResources,
    SreAgentResources,
} from '../../Strings/SREAgentResources';
import { IncidentManagementFormProps, IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { useIncidentManagementConnectivity } from '../Hooks/useIncidentManagementConnectivity';
import { useDialogStyles, usePagerDutyStyles, useSettingsStyles } from './Styles/Settings.styles';

const IncidentManagementForm: FC<IncidentManagementFormProps> = ({
    formikProps,
    loading,
    loadFailure,
    saving,
    disconnect,
}: IncidentManagementFormProps) => {
    const intl = useIntl();
    const styles = useSettingsStyles();
    const pagerDutyStyles = usePagerDutyStyles();
    const { dangerButton } = useDialogStyles();
    const { setFieldValue, setFieldTouched, submitForm, resetForm, values, isValid, isValidating, isSubmitting, dirty, initialValues } =
        formikProps;
    const [editingApiKey, setEditingApiKey] = useState(false);
    const isSetupScenario = useMemo(() => initialValues.platform === IncidentManagementPlatform.Disconnected, [initialValues.platform]);
    const isApiKeyEditable = useMemo(() => isSetupScenario || editingApiKey, [isSetupScenario, editingApiKey]);
    const isPagerDutySetUp = useMemo(() => initialValues.platform === IncidentManagementPlatform.PagerDuty, [initialValues.platform]);

    const location = useLocation();
    const navigate = useNavigate();

    const { isIncidentManagementConnected } = useIncidentManagementConnectivity(isPagerDutySetUp);

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
    }, [initialValues.platform, intl]);

    const handleGoToIncidentManagement = useCallback(() => {
        navigate({ ...location, pathname: '/views/incidentmanagement' });
    }, [location, navigate]);

    return (
        <>
            <MessageBar style={{ maxWidth: 1000, marginBottom: 16 }}>
                {intl.formatMessage(IncidentManagementResources.setUpInfoBanner)}
                <Button
                    appearance="secondary"
                    size="medium"
                    style={{ marginRight: 10, marginTop: 10, marginBottom: 10 }}
                    onClick={() => {
                        handleGoToIncidentManagement();
                    }}
                >
                    {intl.formatMessage(IncidentManagementResources.goToIncidentManagement)}
                </Button>
            </MessageBar>
            <div style={styles.generalSettingsHeader}>{intl.formatMessage(SettingsTabResources.incidentPlatform)}</div>
            <div>
                {loading ? (
                    <Spinner />
                ) : (
                    <>
                        {isSetupScenario && (
                            <div style={styles.incidentManagementDescriptionStyle}>
                                {intl.formatMessage(IncidentManagementResources.incidentManagementDescription)}
                            </div>
                        )}
                        {values.platform === IncidentManagementPlatform.PagerDuty && !isSetupScenario && (
                            <div style={{ display: 'flex', justifyContent: 'flex-start' }}>
                                <MessageBar intent="info" className={pagerDutyStyles.messageBar}>
                                    <MessageBarBody className={pagerDutyStyles.messageBarBody}>
                                        <MessageBarTitle style={{ fontWeight: 400 }}>
                                            {intl.formatMessage(PagerDutyResources.setUpIncidentHandlers)}
                                        </MessageBarTitle>
                                        <div className={pagerDutyStyles.messageBarActionsContainer}>
                                            <MessageBarActions
                                                containerAction={
                                                    <Button appearance="secondary" size="medium" onClick={handleGoToIncidentManagement}>
                                                        {intl.formatMessage(PagerDutyResources.goToIncidentManagement)}
                                                    </Button>
                                                }
                                            />
                                        </div>
                                    </MessageBarBody>
                                </MessageBar>
                            </div>
                        )}
                        <Field
                            id="platformField"
                            label={intl.formatMessage(IncidentManagementResources.incidentPlatform)}
                            orientation="horizontal"
                            required={true}
                            style={{ maxWidth: '1000px' }}
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
                                    <div className={pagerDutyStyles.iconContainer}>
                                        <CheckmarkCircle16Filled
                                            className={pagerDutyStyles.greenCheckIcon}
                                            aria-label={intl.formatMessage(IncidentManagementResources.setUpComplete)}
                                        />
                                        <div>
                                            {isIncidentManagementConnected
                                                ? intl.formatMessage(PagerDutyResources.connectedMessage)
                                                : intl.formatMessage(PagerDutyResources.addedMessage)}
                                        </div>
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
                                    style={{ maxWidth: '1000px' }}
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
