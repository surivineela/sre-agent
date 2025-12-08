import { ConnectorType } from './Connectors/Wizard/Common/ConnectorType';

export enum ServiceTypeFilterKey {
    All = 'All',
}

export type ServiceTypeFilter = ServiceTypeFilterKey | ConnectorType;
