import axios from 'axios';
import { FormikErrors } from 'formik';
import { Guid } from '../../Common/Helpers/Guid';
import { IncidentManagementFormValues, IncidentManagementPlatform } from '../Contracts/IncidentManagement';

const validatePagerDutyApiKey = (apiKey?: string): Promise<boolean> => {
    if (!apiKey) {
        return Promise.resolve(false);
    }
    const url = 'https://api.pagerduty.com/incidents?limit=1';
    const headers = {
        Authorization: `Token token=${apiKey}`,
    };
    return axios
        .get(url, { headers })
        .then(response => {
            return response.status === 200;
        })
        .catch(() => {
            return false;
        });
};

let validationGuid: string | undefined;
let latestValidationResult: FormikErrors<IncidentManagementFormValues> = {};

export const validateIncidentManagement = (
    formValues: IncidentManagementFormValues
): Promise<FormikErrors<IncidentManagementFormValues>> => {
    if (formValues.platform !== IncidentManagementPlatform.PagerDuty) {
        validationGuid = undefined;
        latestValidationResult = {};
        return Promise.resolve(latestValidationResult);
    } else if (!formValues.connectionKey) {
        validationGuid = undefined;
        latestValidationResult = { connectionKey: 'API Key is required' };
        return Promise.resolve(latestValidationResult);
    } else {
        const guid = Guid.newGuid();
        validationGuid = guid;
        return validatePagerDutyApiKey(formValues.connectionKey).then(valid => {
            if (validationGuid !== guid) {
                return latestValidationResult;
            }

            latestValidationResult = valid ? {} : { connectionKey: 'API key is not valid' };
            return latestValidationResult;
        });
    }
};
