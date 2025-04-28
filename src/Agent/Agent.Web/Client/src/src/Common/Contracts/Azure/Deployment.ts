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
        provisioningState?: ProvisioningState;
        error?: DeploymentError;
        outputResources?: DeploymentOutputResource[];
        timestamp?: string;
        parameters?: Record<string, any>;
        template?: any;
        mode?: any;
    };
    tags?: Record<string, any>;
    type?: string;
}

export interface DeploymentOutputResource {
    id: string;
}
