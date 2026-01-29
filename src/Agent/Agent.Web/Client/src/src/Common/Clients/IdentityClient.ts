import { ApiVersions } from '../ApiVersions';
import { MicrosoftAuthorization } from '../Constants/Auth';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { Identity } from '../Contracts/Azure/Identity';
import { RoleAssignment } from '../Contracts/Azure/Permission';
import MakeArmCall, { ARGRequestContent, ARGResponse } from './ArmClient';

export interface RoleAssignmentsByScope {
    /** Map of Azure resource scope to array of role definition IDs assigned at that scope */
    rolesByScope: Map<string, string[]>;
}

const extractRoleDefinitionId = (roleDefinitionResourceId: string): string => {
    const parts = roleDefinitionResourceId.split('/');
    return parts[parts.length - 1];
};

export class IdentityClient {
    public static async getRoleAssignmentsFromArg(
        subscriptionIds: string[],
        principalId: string,
        apiVersion = ApiVersions.argQueryApiVersion20200401Preview
    ): Promise<RoleAssignmentsByScope> {
        const result: RoleAssignmentsByScope = {
            rolesByScope: new Map(),
        };

        if (subscriptionIds.length === 0 || !principalId) {
            return result;
        }

        const query = `
            authorizationresources
            | where type =~ 'microsoft.authorization/roleassignments'
            | where properties.principalId == '${principalId}'
            | project scope = tolower(properties.scope), roleDefinitionId = tostring(properties.roleDefinitionId)
        `;

        const content: ARGRequestContent = {
            query,
            subscriptions: subscriptionIds,
        };

        const response = await MakeArmCall<ARGResponse, ARGRequestContent>({
            method: 'POST',
            url: `/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`,
            body: content,
            commandName: 'GetRoleAssignmentsFromArg',
        });

        if (response?.data?.data?.rows) {
            response.data.data.rows.forEach((row: any[]) => {
                const scope = (row[0] as string).toLowerCase();
                const roleDefinitionId = extractRoleDefinitionId(row[1] as string);

                if (!result.rolesByScope.has(scope)) {
                    result.rolesByScope.set(scope, []);
                }
                result.rolesByScope.get(scope)!.push(roleDefinitionId);
            });
        }

        return result;
    }

    public static getRoleAssignmentsWithScope(scope: string, principalId: string, apiVersion = ApiVersions.rbacApiVersion) {
        return MakeArmCall<ArmObj<Identity>>({
            url: `${scope}/${MicrosoftAuthorization.RoleAssignmentsProvider}?api-version=${apiVersion}&$filter=atScope()+and+assignedTo('{${principalId}}')`,
            commandName: 'GetManagedUserIdentityResource',
        });
    }

    public static putRoleAssignmentWithScope(
        roleAssignment: Partial<ArmObj<Partial<RoleAssignment>>>,
        apiVersion = ApiVersions.servicePrincipalRBACApiVersion
    ) {
        return MakeArmCall<Partial<ArmObj<Partial<RoleAssignment>>>>({
            commandName: 'PutRoleAssignmentWithScope',
            method: 'PUT',
            url: `${roleAssignment.properties!.scope}/${MicrosoftAuthorization.RoleAssignmentsProvider}/${roleAssignment.name}?api-version=${apiVersion}&$filter=atScope()+and+assignedTo('{${roleAssignment.properties!.principalId}}')`,
            body: roleAssignment,
        });
    }

    public static getManagedUserIdentity(
        resourceId: string,
        apiVersion = ApiVersions.userIdentityApiVersion
    ): Promise<ArmObj<Identity> | undefined> {
        return MakeArmCall<ArmObj<Identity>>({
            resourceId,
            commandName: 'GetManagedUserIdentityResource',
            apiVersion,
        }).then(response => {
            if (response?.metadata.success && response.data) {
                return response.data;
            } else {
                return undefined;
            }
        });
    }
}
