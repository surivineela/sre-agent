import { ApiVersions } from '../ApiVersions';
import { PermissionsCheckResponse } from '../Contracts/Azure/Permission';
import MakeArmCall from './ArmClient';

export class PermissionsClient {
    public static getPermissions(resourceId: string, apiVersion = ApiVersions.servicePrincipalRBACApiVersion) {
        return MakeArmCall<PermissionsCheckResponse>({
            commandName: 'checkPermissions',
            url: `${resourceId}/providers/Microsoft.Authorization/permissions?api-version=${apiVersion}`,
        });
    }
}
