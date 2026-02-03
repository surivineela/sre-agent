import { IncidentManagementConfiguration, IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';

export const getIncidentManagementConfguration = (values: {
    incidentPlatformType: IncidentManagementType;
    pagerDutyApiKey?: string;
    serviceNowEndpoint?: string;
    serviceNowUsername?: string;
    serviceNowPassword?: string;
}): IncidentManagementConfiguration | null => {
    if (values.incidentPlatformType !== IncidentManagementType.None) {
        return {
            type: values.incidentPlatformType,
            connectionName: values.incidentPlatformType.toLowerCase(),
            ...(values.incidentPlatformType === IncidentManagementType.PagerDuty && {
                connectionKey: values.pagerDutyApiKey,
            }),
            ...(values.incidentPlatformType === IncidentManagementType.ServiceNow && {
                connectionUrl: values.serviceNowEndpoint,
                connectionKey: JSON.stringify({
                    username: values.serviceNowUsername,
                    password: values.serviceNowPassword,
                }),
            }),
        };
    }

    return null;
};
