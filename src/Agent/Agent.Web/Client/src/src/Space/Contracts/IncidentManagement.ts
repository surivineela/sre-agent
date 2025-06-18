import { FormikProps } from 'formik';

export enum IncidentManagementPlatform {
    Disconnected = 'Disconnected',
    PagerDuty = 'PagerDuty',
    AzMonitor = 'AzMonitor',
}

export interface IncidentManagementFormValues {
    platform?: IncidentManagementPlatform;
    connectionKey?: string;
    createDefaultHandler?: boolean;
}

export interface IncidentManagementFormProps {
    formikProps: FormikProps<IncidentManagementFormValues>;
    disconnect: () => void;
    loading?: boolean;
    loaded?: boolean;
    loadFailure?: string;
    saving?: boolean;
    saveFailure?: string;
}

export interface IncidentHandler {
    id: string;
    name: string;
    severity: string;
    dateModified: Date;
}
