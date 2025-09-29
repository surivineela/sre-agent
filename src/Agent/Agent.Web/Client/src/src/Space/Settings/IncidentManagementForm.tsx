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
    Tooltip,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle16Filled } from '@fluentui/react-icons';
import { FC, useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { SpecialControlValue } from '../../Common/AzPortalProxy/Models/IAmplitude';
import { AzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import PermissionedButton from '../../Common/Components/PermissionedButton';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';
import { FirstPartyHelper } from '../../Common/Helpers/FirstPartyHelper';
import { ArmResourceDescriptor } from '../../Common/Helpers/ResourceDescriptors';
import useUserPermissions from '../../Common/Hooks/useUserPermissions';
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
import { IncidentManagementFormProps } from '../Contracts/IncidentManagement';
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
    const isSetupScenario = useMemo(() => initialValues.platform === IncidentManagementType.None, [initialValues.platform]);
    const isApiKeyEditable = useMemo(() => isSetupScenario || editingApiKey, [isSetupScenario, editingApiKey]);

    const location = useLocation();
    const navigate = useNavigate();
    const azPortalContext = useContext(AzPortalContext);
    const {
        incidentManagement: { isIncidentManagementConnected, hasFilters },
    } = useContext(SreAgentContext);
    const { canWriteAgent } = useUserPermissions();

    const incidentPlatformDropdownOptions = useMemo(() => {
        const options = [
            { key: IncidentManagementType.PagerDuty, text: intl.formatMessage(IncidentManagementPlatformResources.pagerDuty) },
            { key: IncidentManagementType.AzMonitor, text: intl.formatMessage(IncidentManagementPlatformResources.azMonitor) },
            { key: IncidentManagementType.ServiceNow, text: intl.formatMessage(IncidentManagementPlatformResources.serviceNow) },
        ];
        const agentTenantId = tenantId ?? '';
        if (FirstPartyHelper.shouldEnableForIcm(agentTenantId)) {
            options.push({ key: IncidentManagementType.Icm, text: intl.formatMessage(IncidentManagementPlatformResources.icm) });
        }
        return options;
    }, [intl, tenantId]);

    const selectedPlatformDisplayName = useMemo(() => {
        const selectedPlatform = incidentPlatformDropdownOptions.find(option => option.key === values.platform);
        return selectedPlatform?.text || '';
    }, [incidentPlatformDropdownOptions, values.platform]);

    const isDirty = useMemo(() => {
        if (
            values.platform !== IncidentManagementType.PagerDuty &&
            values.platform !== IncidentManagementType.ServiceNow &&
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
        if (initialValues.platform === IncidentManagementType.PagerDuty) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(PagerDutyResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(PagerDutyResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(PagerDutyResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(PagerDutyResources.changePlatformConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementType.AzMonitor) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(AzMonitorResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(AzMonitorResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(AzMonitorResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(AzMonitorResources.changePlatformConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementType.Icm) {
            return {
                disconnectConfirmationTitle: intl.formatMessage(IcMResources.disconnectConfirmationTitle),
                disconnectConfirmationMessage: intl.formatMessage(IcMResources.disconnectConfirmationMessage),
                changePlatformConfirmationTitle: intl.formatMessage(IcMResources.changePlatformConfirmationTitle),
                changePlatformConfirmationMessage: intl.formatMessage(IcMResources.changePlatformConfirmationMessage),
            };
        }

        if (initialValues.platform === IncidentManagementType.ServiceNow) {
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
                (values.platform === IncidentManagementType.PagerDuty ||
                    values.platform === IncidentManagementType.Icm ||
                    values.platform === IncidentManagementType.ServiceNow) &&
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
                            {canWriteAgent ? (
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
                                                data?.optionValue !== IncidentManagementType.PagerDuty &&
                                                data?.optionValue !== IncidentManagementType.ServiceNow
                                            ) {
                                                setFieldValue('connectionKey', undefined, false);
                                                setFieldTouched('connectionKey', false, false);
                                            }
                                            // Clear ServiceNow fields when switching away from ServiceNow
                                            if (data?.optionValue !== IncidentManagementType.ServiceNow) {
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
                                    disabled={loading || !!loadFailure || saving || !canWriteAgent}
                                >
                                    {incidentPlatformDropdownOptions.map(option => (
                                        <Option value={option.key} checkIcon={null}>
                                            {option.text}
                                        </Option>
                                    ))}
                                </Dropdown>
                            ) : (
                                <Tooltip
                                    content={intl.formatMessage(SreAgentResources.noPermissionIncidentManagement)}
                                    relationship="label"
                                >
                                    <Dropdown
                                        id="platform"
                                        style={styles.dropdownStyles}
                                        value={selectedPlatformDisplayName}
                                        placeholder={intl.formatMessage(IncidentManagementPlatformResources.disconnected)}
                                        onOptionSelect={() => {}}
                                        disabled={true}
                                    >
                                        {incidentPlatformDropdownOptions.map(option => (
                                            <Option value={option.key} checkIcon={null}>
                                                {option.text}
                                            </Option>
                                        ))}
                                    </Dropdown>
                                </Tooltip>
                            )}
                        </Field>

                        {values.platform === IncidentManagementType.PagerDuty && (
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
                                        disabled={saving || !isApiKeyEditable || !canWriteAgent}
                                        contentAfter={isValidating && !isSubmitting ? <Spinner size={'tiny'} /> : null}
                                    />
                                </Field>
                            </>
                        )}

                        {values.platform === IncidentManagementType.AzMonitor && (
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
                                    <div className={pagerDutyStyles.iconContainer}>
                                        <CheckmarkCircle16Filled
                                            className={pagerDutyStyles.greenCheckIcon}
                                            aria-label={intl.formatMessage(IncidentManagementResources.setUpComplete)}
                                        />
                                        <div>
                                            {!isIncidentManagementConnected
                                                ? intl.formatMessage(AzMonitorResources.addedMessage)
                                                : hasFilters
                                                  ? intl.formatMessage(AzMonitorResources.connectedMessage)
                                                  : intl.formatMessage(AzMonitorResources.connectedMessageWithoutHandlers)}
                                        </div>
                                    </div>
                                )}
                            </>
                        )}

                        {values.platform === IncidentManagementType.Icm && (
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
                                            {intl.formatMessage(SreAgentResources.learnMore)}
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

                        {values.platform === IncidentManagementType.ServiceNow && (
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

                        {values.platform !== IncidentManagementType.None && isSetupScenario && (
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
                                            values.platform === IncidentManagementType.PagerDuty
                                                ? intl.formatMessage(PagerDutyResources.quickstartHandlerDescription)
                                                : values.platform === IncidentManagementType.ServiceNow
                                                  ? intl.formatMessage(ServiceNowResources.quickstartHandlerDescription)
                                                  : values.platform === IncidentManagementType.AzMonitor
                                                    ? intl.formatMessage(AzMonitorResources.quickstartHandlerDescription)
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
                            {(initialValues.platform === IncidentManagementType.PagerDuty ||
                                initialValues.platform === IncidentManagementType.ServiceNow) &&
                                !editingApiKey && (
                                    <PermissionedButton
                                        appearance="secondary"
                                        style={{ borderRadius: 5, marginRight: 10 }}
                                        canPerform={canWriteAgent}
                                        noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionIncidentManagement)}
                                        disabledReason={saving}
                                        onClick={() => {
                                            setEditingApiKey(true);
                                        }}
                                    >
                                        {initialValues.platform === IncidentManagementType.PagerDuty
                                            ? intl.formatMessage(PagerDutyResources.changeKey)
                                            : intl.formatMessage(ServiceNowResources.changeKey)}
                                    </PermissionedButton>
                                )}

                            {(isSetupScenario || editingApiKey) && (
                                <PermissionedButton
                                    appearance="primary"
                                    style={{ borderRadius: 5, marginRight: 10 }}
                                    canPerform={canWriteAgent}
                                    noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionIncidentManagement)}
                                    disabledReason={
                                        !isDirty ||
                                        saving ||
                                        isValidating ||
                                        !isValid ||
                                        (values.platform === IncidentManagementType.PagerDuty && !values.connectionKey) ||
                                        (values.platform === IncidentManagementType.ServiceNow &&
                                            (!values.endpoint || !values.username || !values.password))
                                    }
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
                                >
                                    {intl.formatMessage(SreAgentResources.save)}
                                </PermissionedButton>
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
                                        <PermissionedButton
                                            appearance="secondary"
                                            style={{ borderRadius: 5 }}
                                            canPerform={canWriteAgent}
                                            noPermissionTooltip={intl.formatMessage(SreAgentResources.noPermissionIncidentManagement)}
                                            disabledReason={saving}
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
                                        </PermissionedButton>
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
            values.platform !== IncidentManagementType.PagerDuty &&
            values.platform !== IncidentManagementType.ServiceNow &&
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
