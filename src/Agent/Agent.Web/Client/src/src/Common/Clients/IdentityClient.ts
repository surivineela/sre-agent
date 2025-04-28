import { ApiVersions } from '../ApiVersions';
import { MicrosoftAuthorization } from '../Constants/Auth';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { Identity } from '../Contracts/Azure/Identity';
import { RoleAssignment } from '../Contracts/Azure/Permissions';
import MakeArmCall from './ArmClient';

export class IdentityClient {
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

    public static getManagedUserIdentity(resourceId: string, apiVersion = ApiVersions.userIdentityApiVersion) {
        return MakeArmCall<ArmObj<Identity>>({
            resourceId,
            commandName: 'GetManagedUserIdentityResource',
            apiVersion,
        });
    }
}
