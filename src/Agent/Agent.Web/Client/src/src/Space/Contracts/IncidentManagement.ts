import { FormikProps } from 'formik';
import { ArmObj } from '../../Common/Contracts/Azure/ArmObj';
import { Agent, IncidentManagementType } from '../../Common/Contracts/Azure/SreAgent';

/** ServiceNow authentication type */
export type ServiceNowAuthType = 'basic' | 'oauth2';

export interface IncidentManagementFormValues {
    platform?: IncidentManagementType;
    connectionKey?: string;
    createDefaultHandler?: boolean;
    // ServiceNow fields
    endpoint?: string; // ServiceNow instance URL
    authType?: ServiceNowAuthType; // Authentication type selection (basic or oauth2)
    // OAuth fields (used when authType is 'oauth2')
    clientId?: string;
    clientSecret?: string;
    apiConnectionName?: string; // Azure API Connection name (set after OAuth setup)
    // Basic auth fields (used when authType is 'basic')
    username?: string;
    password?: string;
    // IcM specific fields
    owningTeamId?: string;
    incidentType?: string;
}

export interface IncidentManagementSettingsProps {
    integrated?: boolean;
    close?: () => void;
    keepOpen?: boolean;
}

/** Result from ServiceNow OAuth setup */
export interface ServiceNowOAuthResult {
    success: boolean;
    errorMessage?: string;
    connectionName?: string;
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
    isUsingAgentSpaceIdentity?: boolean;
    agent?: ArmObj<Agent>;
    /** Function to setup ServiceNow OAuth - triggers popup authorization flow */
    setupServiceNowOAuth?: (formValues: IncidentManagementFormValues) => Promise<ServiceNowOAuthResult>;
    /** Whether ServiceNow OAuth connection is verified as connected (apiConnectionName set AND connection status is connected) */
    isServiceNowOAuthConnected?: boolean;
    /** Function to cleanup a pending OAuth connection when user cancels after authorizing but before saving */
    cleanupPendingOAuthConnection?: (connectionName: string) => Promise<void>;
}

export interface IncidentHandler {
    id: string;
    name: string;
    severity: string;
    dateModified: Date;
}
