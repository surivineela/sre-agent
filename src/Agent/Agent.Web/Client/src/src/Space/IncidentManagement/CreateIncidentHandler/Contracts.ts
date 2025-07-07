import { IncidentFilter } from '../../../Common/Contracts/Azure/IncidentHandler';

export enum TimeDuration {
    Last15Days = 15,
    Last30Days = 30,
    Last60Days = 60,
    Last90Days = 90,
}

export enum TimeDurationKey {
    Last15Days = 'last15Days',
    Last30Days = 'last30Days',
    Last60Days = 'last60Days',
    Last90Days = 'last90Days',
}

export enum IncidentTableFieldNames {
    Priority = 'priority',
    CreatedAt = 'createdAt',
    Title = 'title',
    Id = 'id',
    Status = 'status',
}

export enum ToolTableFieldNames {
    Name = 'name',
    Description = 'description',
}

export type FilterMode = 'create' | 'edit';
export type HandlerMode = 'create' | 'edit' | 'quickEdit';
export type OperationStatus = 'inprogress' | 'succeeded' | 'failed';

export interface HandlerCreateOrEditInfo {
    filter?: IncidentFilter;
    handlerId?: string;
    quickEdit?: boolean;
}
