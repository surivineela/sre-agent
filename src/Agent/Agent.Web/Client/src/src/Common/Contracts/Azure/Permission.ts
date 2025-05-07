export interface PermissionsCheckResponse {
    value: [
        {
            actions: string[];
            notActions: string[];
            dataActions: string[];
            notDataActions: string[];
        },
    ];
}

export enum PermissionActions {
    RbacWrite = 'Microsoft.Authorization/*/Write',
}
