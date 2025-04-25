import { ApiVersions } from '../ApiVersions';
import { ArmObj } from '../Contracts/Azure/ArmObj';
import { ManagedConnection } from '../Contracts/Azure/ManagedConnection';
import MakeArmCall from './ArmClient';

export default class ManagedConnectionClient {
    public static putManagedConnection = (
        resourceId: string,
        managedConnection: ArmObj<ManagedConnection>,
        apiVersion = ApiVersions.managedConnectionApiVersion20180701Preview
    ) => {
        return MakeArmCall<ArmObj<ManagedConnection>>({
            resourceId,
            commandName: 'putManagedConnection',
            method: 'PUT',
            body: managedConnection,
            apiVersion,
        });
    };
}
