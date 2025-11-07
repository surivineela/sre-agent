import { getCloudEndpoints } from '../Auth/cloudConfig';
import { TelemetrySource } from '../Constants/Telemetry';
import { Response } from '../Contracts/Response';
import { LogLevel } from '../Contracts/Telemetry';
import { logTelemetryEvent } from '../Hooks/useTelemetry';
import { acquireAccessToken } from '../Utilities/Client';
import { Client } from './Client';

export class GraphClient extends Client {
    private static _instance: GraphClient | null = null;

    private constructor(telemetrySource: TelemetrySource) {
        super(telemetrySource);
    }

    public static getInstance(telemetrySource: TelemetrySource): GraphClient {
        if (!GraphClient._instance) {
            GraphClient._instance = new GraphClient(telemetrySource);
        }
        return GraphClient._instance;
    }

    /**
     * Fetches the user's profile photo from Microsoft Graph.
     * Returns a blob URL that can be used in an img src, or undefined if no photo is set.
     */
    public async getProfilePhoto(): Promise<Response<string | undefined>> {
        const { accessToken: token } = await acquireAccessToken('graph', this.telemetrySource);

        try {
            const endpoints = getCloudEndpoints();
            const photoResponse = await fetch(`${endpoints.graph}/v1.0/me/photos/96x96/$value`, {
                headers: {
                    Authorization: `Bearer ${token}`,
                },
            });

            if (photoResponse.ok) {
                const blob = await photoResponse.blob();
                const url = URL.createObjectURL(blob);
                return {
                    isSuccessful: true,
                    content: url,
                };
            } else if (photoResponse.status === 404) {
                // User doesn't have a profile photo set
                return {
                    isSuccessful: true,
                    content: undefined,
                };
            } else {
                const error = new Error(`Failed to fetch profile photo: ${photoResponse.status}`);
                logTelemetryEvent({
                    action: 'fetch-profile-photo',
                    actionModifier: 'failed',
                    logLevel: LogLevel.Error,
                    telemetrySource: this.telemetrySource,
                    additionalData: {
                        status: photoResponse.status,
                        statusText: photoResponse.statusText,
                    },
                });
                return {
                    isSuccessful: false,
                    error,
                };
            }
        } catch (error) {
            logTelemetryEvent({
                action: 'fetch-profile-photo',
                actionModifier: 'error',
                logLevel: LogLevel.Error,
                telemetrySource: this.telemetrySource,
                additionalData: {
                    error: error instanceof Error ? error.message : String(error),
                },
            });
            return {
                isSuccessful: false,
                error,
            };
        }
    }
}
