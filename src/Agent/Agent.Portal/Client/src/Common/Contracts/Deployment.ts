export type ProvisioningState =
    | 'Accepted'
    | 'Canceled'
    | 'Created'
    | 'Creating'
    | 'Deleted'
    | 'Failed'
    | 'Deleting'
    | 'NotSpecified'
    | 'Ready'
    | 'Running'
    | 'Succeeded'
    | 'Updating';

export interface DeploymentErrorDetails {
    code?: string;
    message?: string;
}

export interface DeploymentError {
    code?: string;
    message?: string;
    details?: DeploymentErrorDetails;
}

export interface DeploymentExtended {
    id: string;
    location: string;
    name: string;
    properties?: {
        correlationId?: string;
        provisioningState?: ProvisioningState;
        error?: DeploymentError;
        outputResources?: DeploymentOutputResource[];
        timestamp?: string;
        parameters?: Record<string, any>;
        template?: any;
        mode?: 'Incremental' | string;
    };
    tags?: Record<string, any>;
    type?: string;
}

export interface DeploymentOutputResource {
    id: string;
}

export interface DeploymentOperation {
    id: string;
    operationId: string;
    properties?: {
        provisioningState?: ProvisioningState;
        targetResource?: {
            id: string;
            resourceType: string;
            resourceName: string;
        };
        statusCode?: string;
        statusMessage?: any;
        timestamp?: string;
    };
}

export type ArmTargetResource = {
    id: string;
    resourceType: string;
    resourceName: string;
};

export enum ProvisioningOperation {
    action = 'Action',
    create = 'Create',
}

export type ArmDeploymentOperationProperties = {
    provisioningState: string;
    timestamp: string;
    duration: string;
    trackingId: string;
    statusCode: string;
    statusMessage: any;
    targetResource?: ArmTargetResource;
    provisioningOperation?: ProvisioningOperation;
};

export type ArmDeploymentOperation = {
    id: string;
    operationId: string;
    properties: ArmDeploymentOperationProperties;
};

export type ArmDeploymentOperationResponse = {
    value: ArmDeploymentOperation[];
};
