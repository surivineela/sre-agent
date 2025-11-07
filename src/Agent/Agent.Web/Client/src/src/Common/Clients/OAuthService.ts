import { HttpResponseObject, MethodTypes } from '../ArmHelper.types';
import { ConsentLink } from '../Contracts/Azure/ConsentLinks';
import MakeArmCall from './ArmClient';
import { Connector, TestConnectionObject } from './ConnectorService';

export const popupId = 'ms-sreagent-oauthpopup';

export const getRedirectUrl = () => {
    // Use the current origin to support both dev (localhost:5173) and production
    const baseUrl = window.location.origin;
    // In production, the base is /static, in dev it's also /static due to vite config
    return `${baseUrl}/static/authredirect.html?pid=${popupId}`;
};

interface ConsentUrlOptions {
    subscriptionId: string;
    resourceGroup: string;
    connectionName: string;
    tenantId: string;
    objectId: string;
    apiVersion?: string;
}

interface ConfirmConsentCodeRequestOptions {
    subscriptionId: string;
    resourceGroup: string;
    connectionName: string;
    code: string;
    tenantId: string;
    objectId: string;
    apiVersion?: string;
}

export class OAuthServiceClient {
    public static fetchConsentUrlForConnection(options: ConsentUrlOptions): Promise<HttpResponseObject<{ value: ConsentLink[] }>> {
        const { subscriptionId, resourceGroup, connectionName, tenantId, objectId, apiVersion = '2018-07-01-preview' } = options;
        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${connectionName}/listConsentLinks?api-version=${apiVersion}`;

        const requestBody = {
            parameters: [
                {
                    parameterName: 'token',
                    redirectUrl: getRedirectUrl(),
                    tenantId,
                    objectId,
                },
            ],
        };

        return MakeArmCall({
            commandName: 'listConsentLinks',
            method: 'POST',
            body: requestBody,
            url,
            apiVersion,
        });
    }

    public static confirmConsentCodeForConnection(options: ConfirmConsentCodeRequestOptions) {
        const { subscriptionId, resourceGroup, connectionName, code, tenantId, objectId, apiVersion = '2018-07-01-preview' } = options;

        const url = `/subscriptions/${subscriptionId}/resourceGroups/${resourceGroup}/providers/Microsoft.Web/connections/${connectionName}/confirmConsentCode`;

        const requestBody = {
            code,
            objectId,
            tenantId,
        };

        return MakeArmCall({
            commandName: 'confirmConsentCode',
            method: 'POST',
            body: requestBody,
            url,
            apiVersion,
        });
    }

    public static async testConnection(connection: Connector) {
        let response: HttpResponseObject<any> | undefined = undefined;
        const testLink = connection.properties?.testLinks?.[0];
        try {
            if (testLink) {
                response = await this.requestTestConnection(testLink);
                if (response) {
                    this.handleTestConnectionResponse(response);
                }
            }
        } catch (testLinkError: any) {
            try {
                const testRequest = connection.properties?.testRequests?.[0];
                if (testRequest) {
                    response = await this.requestTestConnection(testRequest);
                    if (response) {
                        this.handleTestConnectionResponse(response);
                    }
                }
            } catch (testRequestError: any) {
                throw testLinkError ?? testRequestError;
            }
        }
    }

    private static requestTestConnection(testConnectionObj: TestConnectionObject): Promise<HttpResponseObject<unknown> | undefined> {
        const { method: httpMethod, requestUri: uri, body } = testConnectionObj;
        if (!httpMethod || !uri) {
            return Promise.resolve(undefined);
        }
        const method = httpMethod.toUpperCase() as MethodTypes;

        try {
            return MakeArmCall({
                commandName: 'confirmConsentCode',
                method,
                body,
                url: uri,
                useManagementEndpoint: false,
            });
        } catch (error: any) {
            return Promise.reject(error?.content ?? error);
        }
    }

    private static handleTestConnectionResponse(response?: HttpResponseObject<any>): void {
        if (!response) {
            return;
        }

        // if (response?.data?.status) {
        //     this.handleTestLinkResponse(response);
        //     return;
        // }
        // if ((response as any)?.response) {
        //     this.handleTestRequestResponse((response as any)?.response);
        // }
    }

    //     private static handleTestLinkResponse(response: HttpResponseObject<any>): void {
    //         const defaultErrorResponse = 'Please check your account info and/or permissions and try again.';
    //         const status = response?.data.status;
    //         if (status >= 400 && status < 500 && status !== 429) {
    //         let errorMessage = defaultErrorResponse;
    //         const body = response?.data;
    //         if (body && typeof body === 'string') {
    //             errorMessage = this.tryParseErrorMessage(JSON.parse(body), defaultErrorResponse);
    //         }
    //         throw new UserException(UserErrorCode.TEST_CONNECTION_FAILED, errorMessage);
    //         }
    //     }

    //   private handleTestRequestResponse(response: any): void {
    //     const defaultErrorResponse = 'Please check your account info and/or permissions and try again.';
    //     const statusCode = response?.statusCode;
    //     if (statusCode !== 'OK') {
    //       let errorMessage = defaultErrorResponse;
    //       const body = response?.body;
    //       if (body) {
    //         errorMessage = this.tryParseErrorMessage(body, defaultErrorResponse);
    //       }
    //       throw new UserException(UserErrorCode.TEST_CONNECTION_FAILED, errorMessage);
    //     }
    //   }

    //   protected tryParseErrorMessage(error: any, defaultErrorMessage?: string): string {
    //     return (
    //       error?.message ??
    //       error?.Message ??
    //       error?.error?.message ??
    //       error?.responseText ??
    //       error?.errors?.map((e: any) => e?.message ?? e)?.join(', ') ??
    //       defaultErrorMessage ??
    //       'Unknown error'
    //     );
    //   }
}
