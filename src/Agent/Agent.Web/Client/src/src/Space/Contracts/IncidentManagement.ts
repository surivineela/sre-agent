import { FormikProps } from "formik";

export enum IncidentManagementPlatform {
    Disconnected = 'Disconnected',
    PagerDuty = 'PagerDuty'
};

export interface IncidentManagementFormValues {
    platform?: IncidentManagementPlatform;
    connectionUrl?: string;
    connectionKey?: string;
};

export interface IncidentManagementFormProps {
    formikProps: FormikProps<IncidentManagementFormValues>;
    loading?: boolean;
    loaded?: boolean;
    loadFailure?: string;
    saving?: boolean;
    saveFailure?: string;
};
