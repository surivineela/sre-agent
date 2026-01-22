import { asyncScheduler, bufferTime, catchError, concatMap, filter, from, Observable, of, share, Subject, take } from 'rxjs';
import { getCloudEndpoints } from '../Auth/cloudConfig';
import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
import { ARGRequestContent, ARGResponseObjectArray } from '../Contracts/Arg';
import {
    ArmBatchObject,
    ArmBatchResponse,
    ArmRequestObject,
    AzureAsyncOperationStatus,
    InternalArmRequest,
    KeyValue,
    ProvisioningState,
    ResponseArray,
    Tenant,
} from '../Contracts/Arm';
import { Response } from '../Contracts/Response';
import { acquireAccessToken, delay, getHeader } from '../Utilities/Client';
import { newGuid } from '../Utilities/Guid';
import { getSessionId } from '../Utilities/SessionManager';
import { appendQueryString } from '../Utilities/Url';
import { Client } from './Client';

// Custom response interface to replace AxiosResponse
interface CommonResponse<T> {
    content: T;
    httpStatusCode: number;
    headers: Record<string, string>;
}

const bufferTimeInterval = 100; // ms
const maxBufferSize = 20;

export class ArmClient extends Client {
    private static _instance: ArmClient | null = null;
    private armEndpoint: string;
    private armSubject$: Subject<InternalArmRequest>;
    private armObs$!: Observable<ArmBatchObject>;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armEndpoint = getCloudEndpoints().arm;
        this.armSubject$ = new Subject<InternalArmRequest>();
        this.initializeBatchingObservable();
    }

    public static getInstance(telemetrySource: TelemetrySource): ArmClient {
        if (!ArmClient._instance) {
            ArmClient._instance = new ArmClient(telemetrySource);
        }
        return ArmClient._instance;
    }

    private initializeBatchingObservable(): void {
        this.armObs$ = this.armSubject$.pipe(
            bufferTime(bufferTimeInterval, null, maxBufferSize, asyncScheduler),
            filter(x => x.length > 0),
            concatMap(x => {
                const batchBody = x.map(arm => {
                    const apiVersionString = arm.apiVersion ? `api-version=${arm.apiVersion}` : '';

                    return {
                        httpMethod: arm.method,
                        content: arm.body,
                        requestHeaderDetails: {
                            commandName: arm.commandName,
                            ...arm.headers,
                        },
                        url: appendQueryString(`${arm.resourceId}${arm.queryString || ''}`, apiVersionString),
                    };
                });

                return from(
                    this.makeArmRequest<ArmBatchResponse>({
                        method: 'POST',
                        resourceId: '/batch',
                        body: { requests: batchBody },
                        apiVersion: ApiVersions.armApiVersion20250301,
                        id: newGuid(),
                        useManagementEndpoint: true,
                    })
                ).pipe(
                    concatMap(result => {
                        if (result.httpStatusCode < 300) {
                            const { responses } = result.content;
                            const responsesWithId: ArmBatchObject[] = [];
                            for (let i = 0; i < responses.length; i = i + 1) {
                                responsesWithId.push({ ...responses[i], id: x[i].id });
                            }
                            return from(responsesWithId);
                        } else {
                            throw result;
                        }
                    }),
                    catchError(err => of(err))
                );
            }),
            share()
        );
    }

    public async makeArmCall<T, U = T>(requestObject: ArmRequestObject<U>): Promise<Response<T>> {
        const {
            skipBatching,
            method,
            resourceId = '',
            body,
            apiVersion,
            commandName,
            queryString,
            headers,
            url,
            skipPolling = false,
            useManagementEndpoint = true,
        } = requestObject;

        const useDirectUrl = !!url;
        const effectiveResourceId = useDirectUrl ? url! : resourceId;
        const effectiveApiVersion = useDirectUrl
            ? null
            : apiVersion !== null
              ? apiVersion || ApiVersions.appServiceApiVersion20250301
              : null;
        const effectiveSkipBatching = skipBatching || useDirectUrl;

        const id = newGuid();
        const armBatchObject: InternalArmRequest = {
            resourceId: effectiveResourceId,
            body,
            commandName,
            queryString: useDirectUrl ? '' : queryString,
            id,
            headers: headers || {},
            method: method || 'GET',
            apiVersion: effectiveApiVersion,
            useManagementEndpoint,
        };

        if (!effectiveSkipBatching) {
            try {
                const fetchFromBatch = new Promise<ArmBatchObject>((resolve, reject) => {
                    this.armObs$
                        .pipe(
                            filter((x: ArmBatchObject) => {
                                return !x.id || x.id === id;
                            }),
                            take(1)
                        )
                        .subscribe((x: ArmBatchObject) => {
                            if (!x.id) {
                                reject(x);
                            } else {
                                resolve(x);
                            }
                        });

                    this.armSubject$.next(armBatchObject);
                });

                const res = await fetchFromBatch;
                const resSuccess = res.httpStatusCode < 300;

                if ((res.httpStatusCode === 201 || res.httpStatusCode === 202) && !skipPolling) {
                    return this.pollForCompletion(
                        { content: res.content, httpStatusCode: res.httpStatusCode, headers: res.headers },
                        requestObject
                    );
                } else {
                    return {
                        isSuccessful: resSuccess,
                        content: resSuccess ? res.content : undefined,
                        error: resSuccess ? undefined : res.content,
                        metadata: {
                            status: res.httpStatusCode,
                            headers: res.headers,
                        },
                    };
                }
            } catch (err) {
                return {
                    isSuccessful: false,
                    error: err,
                };
            }
        }

        const response = await this.makeArmRequest<T>(armBatchObject);
        const responseSuccess = response.httpStatusCode < 300;
        if ((response.httpStatusCode === 201 || response.httpStatusCode === 202) && !skipPolling) {
            return this.pollForCompletion(response, requestObject);
        }
        return {
            isSuccessful: responseSuccess,
            content: responseSuccess ? response.content : undefined,
            error: responseSuccess ? undefined : response.content,
            metadata: {
                status: response.httpStatusCode,
                headers: response.headers,
            },
        };
    }

    private async makeArmRequest<T>(armObj: InternalArmRequest, _retry = 0): Promise<CommonResponse<T>> {
        const { accessToken: token } = await acquireAccessToken('arm', this.telemetrySource);
        const { method, resourceId, body, apiVersion, queryString, useManagementEndpoint, commandName } = armObj;
        let url: string;
        const sanitizedResourceId = resourceId.startsWith('/') ? resourceId : `/${resourceId}`;
        if (useManagementEndpoint) {
            url = `${this.armEndpoint}${sanitizedResourceId}${queryString || ''}`;
        } else {
            url = `${sanitizedResourceId}${queryString || ''}`;
        }
        if (apiVersion !== null) {
            url = appendQueryString(url, `api-version=${apiVersion}`);
        }
        const headers: KeyValue<string> = {
            Authorization: `Bearer ${token}`,
            'x-ms-client-request-id': armObj.id,
            'x-ms-client-session-id': getSessionId(),
            ...armObj.headers,
        };

        if (commandName) {
            headers['x-ms-command-name'] = commandName;
        }

        if (body) {
            headers['Content-Type'] = 'application/json';
        }

        try {
            const response = await fetch(url, {
                method,
                headers,
                body: body ? JSON.stringify(body) : undefined,
            });

            // Parse response body as JSON
            let data: T;
            try {
                data = await response.json();
            } catch {
                data = null as any; // If response is not JSON, set data to null
            }

            // Convert Headers object to plain object
            const responseHeaders: Record<string, string> = {};
            response.headers.forEach((value, key) => {
                responseHeaders[key] = value;
            });

            return {
                content: data,
                httpStatusCode: response.status,
                headers: responseHeaders,
            };
        } catch (error) {
            console.error('makeArmRequest:', error);
            throw error;
        }
    }

    private pollForCompletion<T, U = T>(response: CommonResponse<T>, request: ArmRequestObject<U>): Promise<Response<T>> {
        const location = getHeader('location', response.headers);
        const azureAsyncOperation = getHeader('Azure-AsyncOperation', response.headers);

        if (location) {
            return this.pollLocationForCompletion(response, location, request);
        } else if (azureAsyncOperation) {
            return this.pollAzureAsyncOperationForCompletion(response, azureAsyncOperation, request);
        } else if ((<any>response.content)?.properties.provisioningState) {
            return this.pollProvisioningStateForCompletion(request);
        } else {
            const responseSuccess = response.httpStatusCode < 300;
            return Promise.resolve({
                isSuccessful: responseSuccess,
                content: responseSuccess ? response.content : undefined,
                error: responseSuccess ? undefined : response.content,
                metadata: {
                    status: response.httpStatusCode,
                    headers: response.headers,
                },
            });
        }
    }

    private getPollingTelemetryHeader(commandName: string | undefined): string {
        return !commandName ? 'PollingAsyncResponse' : commandName.endsWith('-polling') ? commandName : commandName + '-polling';
    }

    private pollLocationForCompletion<T, U = T>(
        response: CommonResponse<T>,
        previousLocation: string,
        request: ArmRequestObject<U>
    ): Promise<Response<T>> {
        const location = getHeader('location', response.headers) || previousLocation;
        const retryAfter = Math.max(Number(getHeader('Retry-After', response.headers)), 2000);
        const setTelemetryHeader = this.getPollingTelemetryHeader(request.commandName);

        return delay(() => {
            return this.makeArmCall<T>({
                method: 'GET',
                url: location,
                commandName: setTelemetryHeader,
                useManagementEndpoint: false,
            });
        }, retryAfter);
    }

    private pollAzureAsyncOperationForCompletion<T, U = T>(
        response: CommonResponse<T>,
        azureAsyncOperation: string,
        request: ArmRequestObject<U>
    ): Promise<Response<T>> {
        const retryAfter = Math.max(Number(getHeader('Retry-After', response.headers)), 2000);
        const setTelemetryHeader = this.getPollingTelemetryHeader(request.commandName);

        return delay(() => {
            return this.makeArmCall<T>({
                method: 'GET',
                resourceId: azureAsyncOperation,
                commandName: setTelemetryHeader,
                apiVersion: request.apiVersion,
            });
        }, retryAfter).then(r => {
            if (!r || !r.isSuccessful) {
                // Return error if no response or failed
                return {
                    isSuccessful: false,
                    error: r?.error || { code: 'NoResponse', message: 'No response from polling operation' },
                };
            }

            const status = r.content?.status;

            if (status === AzureAsyncOperationStatus.Succeeded) {
                if (request.method === 'PUT' || request.method === 'PATCH') {
                    return this.makeArmCall<T>({
                        method: 'GET',
                        resourceId: request.resourceId,
                        commandName: setTelemetryHeader,
                        apiVersion: request.apiVersion,
                    });
                } else {
                    return {
                        isSuccessful: true,
                        content: response.content,
                    };
                }
            } else if (status === AzureAsyncOperationStatus.Cancelled || status === AzureAsyncOperationStatus.Failed) {
                return {
                    isSuccessful: false,
                    error: r.content || { code: status, message: null },
                };
            } else {
                return this.pollAzureAsyncOperationForCompletion<T, U>(response, azureAsyncOperation, request);
            }
        });
    }

    private pollProvisioningStateForCompletion<T, U = T>(request: ArmRequestObject<U>): Promise<Response<T>> {
        const retryAfter = 2000;
        const setTelemetryHeader = this.getPollingTelemetryHeader(request.commandName);

        return delay(() => {
            return this.makeArmCall<T>({
                resourceId: request.resourceId,
                commandName: setTelemetryHeader,
                method: 'GET',
                apiVersion: request.apiVersion,
            });
        }, retryAfter).then(r => {
            if (!r || !r.isSuccessful) {
                // Return error if no response or failed
                return {
                    isSuccessful: false,
                    error: r?.error || { code: 'NoResponse', message: 'No response from polling operation' },
                };
            }

            if (r.metadata?.status === 200) {
                return {
                    isSuccessful: true,
                    content: r.content,
                };
            } else if (r.metadata?.status === 201 || r.metadata?.status === 202) {
                const provisioningState = (<any>r.content)?.properties?.provisioningState;
                if (provisioningState === ProvisioningState.Succeeded) {
                    return {
                        isSuccessful: true,
                        content: r.content,
                    };
                } else if (provisioningState === ProvisioningState.Failed) {
                    const error = r.content?.error || r.content;
                    return {
                        isSuccessful: false,
                        error: error,
                    };
                } else {
                    return this.pollProvisioningStateForCompletion(request);
                }
            } else {
                const error = r.content || { code: r.metadata?.status, message: null };
                return {
                    isSuccessful: false,
                    error: error,
                };
            }
        });
    }

    /**
     * Execute an Azure Resource Graph (ARG) query
     * Note: Uses default 'objectArray' format for simpler, more readable code
     * Returns Response<T[]> format consistent with ARM calls
     */
    public async executeArg<T = any>(
        content: ARGRequestContent,
        commandName: string,
        apiVersion = ApiVersions.argQueryApiVersion20240401
    ): Promise<Response<T[]>> {
        try {
            const { accessToken: token } = await acquireAccessToken('arm', this.telemetrySource);

            const argUrl = `${this.armEndpoint}/providers/Microsoft.ResourceGraph/resources?api-version=${apiVersion}`;

            // Use default objectArray format (simpler to work with than table format)
            // Can be overridden via content.options.resultFormat if needed
            const requestContent: ARGRequestContent = {
                ...content,
                options: {
                    ...content.options,
                    resultFormat: content.options?.resultFormat ?? 'objectArray',
                },
            };

            const response = await fetch(argUrl, {
                method: 'POST',
                headers: {
                    Authorization: `Bearer ${token}`,
                    'Content-Type': 'application/json',
                    'x-ms-command-name': commandName,
                    'x-ms-client-session-id': getSessionId(),
                },
                body: JSON.stringify(requestContent),
            });

            if (!response.ok) {
                const errorText = await response.text();
                return {
                    isSuccessful: false,
                    error: new Error(`ARG query failed: ${response.status} ${response.statusText}. ${errorText}`),
                };
            }

            const data = (await response.json()) as ARGResponseObjectArray<T>;

            return {
                isSuccessful: true,
                content: data.data,
            };
        } catch (error) {
            return {
                isSuccessful: false,
                error: error instanceof Error ? error : new Error(String(error)),
            };
        }
    }

    public async getTenants(apiVersion = ApiVersions.armApiVersion20250301): Promise<Response<Tenant[]>> {
        const response = await this.makeArmCall<ResponseArray<Tenant>>({
            resourceId: '/tenants',
            commandName: 'getTenants',
            method: 'GET',
            apiVersion,
            skipBatching: true,
        });

        if (response.isSuccessful && response.content) {
            return {
                isSuccessful: true,
                content: response.content.value,
            };
        }

        return {
            isSuccessful: false,
            error: response.error,
        };
    }
}
