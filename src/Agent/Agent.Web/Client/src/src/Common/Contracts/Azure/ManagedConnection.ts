import { KeyValue } from '../KeyValue';

export interface ManagedConnection {
    api: {
        id: string;
    };
    parameterValues: KeyValue<string>;
    displayName: string;
}
