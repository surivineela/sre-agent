import { ApiVersions } from '../ApiVersions';
import { ArmTemplateBuilder } from '../ArmTemplateBuilder/ArmTemplateBuilder';
import { ArmTemplateResource } from '../ArmTemplateBuilder/ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplateParameter,
    ArmTemplateResourceFragment,
    DeploymentFragment,
    ResourceTypes,
} from '../ArmTemplateBuilder/ArmTemplateTypes';
import { PermissionPrincipalType, RBACRoleIdToNameMap } from '../Contracts/Azure/Permission';

interface RbacFragment {
    roleDefinitionId: string;
    principalId: string;
    principalType: string;
}

interface SubscriptionRoleAssignmentOptions {
    deploymentGuid: string;
    roleDefinitionIds: string[];
    subscriptionId: string;
    /** The principal ID of the managed identity to assign roles to */
    principalId: string;
    /** The location for the subscription-scoped deployment */
    location: string;
}

export class SubscriptionRoleAssignmentTemplateResource extends ArmTemplateResource<object> {
    get type() {
        return `${ArmServiceType.SiteRbac}-subscription-${this._options.subscriptionId}`;
    }

    parameters: Record<string, ArmTemplateParameter> = {};

    constructor(
        builder: ArmTemplateBuilder,
        private _options: SubscriptionRoleAssignmentOptions
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
            name: `[concat('subscription-roleAssignments-', '${this._options.deploymentGuid}')]`,
            dependsOn: this.dependsOn,
            subscriptionId: this._options.subscriptionId,
            location: this._options.location,
            properties: {
                mode: 'Incremental',
                template: {
                    $schema: 'https://schema.management.azure.com/schemas/2018-05-01/subscriptionDeploymentTemplate.json#',
                    contentVersion: '1.0.0.0',
                    resources: this._getTemplate(),
                },
            },
        };
    }

    _getPermissionName = (roleDefinitionId: string): string => {
        return RBACRoleIdToNameMap[roleDefinitionId] ?? '';
    };

    _getTemplate = (): ArmTemplateResourceFragment<RbacFragment>[] => {
        return this._options.roleDefinitionIds.map(roleDefinitionId => {
            return {
                apiVersion: ApiVersions.RbacApiVersion,
                name: `[guid('${this._getPermissionName(roleDefinitionId)}', subscription().subscriptionId, '${this._options.deploymentGuid}', '${roleDefinitionId}')]`,
                type: ArmServiceType.SiteRbac,
                properties: {
                    roleDefinitionId: `[concat('/subscriptions/', subscription().subscriptionId, '/providers/Microsoft.Authorization/roleDefinitions/${roleDefinitionId}')]`,
                    principalId: this._options.principalId,
                    principalType: `${PermissionPrincipalType.servicePrincipal}`,
                },
            };
        });
    };
}
