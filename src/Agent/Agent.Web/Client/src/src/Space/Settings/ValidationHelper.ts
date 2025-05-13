import axios from 'axios';

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
