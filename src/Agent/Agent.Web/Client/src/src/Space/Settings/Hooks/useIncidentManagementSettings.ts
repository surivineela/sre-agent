import { FormikErrors } from 'formik';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessage } from '../../../Common/Clients/ArmClient';
import { ConnectorService } from '../../../Common/Clients/ConnectorService';
import { getDataPlaneErrorMessage } from '../../../Common/Clients/DataPlaneClient';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { OAuthPopup } from '../../../Common/Clients/OAuthPopupClient';
import { OAuthServiceClient } from '../../../Common/Clients/OAuthService';
import { PermissionClient } from '../../../Common/Clients/PermissionsClient';
import { ArmObj } from '../../../Common/Contracts/Azure/ArmObj';
import { IncidentFilterDocumentPayload } from '../../../Common/Contracts/Azure/IncidentHandler';
import { Agent, AgentMode, IncidentManagementConfiguration, IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { Guid } from '../../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import {
    IncidentManagementNotificationResources,
    IncidentManagementPlatformResources,
    IncidentManagementSaveErrorResources,
    IncidentManagementValidationResources,
} from '../../../Strings/SREAgentResources';
import { SreAgentContext } from '../../Contracts/Context';
import { IncidentManagementFormValues, ServiceNowAuthType } from '../../Contracts/IncidentManagement';
import {
    PagerDutyApiKeyValidationResult,
    ServiceNowValidationResult,
    validatePagerDutyApiKey,
    validateServiceNowSettings,
} from '../ValidationHelper';

// ============================================================================
// ServiceNow OAuth Helper Types and Functions
// ============================================================================

interface OAuthSetupContext {
    subscriptionId: string;
    resourceGroup: string;
    agentName: string;
    location: string;
    connectionName: string;
    instanceName: string;
    userTenantId: string;
    userObjectId: string;
    uamiPrincipalId: string;
    resourceId: string;
    sreAgentEndpoint: string;
}

interface OAuthSetupResult {
    success: boolean;
    errorMessage?: string;
    connectionName?: string;
}

/**
 * Parses the ARM token JWT to extract user identity (tenantId and objectId).
 * Falls back to userInfo from environment context if parsing fails.
 */
const parseUserIdentityFromToken = (
    armToken: string | undefined,
    fallbackUserInfo: { directoryId?: string; objectId?: string } | undefined
): { userTenantId: string; userObjectId: string } => {
    if (armToken) {
        try {
            const base64Url = armToken.split('.')[1];
            if (base64Url) {
                const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
                const jsonPayload = decodeURIComponent(
                    window
                        .atob(base64)
                        .split('')
                        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
                        .join('')
                );
                const tokenClaims = JSON.parse(jsonPayload);
                const userTenantId = tokenClaims.tid || '';
                const userObjectId = tokenClaims.oid || '';
                console.log('Parsed user identity from ARM token:', { userTenantId, userObjectId });
                return { userTenantId, userObjectId };
            }
        } catch (e) {
            console.error('Failed to parse ARM token, falling back to userInfo:', e);
        }
    }
    // Fallback to userInfo
    return {
        userTenantId: fallbackUserInfo?.directoryId || '',
        userObjectId: fallbackUserInfo?.objectId || '',
    };
};

/**
 * Extracts the ServiceNow instance name from a URL.
 * e.g., "https://dev272654.service-now.com" -> "dev272654"
 */
const extractInstanceName = (endpoint: string): string => {
    return endpoint.replace('https://', '').replace('http://', '').replace('.service-now.com', '').replace(/\/$/, '');
};

/**
 * Generates a unique connection name for the API Connection.
 */
const generateConnectionName = (agentName: string): string => {
    const randomSuffix = Math.random().toString(36).substring(2, 12);
    return `${agentName}-servicenow-${randomSuffix}`.toLowerCase();
};

/**
 * Creates the ServiceNow API Connection in Azure.
 */
const createApiConnection = async (
    ctx: Pick<OAuthSetupContext, 'subscriptionId' | 'resourceGroup' | 'connectionName' | 'location' | 'instanceName' | 'resourceId'>,
    clientId: string,
    clientSecret: string
): Promise<OAuthSetupResult> => {
    console.log('Creating ServiceNow API Connection via frontend ARM call...');
    console.log('Connection name:', ctx.connectionName);
    console.log('Instance name:', ctx.instanceName);

    const createResponse = await ConnectorService.putServiceNowOAuthConnector({
        subscriptionId: ctx.subscriptionId,
        resourceGroup: ctx.resourceGroup,
        connectionName: ctx.connectionName,
        location: ctx.location,
        instanceName: ctx.instanceName,
        clientId,
        clientSecret,
        agentResourceId: ctx.resourceId,
    });

    if (!createResponse.metadata.success) {
        const errorMessage =
            createResponse.metadata.error?.Message || createResponse.metadata.error?.message || 'Failed to create API Connection';
        console.error('Failed to create API Connection:', errorMessage);
        return { success: false, errorMessage: `Failed to create API Connection: ${errorMessage}` };
    }

    console.log('API Connection created successfully');
    return { success: true };
};

/**
 * Gets the OAuth consent link for the API Connection.
 */
const getConsentLink = async (
    ctx: Pick<OAuthSetupContext, 'subscriptionId' | 'resourceGroup' | 'connectionName' | 'userTenantId' | 'userObjectId'>
): Promise<{ success: boolean; consentLink?: string; errorMessage?: string }> => {
    console.log('User identity for consent:', { userTenantId: ctx.userTenantId, userObjectId: ctx.userObjectId });

    const consentResponse = await OAuthServiceClient.fetchConsentUrlForConnection({
        subscriptionId: ctx.subscriptionId,
        resourceGroup: ctx.resourceGroup,
        connectionName: ctx.connectionName,
        tenantId: ctx.userTenantId,
        objectId: ctx.userObjectId,
    });

    if (!consentResponse.metadata.success || !consentResponse.data?.value?.[0]?.link) {
        console.error('Failed to get consent link:', consentResponse.metadata.error);
        return { success: false, errorMessage: 'Failed to get OAuth consent link' };
    }

    const consentLink = consentResponse.data.value[0].link;
    console.log('Got consent link:', consentLink);
    return { success: true, consentLink };
};

/**
 * Opens the OAuth popup and handles the authorization flow.
 */
const handleOAuthPopup = async (
    consentLink: string,
    ctx: Pick<OAuthSetupContext, 'subscriptionId' | 'resourceGroup' | 'connectionName' | 'userTenantId' | 'userObjectId'>
): Promise<OAuthSetupResult> => {
    console.log('Opening popup with consent link...');
    const oauthPopup = new OAuthPopup({ consentUrl: consentLink });

    try {
        const loginResponse = await oauthPopup.loginPromise;
        console.log('OAuth popup response:', loginResponse);

        if (loginResponse.error) {
            console.error('OAuth error:', loginResponse.error);
            return { success: false, errorMessage: `OAuth authorization failed: ${atob(loginResponse.error)}` };
        }

        // If a code was returned, confirm it
        if (loginResponse.code) {
            console.log('Confirming consent code...');
            try {
                await OAuthServiceClient.confirmConsentCodeForConnection({
                    subscriptionId: ctx.subscriptionId,
                    resourceGroup: ctx.resourceGroup,
                    connectionName: ctx.connectionName,
                    code: loginResponse.code,
                    tenantId: ctx.userTenantId,
                    objectId: ctx.userObjectId,
                });
            } catch (confirmError) {
                // Code confirmation may fail if Azure already processed it
                console.warn('Code confirmation failed (may already be processed):', confirmError);
            }
        } else {
            console.log('No code in response - Azure consent service already processed the OAuth flow');
        }

        return { success: true };
    } catch (popupError) {
        console.error('OAuth popup error:', popupError);
        return { success: false, errorMessage: 'OAuth popup was closed or timed out' };
    }
};

/**
 * Verifies that the API Connection is authenticated.
 */
const verifyConnectionStatus = async (
    ctx: Pick<OAuthSetupContext, 'subscriptionId' | 'resourceGroup' | 'agentName' | 'connectionName'>
): Promise<OAuthSetupResult> => {
    console.log('Verifying connection authentication status...');
    const connectionNameOnly = ctx.connectionName.replace(`${ctx.agentName}-`, '');

    const connectionResponse = await ConnectorService.getConnector({
        subscriptionId: ctx.subscriptionId,
        resourceGroup: ctx.resourceGroup,
        agentName: ctx.agentName,
        connectionName: connectionNameOnly,
    });

    if (!connectionResponse.metadata.success) {
        console.error('Failed to retrieve connection for verification:', connectionResponse.metadata.error);
        return { success: false, errorMessage: 'Failed to verify connection after OAuth' };
    }

    const connectionStatus = connectionResponse.data?.properties?.statuses?.[0];
    if (connectionStatus?.status !== 'Connected') {
        console.error('Connection is not authenticated. Status:', connectionStatus?.status);
        return { success: false, errorMessage: `Connection authentication failed. Status: ${connectionStatus?.status || 'Unknown'}` };
    }

    console.log('Connection authenticated successfully');
    return { success: true };
};

/**
 * Assigns the Logic App Contributor role to the UAMI on the API Connection.
 */
const assignRoleToUami = async (
    ctx: Pick<OAuthSetupContext, 'subscriptionId' | 'resourceGroup' | 'connectionName' | 'uamiPrincipalId'>
): Promise<void> => {
    const connectionResourceId = `/subscriptions/${ctx.subscriptionId}/resourceGroups/${ctx.resourceGroup}/providers/Microsoft.Web/connections/${ctx.connectionName}`;
    const logicAppContributorRoleId = '87a39d53-fc1b-424a-814c-f7e04687dc9e';

    console.log('Assigning Logic App Contributor role to UAMI on API Connection...');
    console.log('UAMI principal ID:', ctx.uamiPrincipalId);

    try {
        await PermissionClient.getInstance().assignRole(
            connectionResourceId,
            logicAppContributorRoleId,
            ctx.uamiPrincipalId,
            'ServicePrincipal'
        );
        console.log('Role assignment completed successfully');
    } catch (roleError) {
        // Don't fail if role assignment fails - it may already exist
        console.warn('Failed to assign role (may already exist):', roleError);
    }
};

/**
 * Cleans up the API Connection on failure.
 */
const cleanupConnection = async (
    ctx: Pick<OAuthSetupContext, 'subscriptionId' | 'resourceGroup' | 'agentName' | 'connectionName'>
): Promise<void> => {
    console.log('Cleaning up API Connection after failure...');
    const connectionNameOnly = ctx.connectionName.replace(`${ctx.agentName}-`, '');
    try {
        await ConnectorService.deleteConnector({
            subscriptionId: ctx.subscriptionId,
            resourceGroup: ctx.resourceGroup,
            agentName: ctx.agentName,
            connectionName: connectionNameOnly,
        });
        console.log('Connection cleaned up');
    } catch (e) {
        console.warn('Failed to cleanup connection:', e);
    }
};

const getInitialValues = (agent: ArmObj<Agent> | undefined): IncidentManagementFormValues => {
    const config = agent?.properties?.incidentManagementConfiguration;

    // Determine authType based on existing configuration
    // OAuth mode is indicated by apiConnectionName being set
    let authType: ServiceNowAuthType = 'basic';
    let oauthApiConnectionName: string | undefined;

    if (config?.type === IncidentManagementType.ServiceNow) {
        if (config.apiConnectionName) {
            // OAuth: apiConnectionName is set
            authType = 'oauth2';
            oauthApiConnectionName = config.apiConnectionName;
        }
    }

    // For ServiceNow basic auth, parse connectionKey to get username/password
    let basicAuthSettings: { username?: string; password?: string } = {};
    if (config?.type === IncidentManagementType.ServiceNow && authType === 'basic' && config.connectionKey) {
        try {
            basicAuthSettings = JSON.parse(config.connectionKey);
        } catch {
            // connectionKey might be a plain string (legacy basic auth password)
            basicAuthSettings = { password: config.connectionKey };
        }
    }

    return {
        platform: config?.type || IncidentManagementType.None,
        connectionKey: config?.connectionKey,
        createDefaultHandler: !config?.type,
        endpoint: config?.connectionUrl,
        authType: config?.type === IncidentManagementType.ServiceNow ? authType : undefined,
        apiConnectionName: oauthApiConnectionName,
        // OAuth credentials - always empty for security (never show)
        clientId: undefined,
        clientSecret: undefined,
        // Basic auth - password never shown
        username: basicAuthSettings.username,
        password: undefined,
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
            if (formValues.authType === 'oauth2') {
                // OAuth mode: apiConnectionName stored, credentials managed via Azure API Connection
                return {
                    type: IncidentManagementType.ServiceNow,
                    connectionName: 'servicenow',
                    connectionUrl: formValues.endpoint,
                    apiConnectionName: formValues.apiConnectionName,
                };
            } else {
                // Basic auth mode (legacy/backward compatibility)
                return {
                    type: IncidentManagementType.ServiceNow,
                    connectionName: 'servicenow',
                    connectionUrl: formValues.endpoint,
                    connectionKey: JSON.stringify({
                        username: formValues.username,
                        password: formValues.password,
                    }),
                };
            }
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

    // Basic auth validation for ServiceNow (legacy/backward compatibility)
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

                // OAuth mode validation (when authType is 'oauth2')
                if (formValues.authType === 'oauth2') {
                    if (!formValues.clientId) {
                        errors.clientId = intl.formatMessage(IncidentManagementValidationResources.serviceNowClientIdRequired);
                    }
                    if (!formValues.clientSecret) {
                        errors.clientSecret = intl.formatMessage(IncidentManagementValidationResources.serviceNowClientSecretRequired);
                    }

                    // Return only basic validation errors - skip OAuth connection validation
                    // OAuth validation will happen during the setup flow when user clicks Authorize
                    console.log('ServiceNow OAuth validation result:', errors);
                    validationGuid.current = undefined;
                    latestValidationResult.current = errors;
                    return Promise.resolve(latestValidationResult.current);
                } else {
                    // Basic auth mode validation (legacy/backward compatibility)
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
                }

                validationGuid.current = undefined;
                latestValidationResult.current = errors;
                return Promise.resolve(latestValidationResult.current);
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

    // Helper to get platform-specific filter defaults
    const getPlatformFilterDefaults = useCallback(
        (
            platform: IncidentManagementType | undefined,
            owningTeamId?: string
        ): { incidentType?: string; priorities?: string[]; owningTeamId?: string } => {
            switch (platform) {
                case IncidentManagementType.ServiceNow:
                    return { incidentType: 'incident', priorities: ['1'] };
                case IncidentManagementType.PagerDuty:
                    return { incidentType: 'incident_default', priorities: ['P1'] };
                case IncidentManagementType.Icm:
                    return { incidentType: 'LiveSite', priorities: ['3'], owningTeamId };
                case IncidentManagementType.AzMonitor:
                    return { priorities: ['Sev3'] };
                default:
                    return {};
            }
        },
        []
    );

    // Helper to create default filter and handle result
    const createAndHandleDefaultFilter = useCallback(
        (formValues: IncidentManagementFormValues, handlerNotificationId: string, includeAgentMode: boolean = false) => {
            const defaults = getPlatformFilterDefaults(formValues.platform, formValues.owningTeamId);
            const defaultIncidentFilter: IncidentFilterDocumentPayload = {
                id: 'quickstart_handler',
                ...(includeAgentMode && { agentMode: AgentMode.autonomous }),
                ...defaults,
            };

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
        },
        [
            getPlatformFilterDefaults,
            incidentHandlerClient,
            setIsIncidentManagementConnected,
            setHasFilters,
            azPortalContext,
            resourceId,
            intl,
            close,
        ]
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

    // Helper function to setup ServiceNow OAuth credentials
    const setupServiceNowOAuth = useCallback(
        async (formValues: IncidentManagementFormValues): Promise<OAuthSetupResult> => {
            if (!formValues.endpoint || !formValues.clientId || !formValues.clientSecret) {
                return { success: false, errorMessage: 'Missing OAuth credentials' };
            }

            try {
                // Build the OAuth setup context
                const resourceDescriptor = new ArmResourceDescriptor(resourceId);
                const agentName = resourceDescriptor.resourceName;
                const { userTenantId, userObjectId } = parseUserIdentityFromToken(environmentContext.armToken, environmentContext.userInfo);

                const ctx: OAuthSetupContext = {
                    subscriptionId: resourceDescriptor.subscription,
                    resourceGroup: resourceDescriptor.resourceGroup,
                    agentName,
                    location: agent?.location || 'eastus2',
                    connectionName: generateConnectionName(agentName),
                    instanceName: extractInstanceName(formValues.endpoint),
                    userTenantId,
                    userObjectId,
                    uamiPrincipalId: agent?.identity?.principalId || '',
                    resourceId,
                    sreAgentEndpoint,
                };

                // Step 1: Create API Connection
                const createResult = await createApiConnection(ctx, formValues.clientId, formValues.clientSecret);
                if (!createResult.success) {
                    return createResult;
                }

                // Step 2: Get consent link
                const consentResult = await getConsentLink(ctx);
                if (!consentResult.success || !consentResult.consentLink) {
                    await cleanupConnection(ctx);
                    return { success: false, errorMessage: consentResult.errorMessage };
                }

                // Step 3: Open consent popup and handle authorization
                const popupResult = await handleOAuthPopup(consentResult.consentLink, ctx);
                if (!popupResult.success) {
                    return popupResult;
                }

                // Step 4: Verify the connection is authenticated
                const verifyResult = await verifyConnectionStatus(ctx);
                if (!verifyResult.success) {
                    return verifyResult;
                }

                // Step 5: Assign role to UAMI for managed identity access
                await assignRoleToUami(ctx);

                console.log('OAuth completed successfully');
                return { success: true, connectionName: ctx.connectionName };
            } catch (error) {
                console.error('ServiceNow OAuth setup error:', error);
                return {
                    success: false,
                    errorMessage: `Failed to setup OAuth credentials: ${error instanceof Error ? error.message : 'Unknown error'}`,
                };
            }
        },
        [
            resourceId,
            agent?.location,
            agent?.identity?.principalId,
            sreAgentEndpoint,
            environmentContext.armToken,
            environmentContext.userInfo,
        ]
    );

    // Helper function to disconnect ServiceNow OAuth (delete API Connection)
    const disconnectServiceNowOAuth = useCallback(async (): Promise<{ success: boolean; errorMessage?: string }> => {
        try {
            const config = agent?.properties?.incidentManagementConfiguration;

            // Only call disconnect if we have an API Connection to delete
            if (config?.type !== IncidentManagementType.ServiceNow || !config.apiConnectionName) {
                return { success: true }; // Nothing to disconnect
            }

            const resourceDescriptor = new ArmResourceDescriptor(resourceId);
            const subscriptionId = resourceDescriptor.subscription;
            const resourceGroup = resourceDescriptor.resourceGroup;
            const agentName = resourceDescriptor.resourceName;

            // Use the connection name from agent properties (full name with agent prefix)
            const fullConnectionName = config.apiConnectionName;

            console.log('Disconnecting ServiceNow OAuth connection:', fullConnectionName);

            // Extract connection name part (remove agent prefix)
            const connectionNameOnly = fullConnectionName.replace(`${agentName}-`, '');

            // Delete using frontend ARM call (user's credentials)
            const response = await ConnectorService.deleteConnector({
                subscriptionId,
                resourceGroup,
                agentName,
                connectionName: connectionNameOnly,
            });

            if (!response.metadata.success) {
                // Don't fail if the connection doesn't exist (404)
                if (response.metadata.status !== 404) {
                    console.error('Failed to delete API Connection:', response.metadata.error);
                    return {
                        success: false,
                        errorMessage: response.metadata.error?.message || 'Failed to delete API Connection',
                    };
                }
            }

            console.log('Successfully deleted ServiceNow API Connection');
            return { success: true };
        } catch (error) {
            console.error('Error disconnecting ServiceNow OAuth:', error);
            // Don't block the disconnect flow if API Connection deletion fails
            // The connection may have already been deleted or may not exist
            return { success: true };
        }
    }, [agent, resourceId]);

    // Helper function to cleanup a pending OAuth connection (when user cancels after authorizing but before saving)
    const cleanupPendingOAuthConnection = useCallback(
        async (connectionName: string): Promise<void> => {
            if (!connectionName) return;

            console.log('Cleaning up pending OAuth connection:', connectionName);

            try {
                const resourceDescriptor = new ArmResourceDescriptor(resourceId);
                const subscriptionId = resourceDescriptor.subscription;
                const resourceGroup = resourceDescriptor.resourceGroup;
                const agentName = resourceDescriptor.resourceName;

                // Extract connection name part (remove agent prefix if present)
                const connectionNameOnly = connectionName.replace(`${agentName}-`, '');

                await ConnectorService.deleteConnector({
                    subscriptionId,
                    resourceGroup,
                    agentName,
                    connectionName: connectionNameOnly,
                });

                console.log('Successfully cleaned up pending OAuth connection');
            } catch (error) {
                console.warn('Failed to cleanup pending OAuth connection:', error);
            }
        },
        [resourceId]
    );

    const save = useCallback(
        (formValues: IncidentManagementFormValues) => {
            console.log('Save called with formValues:', formValues);

            if (!agent) {
                console.log('No agent, returning');
                return;
            }

            const configNotificationId = azPortalContext.startNotification(
                intl.formatMessage(IncidentManagementNotificationResources.saveTitle),
                intl.formatMessage(IncidentManagementNotificationResources.saveInProgress)
            );

            setSaving(true);
            setSaveFailure(undefined);

            // Special handling for ServiceNow OAuth
            // If authType is oauth2 and we have an apiConnectionName, just save the config (OAuth already authorized)
            // Otherwise, for basic auth, proceed with normal save
            if (formValues.platform === IncidentManagementType.ServiceNow && formValues.authType === 'oauth2') {
                // OAuth mode - apiConnectionName should already be set from the Authorize flow
                if (!formValues.apiConnectionName) {
                    console.error('OAuth save called without apiConnectionName - authorization not complete');
                    setSaving(false);
                    setSaveFailure('Please complete OAuth authorization first');
                    azPortalContext.stopNotification(configNotificationId, false, 'Please complete OAuth authorization first');
                    return;
                }

                console.log('Saving ServiceNow OAuth configuration...');
                console.log('apiConnectionName:', formValues.apiConnectionName);
                console.log('Generated config:', JSON.stringify(generateIncidentManagementConfiguration(formValues), null, 2));
                if (agent) {
                    patchAgent({
                        properties: {
                            incidentManagementConfiguration: generateIncidentManagementConfiguration(formValues),
                        },
                    }).then(patchResult => {
                        const isSuccessful = patchResult.metadata.success;
                        if (!isSuccessful) {
                            const error = getErrorMessage(patchResult.metadata.error);
                            azPortalContext.log({
                                action: 'save-incidentManagement',
                                actionModifier: 'failed',
                                logLevel: 'error',
                                resourceId,
                            });
                            setSaving(false);
                            setSaveFailure(intl.formatMessage(IncidentManagementSaveErrorResources.configFailure));
                            azPortalContext.stopNotification(
                                configNotificationId,
                                false,
                                intl.formatMessage(IncidentManagementNotificationResources.saveFailed, { errorMessage: error })
                            );
                        } else {
                            azPortalContext.log({
                                action: 'save-incidentManagement',
                                actionModifier: 'success',
                                logLevel: 'info',
                                resourceId,
                            });
                            setSaving(false);
                            setSaveFailure(undefined);
                            setIsIncidentManagementConnected(true);
                            azPortalContext.stopNotification(
                                configNotificationId,
                                true,
                                intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                            );
                            close?.();
                        }
                    });
                } else {
                    // No agent object in local dev - just complete the flow
                    setSaving(false);
                    setSaveFailure(undefined);
                    azPortalContext.stopNotification(configNotificationId, true, 'ServiceNow OAuth connection configured successfully');
                    close?.();
                }
                return;
            }

            // Special handling for disconnecting ServiceNow OAuth - delete API Connection first
            const previousConfig = agent?.properties?.incidentManagementConfiguration;
            const isDisconnectingServiceNowOAuth =
                formValues.platform === IncidentManagementType.None &&
                previousConfig?.type === IncidentManagementType.ServiceNow &&
                previousConfig?.apiConnectionName;

            if (isDisconnectingServiceNowOAuth) {
                console.log('Disconnecting ServiceNow OAuth...');
                disconnectServiceNowOAuth().then(disconnectResult => {
                    if (!disconnectResult.success) {
                        // Log but don't block - API Connection may already be deleted
                        console.warn('Failed to delete API Connection:', disconnectResult.errorMessage);
                        azPortalContext.log({
                            action: 'disconnect-servicenow-oauth',
                            actionModifier: 'warning',
                            logLevel: 'warning',
                            resourceId,
                            data: { error: disconnectResult.errorMessage },
                        });
                    } else {
                        console.log('ServiceNow API Connection deleted successfully');
                        azPortalContext.log({
                            action: 'disconnect-servicenow-oauth',
                            actionModifier: 'success',
                            logLevel: 'info',
                            resourceId,
                        });
                    }

                    // Now patch the agent to remove the incident management configuration
                    patchAgent({
                        properties: {
                            incidentManagementConfiguration: null,
                        },
                    }).then(patchResult => {
                        const isSuccessful = patchResult.metadata.success;

                        if (!isSuccessful) {
                            const error = getErrorMessage(patchResult.metadata.error);
                            azPortalContext.log({
                                action: 'save-incidentManagement',
                                actionModifier: 'failed',
                                logLevel: 'error',
                                resourceId,
                                data: { error },
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
                            });
                            azPortalContext.stopNotification(
                                configNotificationId,
                                true,
                                intl.formatMessage(IncidentManagementNotificationResources.saveSucceeded)
                            );
                            setIsIncidentManagementConnected(false);
                            setSaving(false);
                            setSaveFailure(undefined);
                            setInitialValues({ platform: IncidentManagementType.None, createDefaultHandler: true });
                        }
                    });
                });
                return;
            }

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
                    } else {
                        const handlerNotificationId = azPortalContext.startNotification(
                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerTitle),
                            intl.formatMessage(IncidentManagementNotificationResources.createDefaultHandlerInProgress)
                        );

                        createAndHandleDefaultFilter(formValues, handlerNotificationId, true);

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

                                createAndHandleDefaultFilter(formValues, handlerNotificationId);
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
            setHasFilters,
            patchAgent,
            close,
            getPlatformName,
            waitForConnectivity,
            setupServiceNowOAuth,
            disconnectServiceNowOAuth,
            createAndHandleDefaultFilter,
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
        setupServiceNowOAuth,
        cleanupPendingOAuthConnection,
    };
}
