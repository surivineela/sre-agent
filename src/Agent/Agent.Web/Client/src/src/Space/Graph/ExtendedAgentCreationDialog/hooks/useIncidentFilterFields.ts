import { useEffect, useState } from 'react';
import { IncidentHandlerClient } from '../../../../Common/Clients/IncidentHandlerClient';
import { FilterFieldOption } from '../api/triggerCreation';

interface UseIncidentFilterFieldsResult {
    filterFields: FilterFieldOption[];
    isLoading: boolean;
    error: string | null;
}

/**
 * Hook to fetch dynamic incident filter field options from the backend
 */
export const useIncidentFilterFields = (sreAgentEndpoint: string): UseIncidentFilterFieldsResult => {
    const [filterFields, setFilterFields] = useState<FilterFieldOption[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(true);
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        const fetchFilterFields = async () => {
            try {
                setIsLoading(true);
                setError(null);

                const client = IncidentHandlerClient.getInstance(sreAgentEndpoint, () => {});
                const response = await client.getFilterFieldOptions();

                if (response.isSuccessful && response.content) {
                    setFilterFields(response.content);
                } else {
                    setError('Failed to fetch filter field options');
                    setFilterFields([]);
                }
            } catch (err) {
                console.error('Error fetching filter field options:', err);
                setError(err instanceof Error ? err.message : 'Unknown error');
                setFilterFields([]);
            } finally {
                setIsLoading(false);
            }
        };

        if (sreAgentEndpoint) {
            fetchFilterFields();
        }
    }, [sreAgentEndpoint]);

    return { filterFields, isLoading, error };
};

/**
 * Get priority options from filter fields
 */
export const getPriorityOptionsFromFilterFields = (filterFields: FilterFieldOption[]): { key: string; value: string }[] => {
    const priorityField = filterFields.find(field => field.fieldName === 'Priority');
    return priorityField?.options || [];
};

/**
 * Get incident type options from filter fields
 */
export const getIncidentTypeOptionsFromFilterFields = (filterFields: FilterFieldOption[]): { key: string; value: string }[] => {
    const incidentTypeField = filterFields.find(field => field.fieldName === 'IncidentType');
    return incidentTypeField?.options || [];
};

/**
 * Get all dropdown fields (excluding Priority and IncidentType which have dedicated UI)
 */
export const getAdditionalDropdownFilterFields = (filterFields: FilterFieldOption[]): FilterFieldOption[] => {
    return filterFields.filter(
        field =>
            field.fieldInputType === 'Dropdown' &&
            field.fieldName !== 'Priority' &&
            field.fieldName !== 'IncidentType' &&
            field.fieldName !== 'AgentMode' && // AgentMode is set automatically
            field.fieldName !== 'DeepInvestigationEnabled' // This might be exposed later
    );
};

/**
 * Get all text field options
 */
export const getTextFilterFields = (filterFields: FilterFieldOption[]): FilterFieldOption[] => {
    return filterFields.filter(field => field.fieldInputType === 'TextField');
};

/**
 * Convert field name from PascalCase to camelCase for use in trigger state
 */
export const fieldNameToCamelCase = (fieldName: string): string => {
    return fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
};
