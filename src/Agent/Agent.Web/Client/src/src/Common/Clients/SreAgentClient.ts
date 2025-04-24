import { ArmObj } from '../Contracts/Azure/ArmObj';
import { Agent } from '../Contracts/Azure/SreAgent';
import { ApiVersions } from '../ApiVersions';
import MakeArmCall from './ArmClient';

export default class SreAgentClient {
    public static getAgent = (resourceId: string, apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview) => {
        return MakeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName: 'getAgent',
            apiVersion,
        });
    };

    public static putAgent = (
        resourceId: string,
        agent: ArmObj<Agent>,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ) => {

        return MakeArmCall<ArmObj<Agent>>({
            resourceId,
            commandName: 'putAgent',
            method: 'PUT',
            body: agent,
            apiVersion,
        });
    };

    public static patchAgent = (
        resourceId: string,
        agent: Partial<ArmObj<Partial<Agent>>>,
        apiVersion = ApiVersions.microsoftAppApiVersion20250501Preview
    ) => {

        return MakeArmCall<ArmObj<Agent>, Partial<ArmObj<Partial<Agent>>>>({
            resourceId,
            commandName: 'patchAgent',
            method: 'PATCH',
            body: agent,
            apiVersion,
        });
    };
}