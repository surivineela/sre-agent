import { useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';

type KeyValuePair = { key: string; value: string };

export interface IncidentFilterFieldOptions {
    displayName: string;
    fieldName: string;
    options: KeyValuePair[];
}

export enum IncidentFilterField {
    ImpactedService = 'ImpactedService',
    IncidentType = 'IncidentType',
    Priority = 'Priority',
}

export const useIncidentFilterFields = () => {
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const [filterFieldOptions, setFilterFieldOptions] = useState<IncidentFilterFieldOptions[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(true);

    const impactedServiceOptions = useMemo((): string[] => {
        const impactServiceField = filterFieldOptions?.find(
            (option: IncidentFilterFieldOptions) => option.fieldName === IncidentFilterField.ImpactedService
        );
        if (!impactServiceField) return [];
        return impactServiceField.options?.map((option: KeyValuePair) => option.value) ?? [];
    }, [filterFieldOptions]);

    const incidentTypeOptions = useMemo((): string[] => {
        const incidentTypeField = filterFieldOptions?.find(
            (option: IncidentFilterFieldOptions) => option.fieldName === IncidentFilterField.IncidentType
        );
        if (!incidentTypeField) return [];
        return incidentTypeField.options?.map((option: KeyValuePair) => option.value) ?? [];
    }, [filterFieldOptions]);

    const priorityOptions = useMemo((): string[] => {
        const priorityField = filterFieldOptions?.find(
            (option: IncidentFilterFieldOptions) => option.fieldName === IncidentFilterField.Priority
        );
        if (!priorityField) return [];
        return priorityField.options?.map((option: KeyValuePair) => option.value) ?? [];
    }, [filterFieldOptions]);

    const getFilterFieldOptions = useCallback(async (): Promise<IncidentFilterFieldOptions[]> => {
        const incidentResults = await IncidentHandlerClient.getInstance(sreAgentEndpoint).getFilterFieldOptions();
        return incidentResults?.content ?? [];
    }, [sreAgentEndpoint]);

    const refresh = useCallback(async () => {
        setIsLoading(true);
        const results = await getFilterFieldOptions();
        setFilterFieldOptions(results);
        setIsLoading(false);
    }, [getFilterFieldOptions]);

    useEffect(() => {
        let isSubscribed = true;

        const fetch = async () => {
            const initialResults = await getFilterFieldOptions();
            if (!isSubscribed) return;
            setFilterFieldOptions(initialResults);
            setIsLoading(false);
        };

        fetch();
        return () => {
            isSubscribed = false;
        };
    }, [getFilterFieldOptions]);

    return {
        refresh,
        impactedServiceOptions,
        incidentTypeOptions,
        priorityOptions,
        filterFieldOptions,
        filterFieldOptionsLoading: isLoading,
    };
};
