import { ApiVersions } from '../../../Constants/ApiVersions';
import { ResourceTypes } from '../../../Constants/Arm';
import { PermissionPrincipalType, RBACRoleIdToNameMap } from '../../../Contracts/Permissions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateParameterName,
    ArmTemplateResourceFragment,
    DeploymentFragment,
    SreAgentParameterName,
} from '../ArmTemplateTypes';
import { RoleAssignmentDependencyResolver } from './DependencyResolvers/RoleAssignmentDependencyResolver';

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

/**
 * ARM template resource for creating role assignments on managed resource groups
 * Creates a nested deployment that assigns multiple roles to the managed identity
 */
export class RoleAssignmentTemplateResource extends ArmTemplateResource<object> {
    get type() {
        return `${ArmServiceType.SiteRbac}-${this._options.resourceGroupName}`;
    }

    parameters: Record<string, ArmTemplateParameter> = {};

    constructor(
        builder: ArmTemplateBuilder,
        private _options: RoleAssignmentOptions
    ) {
        super(builder);
        this.dependencyResolvers = [new RoleAssignmentDependencyResolver(this)];
    }

    addResourceToBuilder(): void {
        this._builder.resources.push(this);
    }

    _getTemplateFragmentHelper(): ArmTemplateResourceFragment<DeploymentFragment> {
        return {
            type: ResourceTypes.ResourceDeploymentType,
            apiVersion: ApiVersions.armApiVersion20230301,
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
                apiVersion: ApiVersions.permissionsApiVersion20220401,
                name: `[guid('${this._getPermissionName(roleDefinitionId)}', '${this._options.resourceGroupName}', '${this._options.deploymentGuid}', '${roleDefinitionId}')]`,
                type: ArmServiceType.SiteRbac,
                location: `[parameters('${ArmTemplateParameterName.Location}')]`,
                properties: {
                    roleDefinitionId: `/subscriptions/{subscription().subscriptionId}/providers/Microsoft.Authorization/roleDefinitions/${roleDefinitionId}`,
                    principalId: `[reference(resourceId('Microsoft.ManagedIdentity/userAssignedIdentities', parameters('${SreAgentParameterName.OidcUserIdentityName}')), '2023-01-31').principalId]`,
                    principalType: `${PermissionPrincipalType.servicePrincipal}`,
                },
            };
        });
    }
}
