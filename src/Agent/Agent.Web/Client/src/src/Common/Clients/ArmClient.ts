import axios, { AxiosHeaderValue, AxiosResponse } from 'axios';
import { Subject, from, of } from 'rxjs';
import { asyncScheduler } from 'rxjs/internal/scheduler/async';
import { bufferTime, catchError, concatMap, filter, share, take } from 'rxjs/operators';
import { ApiVersions } from '../ApiVersions';
import { ArmRequestObject, AzureAsyncOperationStatus, HttpResponseObject, MethodTypes, ProvisioningState } from '../ArmHelper.types';
import AzPortalProxy from '../AzPortalProxy/AzPortalProxy';
import { ITelemetryInfo, LogLevel } from '../AzPortalProxy/Models/ITelemetryInfo';
import { ArmArray, ArmObj } from '../Contracts/Azure/ArmObj';
import { KeyValue } from '../Contracts/KeyValue';
import { Guid } from '../Helpers/Guid';
import Url from '../Helpers/Url';
import { LogCategories } from '../LogCategories';

const alwaysSkipBatching = !!Url.getParameterByName(null, 'appsvc.skipbatching');

export interface ARGOptions {
    $top?: number;
    $skipToken?: string;
}

export interface ARGRequestContent {
    subscriptions?: string[];
    query: string;
    options?: ARGOptions;
}

export interface ARGResponse {
    count: number;
    resultTruncated: boolean;
    data: ARGResponseObjData;
    totalRecord: number;
    $skipToken?: string;
}

export interface ARGResponseObjData {
    columns: ARGResponseDataColumn[];
    rows: any[][];
}

export interface ARGResponseDataColumn {
    name: string;
    type: 'string' | 'integer' | 'object';
}

interface InternalArmRequest {
    method: MethodTypes;
    resourceId: string;
    id: string;
    commandName?: string;
    body: any;
    apiVersion: string | null;
    useManagementEndpoint: boolean;
    queryString?: string;
    headers?: KeyValue<AxiosHeaderValue | undefined>;
}

interface ArmBatchObject {
    httpStatusCode: number;
    headers: KeyValue<string>;
    contentLength: number;
    content: any;
    id?: string;
}

interface ArmBatchResponse {
    responses: ArmBatchObject[];
}

interface CommonResponse {
    content: any;
    httpStatusCode: number;
    headers: KeyValue<string> | AxiosResponse['headers'];
}

const bufferTimeInterval = 100; // ms
const maxBufferSize = 20;
const armSubject$ = new Subject<InternalArmRequest>();
const armObs$ = armSubject$.pipe(
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
                url: Url.appendQueryString(`${arm.resourceId}${arm.queryString || ''}`, apiVersionString),
            };
        });

        return from(
            makeArmRequest<ArmBatchResponse>({
                method: 'POST',
                resourceId: '/batch',
                body: { requests: batchBody },
                apiVersion: ApiVersions.armBatchApi20151101,
                id: Guid.newGuid(),
                useManagementEndpoint: true,
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

export const getTelemetryInfo = (
    logLevel: LogLevel,
    action: string,
    actionModifier: string,
    data?: Record<string, any>
): ITelemetryInfo => {
    const dataContent = data ?? {};

    return {
        action,
        actionModifier,
        logLevel,
        data: {
            ...dataContent,
        },
    };
};

const makeArmRequest = async <T>(armObj: InternalArmRequest, _retry = 0): Promise<AxiosResponse<T>> => {
    const env = AzPortalProxy.envInfo;
    const { method, resourceId, body, apiVersion, queryString, useManagementEndpoint, commandName } = armObj;
    const sessionId = env?.sessionId;
    let url: string;
    const sanitizedResourceId = resourceId.startsWith('/') ? resourceId : `/${resourceId}`;
    if (useManagementEndpoint) {
        const armEndpoint = env?.armEndpoint;
        url = `${armEndpoint}${sanitizedResourceId}${queryString || ''}`;
    } else {
        url = `${sanitizedResourceId}${queryString || ''}`;
    }
    if (apiVersion !== null) {
        url = Url.appendQueryString(url, `api-version=${apiVersion}`);
    }
    const headers: KeyValue<string> = {
        Authorization: `Bearer ${env?.armToken}`,
        'x-ms-client-request-id': armObj.id,
        ...armObj.headers,
    };

    if (commandName) {
        headers['x-ms-command-name'] = commandName;
    }

    if (sessionId) {
        headers['x-ms-client-session-id'] = sessionId;
    }

    try {
        const result = await axios({
            url,
            method,
            headers,
            data: body,
            validateStatus: () => true, // never throw on an error, we can check the status and handle the error in the UI
        });

        /** @note (joechung): Portal context is unavailable so log messages to console. */
        console.log(
            getTelemetryInfo('info', LogCategories.armHelper, 'makeArmRequest', { resourceId, method, sessionId, correlationId: armObj.id })
        );

        return result;
    } catch (error) {
        // This shouldn't be hit since we're telling axios to not throw on error
        /** @note (joechung): Portal context is unavailable so log errors to console. */
        console.error(getTelemetryInfo('error', LogCategories.armHelper, 'makeArmRequest', { error }));
        throw error;
    }
};

const MakeArmCall = async <T, U = T>(requestObject: ArmRequestObject<U>): Promise<HttpResponseObject<T>> => {
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
    const effectiveApiVersion = useDirectUrl ? null : apiVersion !== null ? apiVersion || ApiVersions.antaresApiVersion20181101 : null;
    const effectiveSkipBatching = skipBatching || useDirectUrl;

    const id = Guid.newGuid();
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

    if (!effectiveSkipBatching && !alwaysSkipBatching) {
        try {
            const fetchFromBatch = new Promise<ArmBatchObject>((resolve, reject) => {
                armObs$
                    .pipe(
                        filter(x => {
                            return !x.id || x.id === id;
                        }),
                        take(1)
                    )
                    .subscribe(x => {
                        if (!x.id) {
                            reject(x);
                        } else {
                            resolve(x);
                        }
                    });

                armSubject$.next(armBatchObject);
            });

            const res = await fetchFromBatch;
            const resSuccess = res.httpStatusCode < 300;

            if ((res.httpStatusCode === 201 || res.httpStatusCode === 202) && !skipPolling) {
                return pollForCompletion({ content: res.content, httpStatusCode: res.httpStatusCode, headers: res.headers }, requestObject);
            } else {
                const ret: HttpResponseObject<T> = {
                    metadata: {
                        success: resSuccess,
                        status: res.httpStatusCode,
                        headers: res.headers,
                        error: resSuccess ? null : res.content,
                    },
                    data: resSuccess ? res.content : null,
                };

                return ret;
            }
        } catch (err) {
            const { status = 500, headers = {}, data = null } = (err as any) || {};
            return {
                metadata: {
                    success: false,
                    status,
                    headers,
                    error: data,
                },
                data: null as any,
            };
        }
    }

    const response = await makeArmRequest<T>(armBatchObject);
    const responseSuccess = response.status < 300;

    if ((response.status === 201 || response.status === 202) && !skipPolling) {
        return pollForCompletion({ content: response.data, httpStatusCode: response.status, headers: response.headers }, requestObject);
    } else {
        const retObj: HttpResponseObject<T> = {
            metadata: {
                success: responseSuccess,
                status: response.status,
                headers: response.headers,
                error: responseSuccess ? null : response.data,
            },
            data: response.data,
        };

        return retObj;
    }
};

const pollForCompletion = <T, U = T>(response: CommonResponse, request: ArmRequestObject<U>) => {
    const location = getStringHeader('location', response.headers);
    const azureAsyncOperation = getStringHeader('Azure-AsyncOperation', response.headers);
    if (location) {
        return pollLocationForCompletion(response, location, request);
    } else if (azureAsyncOperation) {
        return pollAzureAsyncOperationForCompletion(response, azureAsyncOperation, request);
    } else if ((<any>response.content)?.properties.provisioningState) {
        return pollProvisioningStateForCompletion(request);
    } else {
        const responseSuccess = response.httpStatusCode < 300;
        return Promise.resolve({
            data: response.content,
            metadata: {
                success: responseSuccess,
                status: response.httpStatusCode,
                headers: response.headers,
                error: responseSuccess ? null : response.content,
            },
        });
    }
};

const getPollingTelemetryHeader = (commandName: string | undefined): string => {
    return !commandName ? 'PollingAsyncResponse' : commandName.endsWith('-polling') ? commandName : commandName + '-polling';
};

const pollLocationForCompletion = <T, U = T>(response: CommonResponse, previousLocation: string, request: ArmRequestObject<U>) => {
    const location = getStringHeader('location', response.headers) || previousLocation;
    const retryAfter = Math.max(getNumericHeader('Retry-After', response.headers) ?? 0, 2000);
    const setTelemetryHeader = getPollingTelemetryHeader(request.commandName);

    return delay(() => {
        return MakeArmCall<T>({
            method: 'GET',
            url: location,
            commandName: setTelemetryHeader,
            useManagementEndpoint: false,
        });
    }, retryAfter);
};

const pollAzureAsyncOperationForCompletion = <T, U = T>(
    response: CommonResponse,
    azureAsyncOperation: string,
    request: ArmRequestObject<U>
): Promise<HttpResponseObject<T> | undefined> => {
    const retryAfter = Math.max(getNumericHeader('Retry-After', response.headers) ?? 0, 2000);
    const setTelemetryHeader = getPollingTelemetryHeader(request.commandName);

    return delay(() => {
        return MakeArmCall<T>({
            method: 'GET',
            resourceId: azureAsyncOperation,
            commandName: setTelemetryHeader,
            apiVersion: request.apiVersion,
        });
    }, retryAfter).then(r => {
        let retObj: Promise<HttpResponseObject<T> | undefined> | undefined = undefined;

        if (r) {
            const status = r.content?.status;
            const rSuccess = r.httpStatusCode < 300;

            if (status === AzureAsyncOperationStatus.Succeeded) {
                if (request.method === 'PUT' || request.method === 'PATCH') {
                    return MakeArmCall<T>({
                        method: 'GET',
                        resourceId: request.resourceId,
                        commandName: setTelemetryHeader,
                        apiVersion: request.apiVersion,
                    });
                } else {
                    retObj = Promise.resolve({
                        data: response.content,
                        metadata: {
                            success: rSuccess,
                            status: r.httpStatusCode,
                            headers: r.headers,
                            error: rSuccess ? null : response.content,
                        },
                    });
                }
            } else if (status === AzureAsyncOperationStatus.Cancelled || status === AzureAsyncOperationStatus.Failed) {
                const error = r.content || { code: status, message: null };
                retObj = Promise.resolve({
                    data: r.content,
                    metadata: {
                        success: rSuccess,
                        status: r.httpStatusCode,
                        headers: r.headers,
                        error: error,
                    },
                });
            } else {
                retObj = pollAzureAsyncOperationForCompletion<T, U>(response, azureAsyncOperation, request);
            }
        }

        return retObj;
    });
};

const pollProvisioningStateForCompletion = <T, U = T>(request: ArmRequestObject<U>): Promise<HttpResponseObject<T> | undefined> => {
    const retryAfter = 2000;
    const setTelemetryHeader = getPollingTelemetryHeader(request.commandName);

    return delay(() => {
        return MakeArmCall<T>({
            resourceId: request.resourceId,
            commandName: setTelemetryHeader,
            method: 'GET',
            apiVersion: request.apiVersion,
        });
    }, retryAfter).then(r => {
        let retObj: Promise<HttpResponseObject<T> | undefined> | undefined = undefined;
        if (r) {
            const rSuccess = r.httpStatusCode < 300;

            if (r.httpStatusCode === 200) {
                retObj = Promise.resolve({
                    data: r.content,
                    metadata: {
                        success: rSuccess,
                        status: r.httpStatusCode,
                        headers: r.headers,
                        error: rSuccess ? null : r.content,
                    },
                });
            } else if (r.httpStatusCode === 201 || r.httpStatusCode === 202) {
                const provisioningState = (<any>r.content)?.properties?.provisioningState;
                if (provisioningState === ProvisioningState.Succeeded) {
                    retObj = Promise.resolve({
                        data: r.content,
                        metadata: {
                            success: rSuccess,
                            status: r.httpStatusCode,
                            headers: r.headers,
                        },
                    });
                } else if (provisioningState === ProvisioningState.Failed) {
                    const error = r.content?.error || r.content;
                    retObj = Promise.resolve({
                        data: r.content,
                        metadata: {
                            success: rSuccess,
                            status: r.httpStatusCode,
                            headers: r.headers,
                            error: error,
                        },
                    });
                } else {
                    retObj = pollProvisioningStateForCompletion(request);
                }
            } else {
                const error = r.content || { code: r.httpStatusCode, message: null };
                retObj = Promise.resolve({
                    data: r.content,
                    metadata: {
                        success: rSuccess,
                        status: r.httpStatusCode,
                        headers: r.headers,
                        error: error,
                    },
                });
            }
        }

        return retObj;
    });
};

const getStringHeader = (headerToFind: string, headers: KeyValue<string> | AxiosResponse['headers']) => {
    for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === headerToFind.toLowerCase()) {
            const value = headers[key];
            if (value === undefined || value === null) {
                return undefined;
            }
            return typeof value === 'string' ? value : undefined;
        }
    }
};

const getNumericHeader = (headerToFind: string, headers: KeyValue<string> | AxiosResponse['headers']) => {
    for (const key of Object.keys(headers)) {
        if (key.toLowerCase() === headerToFind.toLowerCase()) {
            const value = headers[key];
            if (value === undefined || value === null) {
                return null;
            }
            if (typeof value === 'string') {
                return Number(value);
            }
            if (typeof value === 'number') {
                return value;
            }
            return null;
        }
    }
    return null;
};

const delay = async (func: () => Promise<any>, ms = 3000) => {
    await new Promise(resolve => setTimeout(resolve, ms));
    return await func();
};

export const getErrorMessage = (error: any, recursionLimit: number = 1): string => {
    return _extractErrorMessage(error, recursionLimit);
};

export const getErrorMessageOrStringify = (error: any, recursionLimit: number = 1): string => {
    const extractedError = _extractErrorMessage(error, recursionLimit);
    return extractedError ?? JSON.stringify(error || {});
};

const _extractErrorMessage = (error: any, recursionLimit: number): string => {
    if (!error) {
        return '';
    }

    if (Object(error) !== error) {
        // The error is a primative type, not an object.
        // If it is a string, just return the value. Otherwise, return any empty string because there's nothing to extract.
        return typeof error === 'string' ? (error as string) : '';
    }

    // Check if a "message" property is present on the error object.
    if (error.message || error.ExceptionMessage || error.Message) {
        return error.message || error.ExceptionMessage || error.Message;
    }

    // No "message" property was present, so check if there is an inner error object with a "message" property.
    return recursionLimit ? _extractErrorMessage(error.error, recursionLimit - 1) : '';
};

export const MakePagedArmCall = async <T, U = T>(
    requestObject: ArmRequestObject<ArmArray<U>>,
    portalContext?: AzPortalProxy
): Promise<ArmObj<T>[]> => {
    let results: ArmObj<T>[] = [];
    const response = await MakeArmCall<ArmArray<T>, ArmArray<U>>(requestObject);

    if (response.metadata.success) {
        results = [...results, ...response.data.value];

        if (response.data.nextLink) {
            const pathAndQuery = Url.getPathAndQuery(response.data.nextLink);

            const pagedResult = await MakePagedArmCall<T, U>(
                {
                    ...requestObject,
                    resourceId: pathAndQuery,
                    apiVersion: null,
                },
                portalContext
            );

            results = [...results, ...pagedResult];
        }
    } else {
        /** @note (joechung): Portal context may not be available so log errors to console. */
        console.error(getTelemetryInfo('error', LogCategories.armHelper, 'MakePagedArmCall', { error: response.metadata.error }));
        portalContext?.log(
            getTelemetryInfo('error', 'MakePagedArmCall', 'failed', {
                message: getErrorMessage(response.metadata.error),
                errorAsString: response.metadata.error ? JSON.stringify(response.metadata.error) : '',
            })
        );
    }

    return results;
};

export default MakeArmCall;
