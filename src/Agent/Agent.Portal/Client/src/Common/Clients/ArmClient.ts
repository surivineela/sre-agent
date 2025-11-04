import { asyncScheduler, bufferTime, catchError, concatMap, filter, from, Observable, of, share, Subject, take } from 'rxjs';
import { getCloudEndpoints } from '../Auth/cloudConfig';
import { ApiVersions } from '../Constants/ApiVersions';
import { TelemetrySource } from '../Constants/Telemetry';
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
import { delay, getHeader } from '../Utilities/Client';
import { newGuid } from '../Utilities/Guid';
import { appendQueryString, getParameterByName } from '../Utilities/Url';
import { Client } from './Client';
import { tokenCache } from './TokenCache';

// Custom response interface to replace AxiosResponse
interface FetchResponse<T> {
    data: T;
    status: number;
    headers: Record<string, string>;
}

const bufferTimeInterval = 100; // ms
const maxBufferSize = 20;

export class ArmClient extends Client {
    private static _instance: ArmClient | null = null;
    private armEndpoint: string;
    private sessionId: string | null;
    private armSubject$: Subject<InternalArmRequest>;
    private armObs$!: Observable<ArmBatchObject>;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
        this.armEndpoint = getCloudEndpoints().arm;
        this.sessionId = getParameterByName(null, 'sessionId');
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
                    })
                ).pipe(
                    concatMap(result => {
                        if (result.status < 300) {
                            const { responses } = result.data;
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
                    return this.pollForCompletion(res, requestObject);
                } else {
                    return {
                        isSuccessful: resSuccess,
                        content: resSuccess ? res.content : undefined,
                        error: resSuccess ? undefined : res.content,
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
        const responseSuccess = response.status < 300;

        return {
            isSuccessful: responseSuccess,
            content: responseSuccess ? response.data : undefined,
            error: responseSuccess ? undefined : response.data,
        };
    }

    private async makeArmRequest<T>(armObj: InternalArmRequest, _retry = 0): Promise<FetchResponse<T>> {
        // Get ARM token
        const token = await tokenCache.getAccessToken('arm');

        const { method, resourceId, body, apiVersion, queryString } = armObj;
        let url = `${this.armEndpoint}${resourceId}${queryString || ''}`;
        if (apiVersion !== null) {
            url = appendQueryString(url, `api-version=${apiVersion}`);
        }
        const headers: KeyValue<string> = {
            Authorization: `Bearer ${token.raw}`,
            'x-ms-client-request-id': armObj.id,
            ...armObj.headers,
        };

        if (this.sessionId) {
            headers['x-ms-client-session-id'] = this.sessionId;
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

            console.log('makeArmRequest:', {
                resourceId,
                method,
                sessionId: this.sessionId,
                correlationId: armObj.id,
            });

            return {
                data,
                status: response.status,
                headers: responseHeaders,
            };
        } catch (error) {
            console.error('makeArmRequest:', error);
            throw error;
        }
    }

    private pollForCompletion<T, U = T>(response: ArmBatchObject, request: ArmRequestObject<U>): Promise<Response<T>> {
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
            });
        }
    }

    private pollLocationForCompletion<T, U = T>(
        response: ArmBatchObject,
        previousLocation: string,
        request: ArmRequestObject<U>
    ): Promise<Response<T>> {
        const location = getHeader('location', response.headers) || previousLocation;
        const retryAfter = Math.max(Number(getHeader('Retry-After', response.headers)), 2000);
        const setTelemetryHeader = request.commandName ? request.commandName + '-polling' : 'PollingAsyncResponse';

        return delay(() => {
            return this.makeArmCall<T>({
                method: 'GET',
                resourceId: location,
                commandName: setTelemetryHeader,
                apiVersion: request.apiVersion,
            });
        }, retryAfter);
    }

    private pollAzureAsyncOperationForCompletion<T, U = T>(
        response: ArmBatchObject,
        azureAsyncOperation: string,
        request: ArmRequestObject<U>
    ): Promise<Response<T>> {
        const retryAfter = Math.max(Number(getHeader('Retry-After', response.headers)), 2000);
        const setTelemetryHeader = request.commandName ? request.commandName + '-polling' : 'PollingAsyncResponse';

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
        const setTelemetryHeader = request.commandName ? request.commandName + '-polling' : 'PollingAsyncResponse';

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

            if (r.content?.httpStatusCode === 200) {
                return {
                    isSuccessful: true,
                    content: r.content,
                };
            } else if (r.content?.httpStatusCode === 201 || r.content?.httpStatusCode === 202) {
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
                const error = r.content || { code: r.content?.httpStatusCode, message: null };
                return {
                    isSuccessful: false,
                    error: error,
                };
            }
        });
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
