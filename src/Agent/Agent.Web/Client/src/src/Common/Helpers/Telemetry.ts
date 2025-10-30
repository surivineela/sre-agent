import { ITelemetryInfo } from '../AzPortalProxy/Models/ITelemetryInfo';

export const logFieldValueChange = (fieldName: string, newValue: string | undefined, log: (info: ITelemetryInfo) => void) => {
    log({
        action: `Changed field '${fieldName}' value to: ${newValue ?? 'unknown'}`,
        actionModifier: 'info',
        logLevel: 'info',
        data: { fieldName, newValue },
    });
};
