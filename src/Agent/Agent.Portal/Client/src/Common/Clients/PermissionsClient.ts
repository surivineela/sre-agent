import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ArmArray, ArmObj } from '../Contracts/Arm';
import {
    DenyAssignment,
    Lock,
    Permissions,
    PermissionsCheckResponse,
    PolicyAssignment,
    RoleAssignment,
    RoleDefinition,
} from '../Contracts/Permissions';
import { Response } from '../Contracts/Response';
import { parseArmId } from '../Utilities/ArmId';
import { newGuid } from '../Utilities/Guid';
import { ArmClient } from './ArmClient';
import { Client } from './Client';

interface PermissionsAsRegExp {
    actions: RegExp[];
    notActions: RegExp[];
}

const isAllowed = (requestedAction: string, permission: PermissionsAsRegExp): boolean => {
    const actionAllowed = permission.actions.some(action => {
        return action.test(requestedAction);
    });
    const actionDenied = permission.notActions.some(notAction => {
        return notAction.test(requestedAction);
    });

    return actionAllowed && !actionDenied;
};

const wildCardEscapeSequence = '\\*';

// Escape reserved regex characters so that they are not interpreted by regex evaluation.
/*
 * 1. All allowed character escapes are taken into account: \*, \t, \n, \r, \\, \'
 *    a. \0 is explicitly not supported
 * 2. All non-escaped wildcards match 0 or more characters of anything
 * 3. The entire wildcard pattern is matched from beginning to end, and no more (e.g., a*d matches add but not adding or bad).
 * 4. The pattern matching should be case insensitive.
 */
const escapeRegExp = (regex: string): string => {
    return regex
        .replace(/([.*+?^=!:${}()|[\]/\\])/g, '\\$1')
        .replace('\\/\\*\\/', '\\/?\\*\\/') // first make any / before a wildcard \\* and a slash / optional but leave the wildcard escaped.
        .replace(wildCardEscapeSequence, '.*') // the previous command escaped legitimate wildcards - replace them with Regex wildcards
        .replace('\x00', wildCardEscapeSequence) // replace sentinels with truly escaped wildcards
        .replace('\\t', '\t') // tabs
        .replace('\\n', '\n') // newlines
        .replace('\\r', '\r') // carriage returns
        .replace('\\\\', '\\') // backslashes
        .replace("\\'", "'"); // single quotes
};

const permissionsToRegExp = (permissions: Permissions): PermissionsAsRegExp => {
    const actions = permissions.actions.map(val => {
        return actionToRegExp(val);
    });
    const notActions = permissions.notActions.map(val => {
        return actionToRegExp(val);
    });

    return {
        actions: actions,
        notActions: notActions,
    };
};

const actionToRegExp = (wildCardPattern: string): RegExp => {
    wildCardPattern = wildCardPattern.replace(wildCardEscapeSequence, '\x00'); // sentinel for escaped wildcards
    const regex = escapeRegExp(wildCardPattern);
    if (wildCardPattern.endsWith('/*')) {
        // If it ends with /* then we have to match X/* or X
        const regex2 = escapeRegExp(wildCardPattern.substring(0, wildCardPattern.length - 2));
        return new RegExp('^((' + regex + ')|(' + regex2 + '))$', 'i'); // perform case insensitive compares
    } else {
        return new RegExp('^' + regex + '$', 'i'); // perform case insensitive compares
    }
};

export class PermissionsClient extends Client {
    private static _instance: PermissionsClient | null = null;
    private armClient: ArmClient;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armClient = ArmClient.getInstance(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): PermissionsClient {
        if (!PermissionsClient._instance) {
            PermissionsClient._instance = new PermissionsClient(telemetrySource);
        }
        return PermissionsClient._instance;
    }

    public async getRoleDefinitions(
        scope: string,
        filter = '',
        apiVersion = ApiVersions.permissionsApiVersion20220401
    ): Promise<Response<ArmArray<RoleDefinition>>> {
        const queryString = filter ? `&$filter=${filter}` : '';
        return this.armClient.makeArmCall<ArmArray<RoleDefinition>>({
            method: 'GET',
            resourceId: `${scope}/providers/Microsoft.Authorization/roleDefinitions`,
            apiVersion,
            queryString,
            commandName: 'getRoleDefinitions',
        });
    }

    public async getRoleAssignments(
        scope: string,
        filter = '',
        apiVersion = ApiVersions.permissionsApiVersion20220401
    ): Promise<Response<ArmArray<RoleAssignment>>> {
        const queryString = filter ? `&$filter=${filter}` : '';
        return this.armClient.makeArmCall<ArmArray<RoleAssignment>>({
            method: 'GET',
            resourceId: `${scope}/providers/Microsoft.Authorization/roleAssignments`,
            apiVersion,
            queryString,
            commandName: 'getRoleAssignments',
        });
    }

    public async assignRole(
        scope: string,
        roleDefinitionId: string,
        principalId: string,
        principalType: string = '',
        apiVersion = ApiVersions.permissionsApiVersion20220401
    ): Promise<Response<ArmObj<RoleAssignment>>> {
        return this.armClient.makeArmCall<ArmObj<RoleAssignment>>({
            method: 'PUT',
            resourceId: `${scope}/providers/Microsoft.Authorization/roleAssignments/${newGuid()}`,
            apiVersion,
            body: {
                properties: {
                    roleDefinitionId,
                    principalId,
                    principalType: principalType ? principalType : undefined,
                },
            } as any,
            commandName: 'assignRole',
            skipPolling: true,
        });
    }

    public async getRoleAssignmentsWithScope(scope: string, principalId: string): Promise<Response<ArmArray<RoleAssignment>>> {
        const queryString = `&$filter=atScope()+and+assignedTo('{${principalId}}')`;
        return this.armClient.makeArmCall<ArmArray<RoleAssignment>>({
            method: 'GET',
            resourceId: `${scope}/providers/Microsoft.Authorization/roleAssignments`,
            apiVersion: ApiVersions.permissionsApiVersion20220401,
            queryString,
            commandName: 'GetRoleAssignmentsWithScope',
        });
    }

    public async putRoleAssignmentWithScope(
        roleAssignment: Partial<ArmObj<Partial<RoleAssignment>>>
    ): Promise<Response<ArmObj<RoleAssignment>>> {
        const queryString = `&$filter=atScope()+and+assignedTo('{${roleAssignment.properties?.principalId}}')`;
        return this.armClient.makeArmCall<ArmObj<RoleAssignment>>({
            method: 'PUT',
            resourceId: `${roleAssignment?.properties?.scope}/providers/Microsoft.Authorization/roleAssignments/${roleAssignment.name}`,
            apiVersion: ApiVersions.permissionsApiVersion20220401,
            queryString,
            body: roleAssignment as any,
            commandName: 'PutRoleAssignmentWithScope',
        });
    }

    /**
     * https://msazure.visualstudio.com/DefaultCollection/One/_git/AzureUX-PortalFx?path=/src/SDK/Website/TypeScript/MsPortalImpl/Services/PermissionsHelpers.ts
     * Evaluates the set of requested actions against the set of active permissions for an entity.
     * @param requestedActions The requested actions.
     * @param permissionSet The set of active permissions.
     */
    public canPerformActions(
        requestedActions: ReadonlyArray<string>,
        permissionSet: ReadonlyArray<Permissions>,
        resourceId: string
    ): boolean {
        if (!requestedActions || !permissionSet || permissionSet.length === 0) {
            // If there are no requested actions or no available actions the caller has no permissions
            return false;
        }

        // Convert available actions to regexes
        const permissionSetRegexes = permissionSet.map(permissionsToRegExp);

        const resourceType = parseArmId(resourceId)?.resourceType ?? '';

        // Every requested action must be allowed by the permission set
        const result = requestedActions.every(item => {
            if (item.length > 1 && item.charAt(0) === '.' && item.charAt(1) === '/') {
                // Special case: turn leading ./ to {resourceType}/ for formatting.
                item = resourceType + item.substring(1);
            }

            return permissionSetRegexes.some(availableRegex => {
                return isAllowed(item, availableRegex);
            });
        });

        return result;
    }

    /**
     * Only use this call if you want to handle the raw content results.
     * Can run the result of this call through {@link canPerformActions}.
     *
     * Otherwise use {@link hasPermission}
     */
    public async getPermissions(
        resourceID: string,
        apiVersion = ApiVersions.permissionsApiVersion20220401
    ): Promise<Response<PermissionsCheckResponse>> {
        return this.armClient.makeArmCall<PermissionsCheckResponse>({
            method: 'GET',
            resourceId: `${resourceID}/providers/Microsoft.Authorization/permissions`,
            apiVersion,
            commandName: 'checkPermissions',
        });
    }

    public async hasPermission(resourceId: string, actions: string[]): Promise<boolean> {
        const response = await this.getPermissions(resourceId);
        if (response.isSuccessful && response.content) {
            return this.canPerformActions(actions, response.content?.value, resourceId);
        }
        return false;
    }

    public async getRoleAssignmentsByPrincipalId(
        subscriptionId: string,
        principalId: string,
        apiVersion = ApiVersions.permissionsApiVersion20220401
    ): Promise<Response<ArmArray<RoleAssignment>>> {
        const queryString = `&$filter=principalId eq '${principalId}'`;
        return this.armClient.makeArmCall<ArmArray<RoleAssignment>>({
            method: 'GET',
            resourceId: `/subscriptions/${subscriptionId}/providers/Microsoft.Authorization/roleAssignments`,
            apiVersion,
            queryString,
            commandName: 'getRoleAssignmentsByPrincipalId',
        });
    }

    public async getLocks(resourceId: string, apiVersion = ApiVersions.armApiVersion20230301): Promise<Response<ArmArray<Lock>>> {
        return this.armClient.makeArmCall<ArmArray<Lock>>({
            method: 'GET',
            resourceId: `${resourceId}/providers/Microsoft.Authorization/locks`,
            apiVersion,
            commandName: 'getLocks',
        });
    }

    public async getDenyAssignments(
        resourceId: string,
        apiVersion = ApiVersions.permissionsApiVersion20220401
    ): Promise<Response<ArmArray<DenyAssignment>>> {
        return this.armClient.makeArmCall<ArmArray<DenyAssignment>>({
            method: 'GET',
            resourceId: `${resourceId}/providers/Microsoft.Authorization/denyAssignments`,
            apiVersion,
            commandName: 'getDenyAssignments',
        });
    }

    public async checkPolicies(
        resourceId: string,
        content: unknown,
        apiVersion = ApiVersions.armApiVersion20230301
    ): Promise<Response<PolicyAssignment>> {
        return this.armClient.makeArmCall<PolicyAssignment>({
            method: 'POST',
            resourceId: `${resourceId}/providers/Microsoft.PolicyInsights/checkPolicyRestrictions`,
            apiVersion,
            body: content as any,
            commandName: 'checkPolicies',
        });
    }
}
