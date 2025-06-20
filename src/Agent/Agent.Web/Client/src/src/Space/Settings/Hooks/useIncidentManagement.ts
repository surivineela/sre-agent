import { FormikErrors } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { ITelemetryInfo } from '../../../Common/AzPortalProxy/Models/ITelemetryInfo';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import SreAgentClient from '../../../Common/Clients/SreAgentClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { Agent, IncidentManagementConfiguration, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import {
    IncidentManagementNotificationResources,
    IncidentManagementSaveErrorResources,
    IncidentManagementValidationResources,
} from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { IncidentManagementFormValues, IncidentManagementPlatform } from '../../Contracts/IncidentManagement';
import { PagerDutyApiKeyValidationResult, validatePagerDutyApiKey } from '../ValidationHelper';
import { useSreAgent } from './useSreAgent';

const getIncidentManagementPlatform = (agent?: ArmObj<Agent>): IncidentManagementPlatform => {
    switch (agent?.properties?.incidentManagementConfiguration?.type) {
        case IncidentManagementType.PagerDuty:
            return IncidentManagementPlatform.PagerDuty;
        case IncidentManagementType.AzMonitor:
            return IncidentManagementPlatform.AzMonitor;
        case IncidentManagementType.Icm:
            return IncidentManagementPlatform.Icm;
        default:
            return IncidentManagementPlatform.Disconnected;
    }
};

const getInitialValues = (agent?: ArmObj<Agent>): IncidentManagementFormValues => {
    return {
        platform: getIncidentManagementPlatform(agent),
        connectionKey: agent?.properties?.incidentManagementConfiguration?.connectionKey,
        createDefaultHandler: !agent?.properties?.incidentManagementConfiguration?.type,
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

export function useIncidentManagement(resourceId: string) {
    const azPortalContext = useContext(AzPortalContext);
    const environmentContext = useContext(EnvironmentContext);
    const intl = useIntl();

    const { agent, agentLoading, agentLoaded, agentLoadFailure } = useSreAgent(resourceId);
    const [saving, setSaving] = useState(false);
    const [saveFailure, setSaveFailure] = useState<string>();

    const sreAgentContext = useContext(SreAgentContext);
    const {
        incidentManagement: { setIsIncidentManagementConnected, setHasFilters },
    } = sreAgentContext;

    const incidentHandlerClient = useMemo(
        () => IncidentHandlerClient.getInstance(environmentContext.sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)),
        [environmentContext.sreAgentEndpoint, azPortalContext]
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

    const validate = useCallback(
        (formValues: IncidentManagementFormValues): Promise<FormikErrors<IncidentManagementFormValues>> => {
            if (formValues.platform !== IncidentManagementPlatform.PagerDuty) {
                validationGuid.current = undefined;
                latestValidationResult.current = {};
                return Promise.resolve(latestValidationResult.current);
            } else if (!formValues.connectionKey) {
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
        },
        [getValidationErrorMessage]
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

            azPortalContext.log({
                action: 'save-incidentManagement',
                actionModifier: 'start',
                logLevel: 'info',
                resourceId,
                data: additionalInfo,
            });

            SreAgentClient.patchAgent(resourceId, {
                properties: {
                    incidentManagementConfiguration: generateIncidentManagementConfiguration(formValues),
                },
            }).then(patchResult => {
                if (!patchResult.metadata.success) {
                    azPortalContext.log({
                        action: 'save-incidentManagement',
                        actionModifier: 'failed',
                        logLevel: 'error',
                        resourceId,
                        data: { ...additionalInfo, error: patchResult.metadata.error },
                    });
                    setSaving(false);
                    setSaveFailure(intl.formatMessage(IncidentManagementSaveErrorResources.configFailure));
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(IncidentManagementNotificationResources.saveFailed, {
                            errorMessage: getErrorMessage(patchResult.metadata.error),
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
                        //To do, once adding /CheckConnectivity for IcM, Will include Icm here as well
                        if (formValues.platform === IncidentManagementPlatform.PagerDuty && formValues.createDefaultHandler) {
                            pollForConnectivity(environmentContext.sreAgentEndpoint, azPortalContext.log.bind(azPortalContext)).then(
                                isConnected => {
                                    if (!isConnected) {
                                        setIsIncidentManagementConnected(false);
                                        setHasFilters(false);
                                        setSaving(false);
                                        setSaveFailure(
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerFailed)
                                        );
                                        setInitialValues({ platform: formValues.platform, createDefaultHandler: false });
                                        azPortalContext.stopNotification(
                                            notificationId,
                                            false,
                                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerFailed)
                                        );
                                    } else {
                                        azPortalContext.log({
                                            action: 'create-defaultHandler',
                                            actionModifier: 'start',
                                            logLevel: 'info',
                                            resourceId,
                                        });
                                        incidentHandlerClient
                                            .createIncidentFilter({
                                                Id: 'quickstart_handler',
                                                Priority: 'P1',
                                                IncidentType: 'incident_default',
                                            })
                                            .then(filterResult => {
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
                                                        intl.formatMessage(
                                                            IncidentManagementNotificationResources.createDefaultHandlerFailed
                                                        )
                                                    );
                                                    azPortalContext.stopNotification(
                                                        notificationId,
                                                        false,
                                                        intl.formatMessage(
                                                            IncidentManagementNotificationResources.createDefaultHandlerFailed
                                                        )
                                                    );
                                                }
                                            });
                                    }
                                }
                            );
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
                        }
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
            environmentContext.sreAgentEndpoint,
            incidentHandlerClient,
            setHasFilters,
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
