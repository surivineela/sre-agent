export interface KeyValue<T> {
    [key: string]: T;
}

export interface MsiIdentity {
    principalId: string;
    tenantId: string;
    type: string;
    userAssignedIdentities: KeyValue<KeyValue<string>>;
}
export interface ArmObj<T> {
    id: string;
    kind?: string;
    properties: T;
    type?: string;
    tags?: KeyValue<string>;
    location: string;
    name: string;
    identity?: MsiIdentity;
    sku?: ArmSku;
}

export interface ArmArray<T> {
    value: ArmObj<T>[];
    nextLink?: string | null;
    id?: string;
}

export interface ResponseArray<T> {
    value: T[];
    nextLink?: string | null;
    id?: string;
}

export interface ArmSku {
    name: string;
    tier: string;
    size: string;
    family: string;
    capacity: string;
}

export interface Identity {
    principalId: string;
    tenantId: string;
    type: string;
    userAssignedIdentities: KeyValue<KeyValue<string>>;
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
    headers?: KeyValue<string | undefined>;
}

export interface ArmBatchObject {
    httpStatusCode: number;
    headers: KeyValue<string>;
    contentLength: number;
    content: any;
    id?: string;
}

export interface ArmBatchResponse {
    responses: ArmBatchObject[];
}

export interface InternalArmRequest {
    method: MethodTypes;
    resourceId: string;
    id: string;
    commandName?: string;
    body: any;
    apiVersion: string | null;
    queryString?: string;
    headers?: KeyValue<string | undefined>;
}

export interface HttpResponseObject<T> {
    metadata: {
        success: boolean;
        status: number;
        error?: any;
        headers: KeyValue<string | undefined>;
    };
    data: T;
}

export interface Tenant {
    id: string;
    tenantId: string;
    displayName?: string;
    tenantCategory?: string;
    defaultDomain?: string;
    tenantType?: string;
    tenantBrandingLogoUrl?: string;
    countryCode?: string;
}
