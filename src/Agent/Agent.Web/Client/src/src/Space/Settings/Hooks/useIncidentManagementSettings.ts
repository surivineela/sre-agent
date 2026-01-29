import { FormikErrors } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { getDataPlaneErrorMessage } from '../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { IncidentFilterDocumentPayload } from '../../../Common/Contracts/Azure/IncidentHandler';
import { Agent, AgentMode, IncidentManagementConfiguration, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import {
    IncidentManagementNotificationResources,
    IncidentManagementPlatformResources,
    IncidentManagementSaveErrorResources,
    IncidentManagementValidationResources,
} from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { IncidentManagementFormValues } from '../../Contracts/IncidentManagement';
import {
    PagerDutyApiKeyValidationResult,
    ServiceNowValidationResult,
    validatePagerDutyApiKey,
    validateServiceNowSettings,
} from '../ValidationHelper';

const getInitialValues = (agent?: ArmObj<Agent>): IncidentManagementFormValues => {
    const config = agent?.properties?.incidentManagementConfiguration;

    return {
        platform: config?.type || IncidentManagementType.None,
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
        case IncidentManagementType.None:
            return null;
        case IncidentManagementType.PagerDuty:
            return {
                type: IncidentManagementType.PagerDuty,
                connectionName: 'pagerduty',
                connectionKey: formValues.connectionKey,
            };
        case IncidentManagementType.AzMonitor:
            return {
                type: IncidentManagementType.AzMonitor,
                connectionName: 'azmonitor',
            };
        case IncidentManagementType.Icm:
            return {
                type: IncidentManagementType.Icm,
                connectionName: 'icm',
            };
        case IncidentManagementType.ServiceNow:
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

export function useIncidentManagementSettings(close: (() => void) | undefined) {
    const azPortalContext = useContext(AzPortalContext);
    const environmentContext = useContext(EnvironmentContext);
    const { resourceId, sreAgentEndpoint } = environmentContext;

    const intl = useIntl();

    const [saving, setSaving] = useState(false);
    const [saveFailure, setSaveFailure] = useState<string>();

    const {
        agentObj: agent,
        agentLoading,
        agentLoaded,
        agentLoadFailure,
        patchAgent,
        incidentManagement: { incidentManagementConnectionState, setIsIncidentManagementConnected, setHasFilters },
    } = useContext(SreAgentContext);

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [sreAgentEndpoint, azPortalContext]
    );

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
            if (formValues.platform === IncidentManagementType.PagerDuty) {
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
            } else if (formValues.platform === IncidentManagementType.ServiceNow) {
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
            } else if (formValues.platform === IncidentManagementType.Icm) {
                const errors: FormikErrors<IncidentManagementFormValues> = {};

                if (formValues.createDefaultHandler && !formValues.owningTeamId) {
                    errors.owningTeamId = intl.formatMessage(IncidentManagementValidationResources.icmOwningTeamIdRequired);
                }

                validationGuid.current = undefined;
                latestValidationResult.current = errors;
                return Promise.resolve(latestValidationResult.current);
            } else {
                validationGuid.current = undefined;
                latestValidationResult.current = {};
                return Promise.resolve(latestValidationResult.current);
            }

            return Promise.resolve({});
        },
        [intl, getValidationErrorMessage, getServiceNowValidationErrorMessage, sreAgentEndpoint]
    );

    const getPlatformName = useCallback(
        (platform?: IncidentManagementType) => {
            switch (platform) {
                case IncidentManagementType.PagerDuty:
                    return intl.formatMessage(IncidentManagementPlatformResources.pagerDuty);
                case IncidentManagementType.AzMonitor:
                    return intl.formatMessage(IncidentManagementPlatformResources.azMonitor);
                case IncidentManagementType.Icm:
                    return intl.formatMessage(IncidentManagementPlatformResources.icm);
                case IncidentManagementType.ServiceNow:
                    return intl.formatMessage(IncidentManagementPlatformResources.serviceNow);
                default:
                    return undefined;
            }
        },
        [intl]
    );

    const incidentManagementConnectionStateRef = useRef(incidentManagementConnectionState);

    useEffect(() => {
        incidentManagementConnectionStateRef.current = incidentManagementConnectionState;
    }, [incidentManagementConnectionState]);

    const waitForConnectivity = useCallback(async () => {
        for (let i = 0; i < 120; i++) {
            await new Promise(resolve => setTimeout(resolve, 1000));
            if (incidentManagementConnectionStateRef.current === 'connected') {
                return true;
            }
            if (incidentManagementConnectionStateRef.current === 'notConnected') {
                return false;
            }
        }
        return false;
    }, []);

    const save = useCallback(
        (formValues: IncidentManagementFormValues) => {
            if (!agent) {
                return;
            }

            const configNotificationId = azPortalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.saveTitle),
                intl.formatMessage(IncidentManagementNotificationResources.saveInProgress)
            );

            setSaving(true);
            setSaveFailure(undefined);

            const additionalInfo = {
                platform: formValues.platform,
                previousPlatform: initialValues.platform,
            };

            const amplitudeTargetName =
                formValues.platform === IncidentManagementType.None ? 'disconnectIncidentPlatform' : `connectTo${formValues.platform}`;
            const amplitudeTargetFriendlyName =
                formValues.platform === IncidentManagementType.None ? 'Disconnect incident platform' : `Connect to ${formValues.platform}`;
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
                        configNotificationId,
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
                    azPortalContext.stopNotification(
                        configNotificationId,
                        true,
                        intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                    );
                    if (formValues.platform === IncidentManagementType.None) {
                        setIsIncidentManagementConnected(false);
                        setSaving(false);
                        setSaveFailure(undefined);
                        setInitialValues({ platform: formValues.platform, createDefaultHandler: true });
                    } else {
                        azPortalContext.log({
                            action: 'poll-incidentManagement-connectivity-onSetup',
                            actionModifier: 'start',
                            logLevel: 'info',
                            resourceId,
                            data: { ...additionalInfo },
                        });

                        const platformName = getPlatformName(formValues.platform);
                        const connectingNotificationId = azPortalContext.startNotification(
                            intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformTitle, { platformName }),
                            intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformInProgress, { platformName })
                        );
                        waitForConnectivity().then(isConnected => {
                            azPortalContext.log({
                                action: 'poll-incidentManagement-connectivity-onSetup',
                                actionModifier: isConnected ? 'success' : 'failed',
                                logLevel: isConnected ? 'info' : 'error',
                                resourceId,
                                data: { ...additionalInfo },
                            });
                            azPortalContext.stopNotification(
                                connectingNotificationId,
                                isConnected,
                                isConnected
                                    ? intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformSuccess, {
                                          platformName,
                                      })
                                    : intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformFailed, {
                                          platformName,
                                      })
                            );

                            if (!isConnected) {
                                setIsIncidentManagementConnected(false);
                                setHasFilters(false);
                                setSaving(false);
                                setSaveFailure(intl.formatMessage(IncidentManagementNotificationResources.connectionToPlatformFailed));
                                setInitialValues({ platform: formValues.platform, createDefaultHandler: false });
                            } else if (formValues.createDefaultHandler) {
                                azPortalContext.log({
                                    action: 'create-defaultHandler',
                                    actionModifier: 'start',
                                    logLevel: 'info',
                                    resourceId,
                                });
                                const handlerNotificationId = azPortalContext.startNotification(
                                    intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerTitle),
                                    intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerInProgress)
                                );

                                const defaultIncidentFilter: IncidentFilterDocumentPayload = {
                                    id: 'quickstart_handler',
                                    agentMode: AgentMode.autonomous,
                                };

                                if (formValues.platform === IncidentManagementType.PagerDuty) {
                                    defaultIncidentFilter.incidentType = 'incident_default';
                                    defaultIncidentFilter.priorities = ['P1'];
                                }

                                if (formValues.platform === IncidentManagementType.Icm) {
                                    defaultIncidentFilter.incidentType = 'LiveSite';
                                    defaultIncidentFilter.priorities = ['3'];
                                    defaultIncidentFilter.owningTeamId = formValues.owningTeamId;
                                }

                                if (formValues.platform === IncidentManagementType.ServiceNow) {
                                    defaultIncidentFilter.incidentType = 'incident';
                                    defaultIncidentFilter.priorities = ['1'];
                                }

                                if (formValues.platform === IncidentManagementType.AzMonitor) {
                                    defaultIncidentFilter.priorities = ['Sev2'];
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
                                            handlerNotificationId,
                                            true,
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerSuccess)
                                        );
                                    } else {
                                        const errorMessage = getDataPlaneErrorMessage(filterResult.error);
                                        azPortalContext.log({
                                            action: 'create-defaultHandler',
                                            actionModifier: 'failed',
                                            logLevel: 'error',
                                            resourceId,
                                        });
                                        setHasFilters(false);
                                        setSaveFailure(
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerFailed, {
                                                errorMessage,
                                            })
                                        );
                                        azPortalContext.stopNotification(
                                            handlerNotificationId,
                                            false,
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerFailed, {
                                                errorMessage,
                                            })
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
                                    configNotificationId,
                                    true,
                                    intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                                );
                                close?.();
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
            incidentHandlerClient,
            setHasFilters,
            patchAgent,
            close,
            getPlatformName,
            waitForConnectivity,
        ]
    );
    const disconnect = useCallback(() => {
        save({ platform: IncidentManagementType.None, connectionKey: undefined });
    }, [save]);

    return {
        loading: agentLoading,
        loaded: agentLoaded,
        loadFailure: agentLoadFailure,
        saving,
        saveFailure,
        platform: agent?.properties?.incidentManagementConfiguration?.type || IncidentManagementType.None,
        initialValues,
        validate,
        save,
        disconnect,
        agent,
    };
}
