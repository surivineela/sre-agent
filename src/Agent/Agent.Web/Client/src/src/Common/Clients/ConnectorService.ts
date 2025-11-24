import { HttpResponseObject } from '../ArmHelper.types';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import MakeArmCall from './ArmClient';

interface ConnectionParameterMetadata {
    sourceType?: string;
    value?: string;
}

type LiteralBoolean = 'true' | 'false';

interface ConnectionParameterAllowedValue {
    text?: string;
    value: any;
}

interface ParameterCredentialMapping {
    /**
     * The type of credential supported by this parameter.
     */
    type: string;
    /**
     * Ordinal value of the type enum.
     */
    typeEnumValue: number;
    /**
     * The key used to read the expected value from the credential for this parameter.
     */
    credentialKeyName: string;
}

interface ParameterCredentialMappingUIDefinition {
    /**
     * Name (identifier) of the mapping, parameters with the same mappingName belong to the same credentials item.
     */
    mappingName: string;
    displayName?: string | null;
    tooltip?: string | null;
    values: ParameterCredentialMapping[];
}

interface ConnectionParameterUIDefinitionBase {
    constraints?: {
        dependentCapability?: string;

        /**
         * @member {'true' | 'false'} [required='false'] - 'true' if the parameter's value is required.
         */
        required?: LiteralBoolean;

        allowedValues?: ConnectionParameterAllowedValue[];
        capability?: string[];
        tabIndex?: number;
        clearText?: boolean;
        /**
         * @member {'true' | 'false'} [required='false'] - 'true' if the parameter's value is hidden and should be hidden/ignored from UI and connection creation payload.
         */
        hidden?: LiteralBoolean;
        location?: string;
        propertyPath?: string[];
        hideInUI?: string;
        editor?: string;
        default?: any;
        dependentParameter?: {
            parameter: string;
            value: any;
        };
        requiresConnectionNamePrefix?: string;
        notSupportedConnectionParameters?: Record<string, string[]>;
    };
    tooltip?: string;
    /**
     * Describes if the parameter belongs to a credentials mapping.
     */
    credentialMapping?: ParameterCredentialMappingUIDefinition;
}

interface ParameterSchema {
    type?: string;
    format?: string;
    description?: string;
    'x-ms-editor'?: string;
    'x-ms-editor-options'?: any;
}

interface ConnectionParameterUIDefinition extends ConnectionParameterUIDefinitionBase {
    description?: string;
    displayName?: string;
    schema?: ParameterSchema;
}

interface ManagedIdentitySetting {
    resourceUri: string;
    additionalResourceUris?: string[];
    requiredRoles?: string[];
}

interface OAuthSettingProperties {
    AzureActiveDirectoryResourceId?: string;
    IsFirstParty: string;
}

interface OAuthSetting {
    clientId: string;
    identityProvider: string;
    scopes: string[];
    redirectMode?: string;
    redirectUrl: string;
    properties: OAuthSettingProperties;
    customParameters?: Record<string, any>;
}

interface GatewaySetting {
    dataSourceType?: string;
    connectionDetails?: string[];
}

const ConnectionParameterSource = {
    AppConfiguration: 'AppConfiguration',
};
type ConnectionParameterSource = (typeof ConnectionParameterSource)[keyof typeof ConnectionParameterSource];

interface ConnectionParameter {
    type: string;
    metadata?: ConnectionParameterMetadata;
    uiDefinition?: ConnectionParameterUIDefinition;
    managedIdentitySettings?: ManagedIdentitySetting;
    oAuthSettings?: OAuthSetting;
    gateway?: GatewaySetting;
    parameterSource?: ConnectionParameterSource;
}

interface ConnectionParameterSetUIDefinition {
    description: string;
    tooltip?: string;
    displayName: string;
}

interface ConnectionParameterSetParameterUIDefinition extends ConnectionParameterUIDefinitionBase {
    description: string;
    displayName: string;
    schema?: ParameterSchema;
}

interface ConnectionParameterSetParameter {
    type: string;
    uiDefinition: ConnectionParameterSetParameterUIDefinition;
    managedIdentitySettings?: ManagedIdentitySetting;
    oAuthSettings?: OAuthSetting;
    gateway?: GatewaySetting;
    parameterSource?: ConnectionParameterSource;
    allowedValues?: { value: string }[];
}

interface ConnectionParameterSet {
    name: string;
    uiDefinition: ConnectionParameterSetUIDefinition;
    parameters: Record<string, ConnectionParameterSetParameter>;
}

export interface TestConnectionObject {
    body?: string;
    method: string;
    requestUri: string;
}

interface ConnectionAuthenticatedUser {
    name?: string;
    objectId?: string;
    tenantId?: string;
}

interface Principal {
    displayName: string;
    email: string;
    id: string;
    type: string;
}

interface ConnectionParameterValues {
    authType?: string;
    gateway?: {
        id: string;
        name: string;
        type: string;
    };
}

interface ConnectionStatusError {
    code: string;
    message: string;
}

interface ConnectionStatus {
    error?: ConnectionStatusError;
    status: string;
    target?: string;
}

interface Api {
    name: string;
    displayName: string;
    description: string;
    iconUri: string;
    brandColor: string;
    category: string;
    id: string;
    type: string;
}

type ConnectionFeatureType = 'DynamicUserInvoked'; // Currently only this value is supported but this can be extended in the future

export interface ConnectorProperties {
    authenticatedUser?: ConnectionAuthenticatedUser;
    connectionParameters?: Record<string, ConnectionParameter>;
    connectionParametersSet?: ConnectionParameterSet;
    createdBy?: Principal;
    createdTime: string;
    displayName: string;
    overallStatus: string;
    parameterValues?: ConnectionParameterValues;
    parameterValueType?: string;
    statuses: ConnectionStatus[];
    testLinks?: TestConnectionObject[];
    testRequests?: TestConnectionObject[];
    accountName?: string;
    api: Api;
    connectionRuntimeUrl?: string;
    dynamicConnectionProxyUrl?: string;
    feature?: ConnectionFeatureType;
}

export type Connector = ArmObj<ConnectorProperties>;

export interface GetConnectorOptions {
    subscriptionId: string;
    resourceGroup: string;
    agentName: string;
    connectionName: string;
    apiVersion?: string;
}

export interface PutConnectorOptions {
    subscriptionId: string;
    resourceGroup: string;
    agentName: string;
    connectionName: string;
    location: string;
    apiVersion?: string;
}

export interface GetConnectorAccessPoliciesOptions {
    subscriptionId: string;
    resourceGroup: string;
    connectionName: string;
    agentName: string;
    apiVersion?: string;
}

export interface PutConnectorAccessPoliciesOptions {
    subscriptionId: string;
    resourceGroup: string;
    connectionName: string;
    agentName: string;
    tenantId: string;
    objectId: string;
    location: string;
    apiVersion?: string;
}

export class ConnectorService {
    public static async getConnector(options: GetConnectorOptions): Promise<HttpResponseObject<Connector>> {
        const { subscriptionId, resourceGroup, agentName, connectionName, apiVersion = '2018-07-01-preview' } = options;
        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${agentName}-${connectionName}?api-version=${apiVersion}`;

        return MakeArmCall({
            commandName: 'getConnector',
            url,
            apiVersion,
        });
    }

    public static async putConnector(options: PutConnectorOptions): Promise<HttpResponseObject<Connector>> {
        const { subscriptionId, resourceGroup, agentName, connectionName, location, apiVersion = '2018-07-01-preview' } = options;
        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${agentName}-${connectionName}?api-version=${apiVersion}`;

        const content = {
            kind: 'V2',
            name: `${agentName}-${connectionName}`,
            location: location,
            properties: {
                api: {
                    id: `/subscriptions/${subscriptionId}/providers/Microsoft.Web/locations/${location}/managedApis/${connectionName}`,
                },
                parameterValues: {},
            },
        };

        return MakeArmCall({
            commandName: 'putConnector',
            method: 'PUT',
            body: content,
            url,
            apiVersion,
        });
    }

    public static async deleteConnector(options: GetConnectorOptions): Promise<HttpResponseObject<Connector>> {
        const { subscriptionId, resourceGroup, agentName, connectionName, apiVersion = '2018-07-01-preview' } = options;
        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${agentName}-${connectionName}?api-version=${apiVersion}`;

        return MakeArmCall({
            commandName: 'deleteConnector',
            method: 'DELETE',
            url,
            apiVersion,
        });
    }

    public static async getConnectorAccessPolicies(options: GetConnectorAccessPoliciesOptions) {
        const { subscriptionId, resourceGroup, connectionName, agentName, apiVersion = '2018-07-01-preview' } = options;

        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${agentName}-${connectionName}/accessPolicies?api-version=${apiVersion}`;

        return MakeArmCall({
            commandName: 'getConnectorAccessPolicies',
            method: 'GET',
            url,
            apiVersion,
        });
    }

    public static async putConnectorAccessPolicies(options: PutConnectorAccessPoliciesOptions): Promise<HttpResponseObject<Connector>> {
        const {
            subscriptionId,
            resourceGroup,
            connectionName,
            agentName,
            tenantId,
            objectId,
            location,
            apiVersion = '2018-07-01-preview',
        } = options;

        const policyName = `${agentName}-${connectionName}-${objectId}`;
        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${agentName}-${connectionName}/accessPolicies/${policyName}?api-version=${apiVersion}`;

        const content = {
            name: policyName,
            type: 'Microsoft.Web/connections/accessPolicy',
            location,
            properties: {
                principal: {
                    type: 'ActiveDirectory',
                    identity: { objectId, tenantId },
                },
            },
        };

        return MakeArmCall({
            commandName: 'putConnectorAccessPolicies',
            method: 'PUT',
            body: content,
            url,
            apiVersion,
        });
    }
}
