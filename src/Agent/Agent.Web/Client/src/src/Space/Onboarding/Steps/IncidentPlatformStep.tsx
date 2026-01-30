import { useFormikContext } from 'formik';
import { FC, useCallback } from 'react';
import { IncidentPlatformPicker, IncidentPlatformValues } from '../../../Common/Components/IncidentPlatformPicker/IncidentPlatformPicker';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { AgentFormValues } from '../../../Common/Utils/AgentFormUtils';

export const IncidentPlatformStep: FC = () => {
    const { values, setFieldValue } = useFormikContext<AgentFormValues>();

    const pickerValues: IncidentPlatformValues = {
        incidentPlatformType: values.incidentPlatformType,
        pagerDutyApiKey: values.pagerDutyApiKey,
        serviceNowEndpoint: values.serviceNowEndpoint,
        serviceNowUsername: values.serviceNowUsername,
        serviceNowPassword: values.serviceNowPassword,
    };

    const handlePlatformSelect = useCallback(
        (type: IncidentManagementType) => {
            setFieldValue('incidentPlatformType', type);
        },
        [setFieldValue]
    );

    const handlePagerDutyApiKeyChange = useCallback(
        (value: string) => {
            setFieldValue('pagerDutyApiKey', value);
        },
        [setFieldValue]
    );

    const handleServiceNowEndpointChange = useCallback(
        (value: string) => {
            setFieldValue('serviceNowEndpoint', value);
        },
        [setFieldValue]
    );

    const handleServiceNowUsernameChange = useCallback(
        (value: string) => {
            setFieldValue('serviceNowUsername', value);
        },
        [setFieldValue]
    );

    const handleServiceNowPasswordChange = useCallback(
        (value: string) => {
            setFieldValue('serviceNowPassword', value);
        },
        [setFieldValue]
    );

    return (
        <IncidentPlatformPicker
            values={pickerValues}
            onPlatformSelect={handlePlatformSelect}
            onPagerDutyApiKeyChange={handlePagerDutyApiKeyChange}
            onServiceNowEndpointChange={handleServiceNowEndpointChange}
            onServiceNowUsernameChange={handleServiceNowUsernameChange}
            onServiceNowPasswordChange={handleServiceNowPasswordChange}
            showDescription={true}
        />
    );
};
