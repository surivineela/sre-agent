import { AxiosHeaderValue } from 'axios';
import { KeyValue } from './Contracts/KeyValue';

export type MethodTypes = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
export interface ArmRequestObject<T> {
    commandName: string;
    resourceId?: string;
    url?: string;
    skipPolling?: boolean;
    method?: MethodTypes;
    body?: T;
    skipBatching?: boolean;
    apiVersion?: string | null;
    queryString?: string;
    headers?: KeyValue<AxiosHeaderValue | undefined>;
    useManagementEndpoint?: boolean;
}
export interface HttpResponseObject<T> {
    metadata: {
        success: boolean;
        status: number;
        error?: any;
        headers: KeyValue<AxiosHeaderValue | undefined>;
    };
    data: T;
}

export enum AzureAsyncOperationStatus {
    Succeeded = 'Succeeded',
    Failed = 'Failed',
    Cancelled = 'Cancelled',
}

export enum ProvisioningState {
    Succeeded = 'Succeeded',
    Failed = 'Failed',
}
