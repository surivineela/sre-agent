import { ArmTemplateResource } from './ArmTemplateResource';
import {
    ArmServiceType,
    ArmTemplate,
    ArmTemplateParameter,
    ArmTemplateResourceFragment,
    ArmTemplateParameterName as ParamName,
} from './ArmTemplateTypes';

export class ArmTemplateBuilder {
    resources: ArmTemplateResource<any>[] = [];

    private _parameters: Record<string, ArmTemplateParameter> = {};

    constructor() {}

    addResource(fragment: ArmTemplateResource<any>) {
        if (this.findResourceById(fragment.type)) {
            throw Error(`Found duplicate types for ${fragment.type} in template`);
        }

        fragment.addResourceToBuilder();
    }

    findResourceById(type: ArmServiceType | string): ArmTemplateResource<any> | null {
        const resource = this.resources.find(r => {
            return r.type == type;
        });

        return resource ?? null;
    }

    resolveDependencies(): void {
        this.resources.forEach(r => {
            r.resolveDependencies();
            this._resolveDependencyHelper(r);
        });
    }

    private _resolveDependencyHelper(parentResource: ArmTemplateResource<any>): void {
        parentResource.resources.forEach(r => {
            r.resolveDependencies();
            this._resolveDependencyHelper(r);
        });
    }

    getTemplate(): ArmTemplate {
        this.resolveDependencies();

        const template: ArmTemplate = {
            $schema: 'http://schema.management.azure.com/schemas/2015-01-01/deploymentTemplate.json#',
            contentVersion: '1.0.0.0',
            parameters: {},
            variables: {},
            resources: [],
        };

        const initialParameters: Record<string, ArmTemplateParameter> = {
            [ParamName.SubscriptionId]: { type: 'string' },
            [ParamName.ResourceGroupName]: { type: 'string' },
            [ParamName.Location]: { type: 'string' },
        };

        this._parameters = {};
        this.mergeParameters(initialParameters);

        const resources: ArmTemplateResourceFragment<any>[] = [];
        this.resources.forEach(r => {
            resources.push(r.getTemplateFragment());
        });

        template.parameters = this._parameters;
        template.resources = resources;

        return template;
    }

    mergeParameters(parameters: Record<string, ArmTemplateParameter>) {
        for (const pName of Object.keys(parameters)) {
            this._parameters[pName] = parameters[pName];
        }
    }
}
