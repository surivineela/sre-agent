import axios from 'axios';
import { DataPlaneClient } from '../../Common/Clients/DataPlaneClient';
import { getAgentHeaders } from '../../Common/Helpers/headers';

export type PagerDutyApiKeyValidationResult = 'validKey' | 'missingKey' | 'invalidKey' | 'unknownError';

export const validatePagerDutyApiKey = (apiKey?: string): Promise<PagerDutyApiKeyValidationResult> => {
    if (!apiKey) {
        return Promise.resolve('missingKey');
    }
    const url = 'https://api.pagerduty.com/incidents?limit=1';
    const headers = {
        Authorization: `Token token=${apiKey}`,
    };
    return axios
        .get(url, { headers })
        .then(response => {
            return response.status === 200 ? 'validKey' : response.status === 401 ? 'invalidKey' : 'unknownError';
        })
        .catch(error => {
            if (error?.status === 401) {
                return 'invalidKey';
            }
            return 'unknownError';
        });
};

export type ServiceNowValidationResult =
    | 'valid'
    | 'missingEndpoint'
    | 'missingUsername'
    | 'missingPassword'
    | 'invalidCredentials'
    | 'connectionError'
    | 'unknownError';

export const validateServiceNowSettings = async (
    endpoint?: string,
    username?: string,
    password?: string,
    sreAgentEndpoint?: string
): Promise<ServiceNowValidationResult> => {
    if (!endpoint) {
        return Promise.resolve('missingEndpoint');
    }
    if (!username) {
        return Promise.resolve('missingUsername');
    }
    if (!password) {
        return Promise.resolve('missingPassword');
    }
    if (!sreAgentEndpoint) {
        return Promise.resolve('unknownError');
    }

    // Create data plane client to get the backend URL
    const dataPlaneClient = new DataPlaneClient(sreAgentEndpoint);
    const url = dataPlaneClient['getRequestUrl']('/api/v1/incidentplatformvalidation/servicenow');

    const requestBody = {
        endpoint,
        username,
        password,
    };

    try {
        const response = await axios.post(url, requestBody, {
            headers: getAgentHeaders(),
        });

        return response.data.result as ServiceNowValidationResult;
    } catch (error) {
        console.error('ServiceNow validation error:', error);
        return 'unknownError';
    }
};
