import {
    Breadcrumb,
    BreadcrumbButton,
    BreadcrumbDivider,
    BreadcrumbItem,
    Button,
    Checkbox,
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
    Link,
    MessageBar,
    Option,
    Spinner,
    Text,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { FirstPartyHelper } from '../../Common/Helpers/FirstPartyHelper';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import {
    AzMonitorResources,
    IcMResources,
    IncidentHandlerCreateResources,
    IncidentManagementPlatformResources,
    IncidentManagementResources,
    PagerDutyResources,
    ServiceNowResources,
    SettingsTabResources,
    SreAgentResources,
    SreAgentTabResources,
} from '../../Strings/SREAgentResources';
import { SreAgentContext } from '../Contracts/Context';
import { IncidentManagementFormProps, IncidentManagementPlatform } from '../Contracts/IncidentManagement';
import { IncidentManagementMenuKeys } from '../IncidentManagement/CreateIncidentHandler/Contracts';
import { DirtyStateConfirmationWrapper } from '../IncidentManagement/CreateIncidentHandler/DirtyStateConfirmationDialog';
import { useDialogStyles, usePagerDutyStyles, useSettingsStyles } from './Styles/Settings.styles';

const IncidentManagementFormInner: FC<IncidentManagementFormProps> = ({
    formikProps,
    loading,
    loadFailure,
    saving,
    disconnect,
    managedIdentityId,
    tenantId,
    integrated,
    close,
    keepOpen,
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

    const location = useLocation();
    const navigate = useNavigate();
    const azPortalContext = useContext(AzPortalContext);
    const {
        incidentManagement: { isIncidentManagementConnected, hasFilters },
    } = useContext(SreAgentContext);

    const incidentPlatformDropdownOptions = useMemo(() => {
        const options = [
            { key: IncidentManagementPlatform.PagerDuty, text: intl.formatMessage(IncidentManagementPlatformResources.pagerDuty) },
            { key: IncidentManagementPlatform.AzMonitor, text: intl.formatMessage(IncidentManagementPlatformResources.azMonitor) },
            { key: IncidentManagementPlatform.ServiceNow, text: intl.formatMessage(IncidentManagementPlatformResources.serviceNow) },
        ];
        const agentTenantId = tenantId ?? '';
        if (FirstPartyHelper.shouldEnableForIcm(agentTenantId)) {
            options.push({ key: IncidentManagementPlatform.Icm, text: intl.formatMessage(IncidentManagementPlatformResources.icm) });
        }
        return options;
    }, [intl, tenantId]);

    const selectedPlatformDisplayName = useMemo(() => {
        const selectedPlatform = incidentPlatformDropdownOptions.find(option => option.key === values.platform);
        return selectedPlatform?.text || '';
    }, [incidentPlatformDropdownOptions, values.platform]);

    const isDirty = useMemo(() => {
        if (
            values.platform !== IncidentManagementPlatform.PagerDuty &&
            values.platform !== IncidentManagementPlatform.ServiceNow &&
            initialValues.platform === values.platform
        ) {
            return false;
        }
        return dirty;
    }, [dirty, values.platform, initialValues.platform]);

    const {
        disconnectConfirmationTitle,
        disconnectConfirmationMessage,
        changePlatformConfirmationTitle,
        changePlatformConfirmationMessage,
    } = useMemo(() => {
        if (initialValues.platform === IncidentManagementPlatform.PagerDuty) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(PagerDutyResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(PagerDutyResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(PagerDutyResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(PagerDutyResources.changePlatformConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementPlatform.AzMonitor) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(AzMonitorResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(AzMonitorResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(AzMonitorResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(AzMonitorResources.changePlatformConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementPlatform.Icm) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(IcMResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(IcMResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(IcMResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(IcMResources.changePlatformConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementPlatform.ServiceNow) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(ServiceNowResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(ServiceNowResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(ServiceNowResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(ServiceNowResources.changePlatformConfirmationMessage),
            };
        }

        return {
            disconnectConfirmationTitle: '',
            disconnectConfirmationMessage: '',
            changePlatformConfirmationTitle: '',
            changePlatformConfirmationMessage: '',
        };
    }, [initialValues.platform, intl]);

    const [showSwitchPlatformDisconnectDialog, setShowSwitchPlatformDisconnectDialog] = useState(false);

    const managedIdentityResourceName = useMemo(() => {
        if (!managedIdentityId) {
            return '';
        }
        const resourceDescriptor = new ArmResourceDescriptor(managedIdentityId);
        return resourceDescriptor.resourceName;
    }, [managedIdentityId]);

    const handleGoToIncidentManagement = useCallback(() => {
        navigate({ ...location, pathname: `/views/incidentmanagement/${IncidentManagementMenuKeys.HandlerConfiguration}` });
    }, [location, navigate]);

    const openManagedIdentity = useCallback(() => {
        if (!managedIdentityId) {
            return;
        }
        azPortalContext.openBlade({
            detailBlade: 'ResourceMenuBlade',
            detailBladeInputs: { id: managedIdentityId },
            extension: 'HubsExtension',
        });
    }, [azPortalContext, managedIdentityId]);

    return (
        <>
            {!loading &&
                (values.platform === IncidentManagementPlatform.PagerDuty ||
                    values.platform === IncidentManagementPlatform.Icm ||
                    values.platform === IncidentManagementPlatform.ServiceNow) &&
                !isSetupScenario &&
                !integrated &&
                isIncidentManagementConnected && (
                    <MessageBar style={{ maxWidth: '80%', marginBottom: 16 }}>
                        {intl.formatMessage(
                            hasFilters
                                ? IncidentManagementResources.setUpInfoBanner
                                : IncidentManagementResources.setUpInfoBannerWithoutHandlers
                        )}
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
                )}
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
                        <Field
                            id="platformField"
                            label={intl.formatMessage(IncidentManagementResources.incidentPlatform)}
                            orientation="horizontal"
                            required={true}
                            style={{ maxWidth: '80%' }}
                        >
                            <Dropdown
                                id="platform"
                                style={styles.dropdownStyles}
                                value={selectedPlatformDisplayName}
                                placeholder={intl.formatMessage(IncidentManagementPlatformResources.disconnected)}
                                onOptionSelect={(_event, data) => {
                                    if (!isSetupScenario && data?.optionValue !== values.platform) {
                                        setShowSwitchPlatformDisconnectDialog(true);
                                    } else {
                                        setFieldTouched('platform', true, false);
                                        setFieldValue('platform', data?.optionValue);
                                        if (
                                            data?.optionValue !== IncidentManagementPlatform.PagerDuty &&
                                            data?.optionValue !== IncidentManagementPlatform.ServiceNow
                                        ) {
                                            setFieldValue('connectionKey', undefined, false);
                                            setFieldTouched('connectionKey', false, false);
                                        }
                                        // Clear ServiceNow fields when switching away from ServiceNow
                                        if (data?.optionValue !== IncidentManagementPlatform.ServiceNow) {
                                            setFieldValue('endpoint', undefined, false);
                                            setFieldValue('username', undefined, false);
                                            setFieldValue('password', undefined, false);
                                        }
                                    }

                                    azPortalContext.logAmplitudeControlEvent({
                                        targetType: 'dropdown',
                                        targetAction: 'changed',
                                        targetName: 'incidentPlatform',
                                        targetFriendlyName: 'Incident platform',
                                        valueObjectName: data?.optionValue ?? '',
                                        valueObjectFriendlyName: data?.optionValue ?? '',
                                    });
                                }}
                                disabled={loading || !!loadFailure || saving}
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
                                            {!isIncidentManagementConnected
                                                ? intl.formatMessage(PagerDutyResources.addedMessage)
                                                : hasFilters
                                                  ? intl.formatMessage(PagerDutyResources.connectedMessage)
                                                  : intl.formatMessage(PagerDutyResources.connectedMessageWithoutHandlers)}
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
                                    style={{ maxWidth: '80%' }}
                                >
                                    <Input
                                        style={styles.secureTextFieldStyles}
                                        id="connectionKey"
                                        value={isApiKeyEditable ? values.connectionKey : undefined}
                                        placeholder={isApiKeyEditable ? undefined : ''}
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

                        {values.platform === IncidentManagementPlatform.Icm && (
                            <>
                                <div style={styles.azMonitorWrapperStyle}>
                                    <img src="./IcM.svg" alt="IcM" style={styles.azMonitorLogoStyle} />
                                    <span style={styles.azMonitorNameStyle}>
                                        {intl.formatMessage(IncidentManagementPlatformResources.icm)}
                                    </span>
                                </div>
                                <div style={styles.incidentManagementDescriptionStyle}>
                                    <Text block>{intl.formatMessage(IcMResources.connectionDescription)}</Text>
                                    <Text block>
                                        {intl.formatMessage(IcMResources.allowListDescription)}
                                        &nbsp;&nbsp;
                                        <Link
                                            href="https://eng.ms/docs/products/icm/developers/authorizingcertificatesforprogrammaticaccesstoicm"
                                            target="_blank"
                                        >
                                            {intl.formatMessage(IcMResources.allowListLink)}
                                        </Link>
                                    </Text>
                                    <Text block>
                                        {intl.formatMessage(IcMResources.managedIdentity)}:{' '}
                                        <Link onClick={() => openManagedIdentity()}>{managedIdentityResourceName}</Link>
                                    </Text>
                                </div>
                                {!isSetupScenario && (
                                    <div className={pagerDutyStyles.iconContainer}>
                                        <CheckmarkCircle16Filled
                                            className={pagerDutyStyles.greenCheckIcon}
                                            aria-label={intl.formatMessage(IncidentManagementResources.setUpComplete)}
                                        />
                                        <div>
                                            {!isIncidentManagementConnected
                                                ? intl.formatMessage(IcMResources.addedMessage)
                                                : hasFilters
                                                  ? intl.formatMessage(IcMResources.connectedMessage)
                                                  : intl.formatMessage(IcMResources.connectedMessageWithoutHandlers)}
                                        </div>
                                    </div>
                                )}
                            </>
                        )}

                        {values.platform === IncidentManagementPlatform.ServiceNow && (
                            <>
                                <div style={styles.pagerDutyWrapperStyle}>
                                    <img src="./ServiceNow.svg" alt="ServiceNow" style={styles.pagerDutyLogoStyle} />
                                </div>
                                <div style={styles.incidentManagementDescriptionStyle}>
                                    {intl.formatMessage(ServiceNowResources.description)}
                                </div>
                                {!isSetupScenario && (
                                    <div className={pagerDutyStyles.iconContainer}>
                                        <CheckmarkCircle16Filled
                                            className={pagerDutyStyles.greenCheckIcon}
                                            aria-label={intl.formatMessage(IncidentManagementResources.setUpComplete)}
                                        />
                                        <div>
                                            {!isIncidentManagementConnected
                                                ? intl.formatMessage(ServiceNowResources.addedMessage)
                                                : hasFilters
                                                  ? intl.formatMessage(ServiceNowResources.connectedMessage)
                                                  : intl.formatMessage(ServiceNowResources.connectedMessageWithoutHandlers)}
                                        </div>
                                    </div>
                                )}

                                <Field
                                    id="endpointField"
                                    label={intl.formatMessage(ServiceNowResources.serviceNowEndpoint)}
                                    orientation="horizontal"
                                    required={true}
                                    validationMessage={
                                        formikProps.touched.endpoint && !isValidating ? formikProps.errors.endpoint : undefined
                                    }
                                    style={{ maxWidth: '80%' }}
                                >
                                    <Input
                                        style={styles.plainTextFieldStyles}
                                        id="endpoint"
                                        value={isApiKeyEditable ? values.endpoint : undefined}
                                        placeholder={values.endpoint ?? 'https://your-instance.service-now.com'}
                                        onChange={(_event, newValue) => {
                                            setFieldTouched('endpoint', true, false);
                                            setFieldValue('endpoint', newValue?.value);
                                        }}
                                        disabled={saving || !isApiKeyEditable}
                                        contentAfter={isValidating && !isSubmitting ? <Spinner size={'tiny'} /> : null}
                                    />
                                </Field>

                                <Field
                                    id="usernameField"
                                    label={intl.formatMessage(ServiceNowResources.serviceNowUsername)}
                                    orientation="horizontal"
                                    required={true}
                                    validationMessage={
                                        formikProps.touched.username && !isValidating ? formikProps.errors.username : undefined
                                    }
                                    style={{ maxWidth: '80%', marginTop: 16 }}
                                >
                                    <Input
                                        style={styles.plainTextFieldStyles}
                                        id="username"
                                        value={isApiKeyEditable ? values.username : undefined}
                                        placeholder={isApiKeyEditable ? undefined : ''}
                                        onChange={(_event, newValue) => {
                                            setFieldTouched('username', true, false);
                                            setFieldValue('username', newValue?.value);
                                        }}
                                        disabled={saving || !isApiKeyEditable}
                                        contentAfter={isValidating && !isSubmitting ? <Spinner size={'tiny'} /> : null}
                                    />
                                </Field>

                                <Field
                                    id="passwordField"
                                    label={intl.formatMessage(ServiceNowResources.serviceNowPassword)}
                                    orientation="horizontal"
                                    required={true}
                                    validationMessage={
                                        formikProps.touched.password && !isValidating ? formikProps.errors.password : undefined
                                    }
                                    style={{ maxWidth: '80%', marginTop: 16 }}
                                >
                                    <Input
                                        style={styles.secureTextFieldStyles}
                                        id="password"
                                        type="password"
                                        value={isApiKeyEditable ? values.password : undefined}
                                        placeholder={isApiKeyEditable ? undefined : ''}
                                        onChange={(_event, newValue) => {
                                            setFieldTouched('password', true, false);
                                            setFieldValue('password', newValue?.value);
                                        }}
                                        disabled={saving || !isApiKeyEditable}
                                        contentAfter={isValidating && !isSubmitting ? <Spinner size={'tiny'} /> : null}
                                    />
                                </Field>
                            </>
                        )}

                        {(values.platform === IncidentManagementPlatform.PagerDuty ||
                            values.platform === IncidentManagementPlatform.Icm ||
                            values.platform === IncidentManagementPlatform.ServiceNow) &&
                            isSetupScenario && (
                                <>
                                    <Field
                                        id="createDefaultHandlerField"
                                        label={intl.formatMessage(IncidentManagementResources.quickstartHandler)}
                                        orientation="horizontal"
                                        style={{ maxWidth: '78.5%', marginTop: 20 }}
                                    >
                                        <Checkbox
                                            id="createDefaultHandler"
                                            checked={formikProps.values.createDefaultHandler}
                                            onChange={(_event, newValue) => {
                                                setFieldTouched('createDefaultHandler', true, false);
                                                setFieldValue('createDefaultHandler', !!newValue?.checked);

                                                azPortalContext.logAmplitudeControlEvent({
                                                    targetType: 'checkbox',
                                                    targetAction: 'changed',
                                                    targetName: 'createDefaultHandler',
                                                    targetFriendlyName: 'Create default handler',
                                                    valueObjectName: newValue?.checked ? 'checked' : 'unchecked',
                                                    valueObjectFriendlyName: newValue?.checked ? 'Checked' : 'Unchecked',
                                                });
                                            }}
                                            disabled={saving}
                                            label={
                                                values.platform === IncidentManagementPlatform.PagerDuty
                                                    ? intl.formatMessage(PagerDutyResources.quickstartHandlerDescription)
                                                    : values.platform === IncidentManagementPlatform.ServiceNow
                                                      ? intl.formatMessage(ServiceNowResources.quickstartHandlerDescription)
                                                      : intl.formatMessage(IcMResources.quickstartHandlerDescription)
                                            }
                                            labelPosition="after"
                                        />
                                    </Field>
                                    {!formikProps.values.createDefaultHandler && (
                                        <MessageBar style={{ maxWidth: '80%', marginTop: 16, marginBottom: 16 }}>
                                            {intl.formatMessage(IncidentManagementResources.quickstartHandlerInfoMessage)}
                                        </MessageBar>
                                    )}
                                </>
                            )}

                        <div style={styles.buttonsWrapperStyle}>
                            {(initialValues.platform === IncidentManagementPlatform.PagerDuty ||
                                initialValues.platform === IncidentManagementPlatform.ServiceNow) &&
                                !editingApiKey && (
                                    <Button
                                        appearance="secondary"
                                        style={{ borderRadius: 5, marginRight: 10 }}
                                        onClick={() => {
                                            setEditingApiKey(true);
                                        }}
                                        disabled={saving}
                                    >
                                        {initialValues.platform === IncidentManagementPlatform.PagerDuty
                                            ? intl.formatMessage(PagerDutyResources.changeKey)
                                            : intl.formatMessage(ServiceNowResources.changeKey)}
                                    </Button>
                                )}

                            {(isSetupScenario || editingApiKey) && (
                                <Button
                                    appearance="primary"
                                    style={{ borderRadius: 5, marginRight: 10 }}
                                    onClick={() => {
                                        setEditingApiKey(false);
                                        submitForm();

                                        azPortalContext.logAmplitudeControlEvent({
                                            targetType: 'button',
                                            targetAction: 'clicked',
                                            targetName: 'save',
                                            targetFriendlyName: 'Save',
                                            valueObjectName: values.platform ?? '',
                                            valueObjectFriendlyName: values.platform ?? '',
                                        });
                                    }}
                                    disabled={
                                        !isDirty ||
                                        saving ||
                                        isValidating ||
                                        !isValid ||
                                        (values.platform === IncidentManagementPlatform.PagerDuty && !values.connectionKey) ||
                                        (values.platform === IncidentManagementPlatform.ServiceNow &&
                                            (!values.endpoint || !values.username || !values.password))
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
                                        <Button
                                            appearance="secondary"
                                            style={{ borderRadius: 5 }}
                                            disabled={saving}
                                            onClick={() => {
                                                azPortalContext.logAmplitudeControlEvent({
                                                    targetType: 'button',
                                                    targetAction: 'clicked',
                                                    targetName: 'disconnect',
                                                    targetFriendlyName: 'Disconnect',
                                                    valueObjectName: SpecialControlValue.DoAction,
                                                    valueObjectFriendlyName: SpecialControlValue.DoAction,
                                                });
                                            }}
                                        >
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
                            {integrated && !keepOpen && close && (
                                <Button
                                    appearance="secondary"
                                    style={{ borderRadius: 5, marginLeft: 10 }}
                                    onClick={() => {
                                        setEditingApiKey(false);
                                        resetForm();
                                        close();
                                    }}
                                    disabled={saving}
                                >
                                    {intl.formatMessage(SreAgentResources.close)}
                                </Button>
                            )}
                            <Dialog modalType="alert" open={showSwitchPlatformDisconnectDialog}>
                                <DialogSurface>
                                    <DialogBody>
                                        <DialogTitle>{changePlatformConfirmationTitle}</DialogTitle>
                                        <DialogContent>{changePlatformConfirmationMessage}</DialogContent>
                                        <DialogActions>
                                            <DialogTrigger>
                                                <Button
                                                    className={dangerButton}
                                                    onClick={() => {
                                                        setShowSwitchPlatformDisconnectDialog(false);
                                                        disconnect();
                                                    }}
                                                >
                                                    {intl.formatMessage(SreAgentResources.yes)}
                                                </Button>
                                            </DialogTrigger>
                                            <DialogTrigger disableButtonEnhancement>
                                                <Button
                                                    appearance="secondary"
                                                    onClick={() => {
                                                        setShowSwitchPlatformDisconnectDialog(false);
                                                    }}
                                                >
                                                    {intl.formatMessage(SreAgentResources.no)}
                                                </Button>
                                            </DialogTrigger>
                                        </DialogActions>
                                    </DialogBody>
                                </DialogSurface>
                            </Dialog>
                        </div>
                    </>
                )}
            </div>
        </>
    );
};

const IncidentManagementForm: FC<IncidentManagementFormProps> = props => {
    const intl = useIntl();
    const { formikProps, integrated, close, keepOpen } = props;
    const { values, initialValues, dirty } = formikProps;
    const isDirty = useMemo(() => {
        if (
            values.platform !== IncidentManagementPlatform.PagerDuty &&
            values.platform !== IncidentManagementPlatform.ServiceNow &&
            initialValues.platform === values.platform
        ) {
            return false;
        }
        return dirty;
    }, [dirty, values.platform, initialValues.platform]);

    return integrated ? (
        <div
            style={{
                background: tokens.colorNeutralBackground3,
                height: 'calc(100vh - 45px)',
            }}
        >
            <Breadcrumb style={{ display: 'flex', height: 50, marginLeft: 16 }}>
                <BreadcrumbItem>
                    {!keepOpen && close ? (
                        <DirtyStateConfirmationWrapper isDirty={isDirty} onConfirm={close}>
                            <BreadcrumbButton>{intl.formatMessage(IncidentHandlerCreateResources.incidentManagement)}</BreadcrumbButton>
                        </DirtyStateConfirmationWrapper>
                    ) : (
                        intl.formatMessage(IncidentHandlerCreateResources.incidentManagement)
                    )}
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>{intl.formatMessage(SreAgentTabResources.settings)}</BreadcrumbItem>
            </Breadcrumb>
            <div
                style={{
                    borderRadius: tokens.borderRadiusXLarge,
                    boxShadow: tokens.shadow4,
                    marginLeft: 20,
                    marginRight: 20,
                    height: 'calc(100% - 55px)',
                    background: tokens.colorNeutralBackground1,
                    overflowY: 'auto',
                }}
            >
                <div style={{ padding: '2rem' }}>
                    <IncidentManagementFormInner {...props} />
                </div>
            </div>
        </div>
    ) : (
        <IncidentManagementFormInner {...props} />
    );
};

export default IncidentManagementForm;
