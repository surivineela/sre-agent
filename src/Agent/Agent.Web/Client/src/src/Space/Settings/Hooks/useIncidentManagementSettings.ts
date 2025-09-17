import { FormikErrors } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { ITelemetryInfo } from '../../../Common/AzPortalProxy/Models/ITelemetryInfo';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { IncidentFilterDocumentPayload } from '../../../Common/Contracts/Azure/IncidentHandler';
import { Agent, IncidentManagementConfiguration, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import {
    IncidentManagementNotificationResources,
    IncidentManagementSaveErrorResources,
    IncidentManagementValidationResources,
} from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { IncidentManagementFormValues, IncidentManagementPlatform } from '../../Contracts/IncidentManagement';
import {
    PagerDutyApiKeyValidationResult,
    ServiceNowValidationResult,
    validatePagerDutyApiKey,
    validateServiceNowSettings,
} from '../ValidationHelper';

export const getIncidentManagementPlatform = (agent?: ArmObj<Agent>): IncidentManagementPlatform => {
    switch (agent?.properties?.incidentManagementConfiguration?.type) {
        case IncidentManagementType.PagerDuty:
            return IncidentManagementPlatform.PagerDuty;
        case IncidentManagementType.AzMonitor:
            return IncidentManagementPlatform.AzMonitor;
        case IncidentManagementType.Icm:
            return IncidentManagementPlatform.Icm;
        case IncidentManagementType.ServiceNow:
            return IncidentManagementPlatform.ServiceNow;
        default:
            return IncidentManagementPlatform.Disconnected;
    }
};

const getInitialValues = (agent?: ArmObj<Agent>): IncidentManagementFormValues => {
    const config = agent?.properties?.incidentManagementConfiguration;

    return {
        platform: getIncidentManagementPlatform(agent),
        connectionKey: config?.connectionKey,
        createDefaultHandler: !config?.type,
        // ServiceNow specific fields - always empty for security
        endpoint: config?.connectionUrl,
        username: undefined,
        password: undefined,
        instanceName: undefined,
    };
};

const generateIncidentManagementConfiguration = (formValues: IncidentManagementFormValues): IncidentManagementConfiguration | null => {
    switch (formValues.platform) {
        case IncidentManagementPlatform.Disconnected:
            return null;
        case IncidentManagementPlatform.PagerDuty:
            return {
                type: IncidentManagementType.PagerDuty,
                connectionName: 'pagerduty',
                connectionKey: formValues.connectionKey,
            };
        case IncidentManagementPlatform.AzMonitor:
            return {
                type: IncidentManagementType.AzMonitor,
                connectionName: 'azmonitor',
            };
        case IncidentManagementPlatform.Icm:
            return {
                type: IncidentManagementType.Icm,
                connectionName: 'icm',
            };
        case IncidentManagementPlatform.ServiceNow:
            return {
                type: IncidentManagementType.ServiceNow,
                connectionName: 'servicenow',
                connectionUrl: formValues.endpoint,
                connectionKey: JSON.stringify({
                    username: formValues.username,
                    password: formValues.password,
                    instanceName: formValues.instanceName,
                }),
            };
        default:
            throw new Error(`Unknown incident management platform: ${formValues.platform}`);
    }
};

const pollForConnectivity = async (sreAgentEndpoint: string, log: (info: ITelemetryInfo) => void) => {
    const incidentHandlerClient = IncidentHandlerClient.getInstance(sreAgentEndpoint, log);
    let isConnected = false;
    for (let i = 0; i < 120; i++) {
        const connectivityResult = await incidentHandlerClient.checkConnectivity();
        isConnected = connectivityResult?.content ?? false;
        if (isConnected) {
            return true;
        }
        // Wait for 1 second before checking again
        await new Promise(resolve => setTimeout(resolve, 1000));
    }
    return isConnected;
};

export function useIncidentManagement(close: (() => void) | undefined) {
    const azPortalContext = useContext(AzPortalContext);
    const environmentContext = useContext(EnvironmentContext);
    const { resourceId, sreAgentEndpoint } = environmentContext;

    const intl = useIntl();

    const [saving, setSaving] = useState(false);
    const [saveFailure, setSaveFailure] = useState<string>();

    const sreAgentContext = useContext(SreAgentContext);
    const {
        agentObj: agent,
        agentLoading,
        agentLoaded,
        agentLoadFailure,
        patchAgent,
        incidentManagement: { setIsIncidentManagementConnected, setHasFilters },
    } = sreAgentContext;

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [sreAgentEndpoint, azPortalContext]
    );

    const platform: IncidentManagementPlatform = useMemo(() => getIncidentManagementPlatform(agent), [agent]);

    const [initialValues, setInitialValues] = useState<IncidentManagementFormValues>(getInitialValues(agent));

    const validationGuid = useRef<string>();
    const latestValidationResult = useRef<FormikErrors<IncidentManagementFormValues>>({});

    useEffect(() => {
        setInitialValues(getInitialValues(agent));
    }, [agent]);

    const getValidationErrorMessage = useCallback(
        (validationResult: PagerDutyApiKeyValidationResult) => {
            switch (validationResult) {
                case 'validKey':
                    return undefined;
                case 'missingKey':
                    return intl.formatMessage(IncidentManagementValidationResources.apiKeyRequired);
                case 'invalidKey':
                    return intl.formatMessage(IncidentManagementValidationResources.apiKeyInvalid);
                case 'unknownError':
                    return intl.formatMessage(IncidentManagementValidationResources.apiKeyFailedToValidate);
                default:
                    return undefined;
            }
        },
        [intl]
    );

    const getServiceNowValidationErrorMessage = useCallback(
        (validationResult: ServiceNowValidationResult, field: 'endpoint' | 'username' | 'password') => {
            switch (validationResult) {
                case 'valid':
                    return undefined;
                case 'missingEndpoint':
                    return field === 'endpoint'
                        ? intl.formatMessage(IncidentManagementValidationResources.serviceNowEndpointRequired)
                        : undefined;
                case 'missingUsername':
                    return field === 'username'
                        ? intl.formatMessage(IncidentManagementValidationResources.serviceNowUsernameRequired)
                        : undefined;
                case 'missingPassword':
                    return field === 'password'
                        ? intl.formatMessage(IncidentManagementValidationResources.serviceNowPasswordRequired)
                        : undefined;
                case 'invalidCredentials':
                    return field === 'username' || field === 'password'
                        ? intl.formatMessage(IncidentManagementValidationResources.serviceNowInvalidCredentials)
                        : undefined;
                case 'connectionError':
                    return field === 'endpoint'
                        ? intl.formatMessage(IncidentManagementValidationResources.serviceNowConnectionError)
                        : undefined;
                case 'unknownError':
                    return intl.formatMessage(IncidentManagementValidationResources.serviceNowFailedToValidate);
                default:
                    return undefined;
            }
        },
        [intl]
    );

    const validate = useCallback(
        (formValues: IncidentManagementFormValues): Promise<FormikErrors<IncidentManagementFormValues>> => {
            if (formValues.platform === IncidentManagementPlatform.PagerDuty) {
                if (!formValues.connectionKey) {
                    validationGuid.current = undefined;
                    latestValidationResult.current = { connectionKey: getValidationErrorMessage('missingKey') };
                    return Promise.resolve(latestValidationResult.current);
                } else {
                    const guid = Guid.newGuid();
                    validationGuid.current = guid;
                    return validatePagerDutyApiKey(formValues.connectionKey).then(validationResult => {
                        if (validationGuid.current !== guid) {
                            return latestValidationResult.current;
                        }

                        latestValidationResult.current =
                            validationResult === 'validKey' ? {} : { connectionKey: getValidationErrorMessage(validationResult) };
                        return latestValidationResult.current;
                    });
                }
            } else if (formValues.platform === IncidentManagementPlatform.ServiceNow) {
                const errors: FormikErrors<IncidentManagementFormValues> = {};

                if (!formValues.endpoint) {
                    errors.endpoint = intl.formatMessage(IncidentManagementValidationResources.serviceNowEndpointRequired);
                }
                if (!formValues.username) {
                    errors.username = intl.formatMessage(IncidentManagementValidationResources.serviceNowUsernameRequired);
                }
                if (!formValues.password) {
                    errors.password = intl.formatMessage(IncidentManagementValidationResources.serviceNowPasswordRequired);
                }

                // If basic validation fails, return early
                if (Object.keys(errors).length > 0) {
                    validationGuid.current = undefined;
                    latestValidationResult.current = errors;
                    return Promise.resolve(latestValidationResult.current);
                }

                // If all fields are present, validate ServiceNow connection
                if (formValues.endpoint && formValues.username && formValues.password) {
                    const guid = Guid.newGuid();
                    validationGuid.current = guid;
                    return validateServiceNowSettings(formValues.endpoint, formValues.username, formValues.password, sreAgentEndpoint)
                        .then(validationResult => {
                            if (validationGuid.current !== guid) {
                                return latestValidationResult.current;
                            }

                            if (validationResult === 'valid') {
                                latestValidationResult.current = {};
                            } else {
                                // Set the error on the most relevant field based on the validation result
                                const endpointError = getServiceNowValidationErrorMessage(validationResult, 'endpoint');
                                const usernameError = getServiceNowValidationErrorMessage(validationResult, 'username');
                                const passwordError = getServiceNowValidationErrorMessage(validationResult, 'password');

                                latestValidationResult.current = {};
                                if (endpointError) {
                                    latestValidationResult.current.endpoint = endpointError;
                                } else if (usernameError) {
                                    latestValidationResult.current.username = usernameError;
                                } else if (passwordError) {
                                    latestValidationResult.current.password = passwordError;
                                } else {
                                    // Default to endpoint for unknown errors
                                    latestValidationResult.current.endpoint = intl.formatMessage(
                                        IncidentManagementValidationResources.serviceNowFailedToValidate
                                    );
                                }
                            }
                            return latestValidationResult.current;
                        })
                        .catch(() => {
                            if (validationGuid.current !== guid) {
                                return latestValidationResult.current;
                            }
                            latestValidationResult.current = {
                                endpoint: intl.formatMessage(IncidentManagementValidationResources.serviceNowFailedToValidate),
                            };
                            return latestValidationResult.current;
                        });
                }
            } else {
                validationGuid.current = undefined;
                latestValidationResult.current = {};
                return Promise.resolve(latestValidationResult.current);
            }

            return Promise.resolve({});
        },
        [intl, getValidationErrorMessage, getServiceNowValidationErrorMessage, sreAgentEndpoint]
    );

    const save = useCallback(
        (formValues: IncidentManagementFormValues) => {
            if (!agent) {
                return;
            }

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.saveTitle),
                intl.formatMessage(IncidentManagementNotificationResources.saveStarted)
            );

            setSaving(true);
            setSaveFailure(undefined);

            const additionalInfo = {
                platform: formValues.platform,
                previousPlatform: initialValues.platform,
            };

            const amplitudeTargetName =
                formValues.platform === IncidentManagementPlatform.Disconnected
                    ? 'disconnectIncidentPlatform'
                    : `connectTo${formValues.platform}`;
            const amplitudeTargetFriendlyName =
                formValues.platform === IncidentManagementPlatform.Disconnected
                    ? 'Disconnect incident platform'
                    : `Connect to ${formValues.platform}`;
            azPortalContext.logAmplitudeOperationEvent({
                targetType: 'update',
                targetAction: 'start',
                targetName: amplitudeTargetName,
                targetFriendlyName: amplitudeTargetFriendlyName,
                metadata: {
                    ...additionalInfo,
                },
            });

            azPortalContext.log({
                action: 'save-incidentManagement',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId,
                data: additionalInfo,
            });

            patchAgent({
                properties: {
                    incidentManagementConfiguration: generateIncidentManagementConfiguration(formValues),
                },
            }).then(patchResult => {
                const isSuccessful = patchResult.metadata.success;

                azPortalContext.logAmplitudeOperationEvent({
                    targetType: 'update',
                    targetAction: isSuccessful ? 'success' : 'failed',
                    targetName: amplitudeTargetName,
                    targetFriendlyName: amplitudeTargetFriendlyName,
                    metadata: {
                        ...additionalInfo,
                    },
                });

                if (!isSuccessful) {
                    const error = getErrorMessage(patchResult.metadata.error);
                    azPortalContext.log({
                        action: 'save-incidentManagement',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId,
                        data: { ...additionalInfo, error },
                    });
                    setSaving(false);
                    setSaveFailure(intl.formatMessage(IncidentManagementSaveErrorResources.configFailure));
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(IncidentManagementNotificationResources.saveFailed, {
                            errorMessage: error,
                        })
                    );
                } else {
                    azPortalContext.log({
                        action: 'save-incidentManagement',
                        actionModifier: 'success',
                        logLevel: 'info',
                        resourceId,
                        data: additionalInfo,
                    });
                    if (formValues.platform === IncidentManagementPlatform.Disconnected) {
                        setIsIncidentManagementConnected(false);
                        setSaving(false);
                        setSaveFailure(undefined);
                        setInitialValues({ platform: formValues.platform, createDefaultHandler: true });
                        azPortalContext.stopNotification(
                            notificationId,
                            true,
                            intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                        );
                    } else {
                        pollForConnectivity(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)).then(isConnected => {
                            if (!isConnected) {
                                setIsIncidentManagementConnected(false);
                                setHasFilters(false);
                                setSaving(false);
                                setSaveFailure(intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformFailed));
                                setInitialValues({ platform: formValues.platform, createDefaultHandler: false });
                                azPortalContext.stopNotification(
                                    notificationId,
                                    false,
                                    intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformFailed)
                                );
                            } else if (
                                (formValues.platform === IncidentManagementPlatform.PagerDuty ||
                                    formValues.platform === IncidentManagementPlatform.Icm ||
                                    formValues.platform === IncidentManagementPlatform.ServiceNow) &&
                                formValues.createDefaultHandler
                            ) {
                                azPortalContext.log({
                                    action: 'create-defaultHandler',
                                    actionModifier: 'start',
                                    logLevel: 'info',
                                    resourceId,
                                });

                                const defaultIncidentFilter: IncidentFilterDocumentPayload = {
                                    id: 'quickstart_handler',
                                };

                                if (formValues.platform === IncidentManagementPlatform.PagerDuty) {
                                    defaultIncidentFilter.incidentType = 'incident_default';
                                    defaultIncidentFilter.priority = 'P1';
                                }

                                if (formValues.platform === IncidentManagementPlatform.Icm) {
                                    defaultIncidentFilter.incidentType = 'LiveSite';
                                    defaultIncidentFilter.priority = '3';
                                }

                                if (formValues.platform === IncidentManagementPlatform.ServiceNow) {
                                    defaultIncidentFilter.incidentType = 'incident';
                                    defaultIncidentFilter.priority = '1';
                                }
                                incidentHandlerClient.createIncidentFilter(defaultIncidentFilter).then(filterResult => {
                                    setIsIncidentManagementConnected(true);
                                    setSaving(false);
                                    setInitialValues({ platform: formValues.platform, createDefaultHandler: false });
                                    if (filterResult.isSuccessful) {
                                        azPortalContext.log({
                                            action: 'create-defaultHandler',
                                            actionModifier: 'success',
                                            logLevel: 'info',
                                            resourceId,
                                        });
                                        setHasFilters(true);
                                        setSaveFailure(undefined);
                                        close?.();
                                        azPortalContext.stopNotification(
                                            notificationId,
                                            true,
                                            intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                                        );
                                    } else {
                                        azPortalContext.log({
                                            action: 'create-defaultHandler',
                                            actionModifier: 'failed',
                                            logLevel: 'error',
                                            resourceId,
                                        });
                                        setHasFilters(false);
                                        setSaveFailure(
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerFailed)
                                        );
                                        azPortalContext.stopNotification(
                                            notificationId,
                                            false,
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerFailed)
                                        );
                                    }
                                });
                            } else {
                                setIsIncidentManagementConnected(true);
                                setHasFilters(false);
                                setSaving(false);
                                setSaveFailure(undefined);
                                setInitialValues({ platform: formValues.platform, createDefaultHandler: false });
                                azPortalContext.stopNotification(
                                    notificationId,
                                    true,
                                    intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                                );
                                if (formValues.platform !== IncidentManagementPlatform.AzMonitor) {
                                    close?.();
                                }
                            }
                        });
                    }
                }
            });
        },
        [
            agent,
            azPortalContext,
            intl,
            initialValues.platform,
            resourceId,
            setIsIncidentManagementConnected,
            sreAgentEndpoint,
            incidentHandlerClient,
            setHasFilters,
            patchAgent,
            close,
        ]
    );
    const disconnect = useCallback(() => {
        save({ platform: IncidentManagementPlatform.Disconnected, connectionKey: undefined });
    }, [save]);

    return {
        loading: agentLoading,
        loaded: agentLoaded,
        loadFailure: agentLoadFailure,
        saving,
        saveFailure,
        platform,
        initialValues,
        validate,
        save,
        disconnect,
        agent,
    };
}
