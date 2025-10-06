import { FormikProps } from 'formik';
import { IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';

export interface IncidentManagementFormValues {
    platform?: IncidentManagementType;
    connectionKey?: string;
    createDefaultHandler?: boolean;
    // ServiceNow specific fields
    endpoint?: string;
    username?: string;
    password?: string;
    instanceName?: string;
    // IcM specific fields
    owningTeamId?: string;
    incidentType?: string;
}

export interface IncidentManagementSettingsProps {
    integrated?: boolean;
    close?: () => void;
    keepOpen?: boolean;
}
export interface IncidentManagementFormProps {
    formikProps: FormikProps<IncidentManagementFormValues>;
    disconnect: () => void;
    loading?: boolean;
    loaded?: boolean;
    loadFailure?: string;
    saving?: boolean;
    saveFailure?: string;
    managedIdentityId?: string;
    tenantId?: string;
    integrated?: boolean;
    close?: () => void;
    keepOpen?: boolean;
}

export interface IncidentHandler {
    id: string;
    name: string;
    severity: string;
    dateModified: Date;
}
