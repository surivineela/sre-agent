import { ApiVersions } from '../ApiVersions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder/ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateBuilder/ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
    DeploymentFragment,
    SreAgentParameterName as ParamName,
    ResourceTypes,
} from '../ArmTemplateBuilder/ArmTemplateTypes';
import { PermissionPrincipalType, RBACRoleIdToNameMap } from '../Contracts/Azure/Permission';
interface RbacFragment {
    roleDefinitionId: string;
    principalId: string;
    principalType: string;
}

interface RoleAssignmentOptions {
    deploymentGuid: string;
    roleDefinitionIds: string[];
    resourceGroupName: string;
    subscriptionId: string;
}

export class RoleAssignmentTemplateResource extends ArmTemplateResource<object> {
    get type() {
        return `${ArmServiceType.SiteRbac}-${this._options.resourceGroupName}`;
    }

    parameters: Record<string, ArmTemplateParameter> = {
        [ParamName.UserIdentityName]: {
            type: 'string',
            defaultValue: '',
        },
    };

    constructor(
        builder: ArmTemplateBuilder,
        private _options: RoleAssignmentOptions
    ) {
        super(builder);
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<DeploymentFragment> {
        return {
            type: ResourceTypes.ResourceDeploymentType,
            apiVersion: ApiVersions.ArmApiVersion20210401,
            name: `[concat(substring('${this._options.resourceGroupName}', 0, min(34, length('${this._options.resourceGroupName}'))), '-roleAssignments-', '${this._options.deploymentGuid}')]`,
            dependsOn: this.dependsOn,
            subscriptionId: this._options.subscriptionId,
            resourceGroup: this._options.resourceGroupName,
            properties: {
                mode: 'Incremental',
                template: {
                    $schema: 'https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#',
                    contentVersion: '1.0.0.0',
                    resources: this._getTemplate(),
                },
            },
        };
    }

    _getPermissionName(roleDefinitionId: string): string {
        return RBACRoleIdToNameMap[roleDefinitionId] ?? '';
    }

    _getTemplate(): ArmTemplateResourceFragment<RbacFragment>[] {
        return this._options.roleDefinitionIds.map(roleDefinitionId => {
            return {
                apiVersion: ApiVersions.RbacApiVersion,
                name: `[guid('${this._getPermissionName(roleDefinitionId)}', '${this._options.resourceGroupName}', '${this._options.deploymentGuid}', '${roleDefinitionId}')]`,
                type: ArmServiceType.SiteRbac,
                location: `[parameters('${ArmTemplateParameterName.Location}')]`,
                properties: {
                    roleDefinitionId: `/subscriptions/{subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/${roleDefinitionId}`,
                    principalId: `[reference(resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', parameters('${ParamName.UserIdentityName}')), '2023-01-31').principalId]`,
                    principalType: `${PermissionPrincipalType.servicePrincipal}`,
                },
            };
        });
    }
}
