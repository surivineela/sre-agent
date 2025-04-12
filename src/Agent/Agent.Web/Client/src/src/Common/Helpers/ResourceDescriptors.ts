export abstract class Descriptor {
    public parts: string[];

    constructor(public resourceId: string) {
        this.parts = resourceId.split('/').filter(part => !!part);
    }

    public abstract getTrimmedResourceId(): string;
}

export class ArmResourceDescriptor extends Descriptor {
    public subscription: string;
    public resourceGroup: string;
    public resourceName: string;

    constructor(resourceId: string) {
        super(resourceId);

        if (this.parts.length < 4) {
            throw Error(`resourceId length is too short: ${resourceId}`);
        }

        if (this.parts[0].toLowerCase() !== 'subscriptions') {
            throw Error(`Expected subscriptions segment in resourceId: ${resourceId}`);
        }

        if (this.parts[2].toLowerCase() !== 'resourcegroups') {
            throw Error(`Expected resourceGroups segment in resourceId: ${resourceId}`);
        }

        this.subscription = this.parts[1];
        this.resourceGroup = this.parts[3];
        this.resourceName = this.parts[this.parts.length - 1];
    }

    public getTrimmedResourceId() {
        return this.resourceId;
    }
}