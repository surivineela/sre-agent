import { ArmObj } from '../Contracts/Azure/ArmObj';
import { ApiVersions } from '../ApiVersions';
import MakeArmCall from './ArmClient';

export default class LogicAppClient {
    public static putPagerDutyLogicApp = (
        resourceId: string,
        logicApp: ArmObj<any>,
        apiVersion = ApiVersions.logicAppApiVersion20190501
    ) => {

        return MakeArmCall<ArmObj<any>>({
            resourceId,
            commandName: 'putPagerDutyLogicApp',
            method: 'PUT',
            body: logicApp,
            apiVersion,
        });
    };

    public static deleteLogicApp = (
        resourceId: string,
        apiVersion = ApiVersions.logicAppApiVersion20190501
    ) => {

        return MakeArmCall<void>({
            resourceId,
            commandName: 'deleteLogicApp',
            method: 'DELETE',
            apiVersion,
        });
    };
}

export const generatePagerDutyLogicAppPayload = (
    resourceId: string,
    name: string,
    location: string,
    agentEndpoint: string,
    pagerDutyApiKey: string,
    managedApiResourceId: string,
    connectionResourceId: string,
    connectionName: string,
) => {
    return {
        id: resourceId,
        name: name,
        type: "microsoft.logic/workflows",
        location: location,
        properties: {
            definition: {
                $schema: "https://schema.management.azure.com/providers/Microsoft.Logic/schemas/2016-06-01/workflowdefinition.json#",
                contentVersion: "1.0.0.0",
                parameters: {
                    pagerDutyApiKey: {
                        defaultValue: "",
                        type: "SecureString"
                    },
                    agentEndpoint: {
                        defaultValue: "",
                        type: "String",
                    },
                    $connections: {
                        defaultValue: {},
                        type: "Object"
                    }
                },
                triggers: {
                    When_an_incident_is_created: {
                        recurrence: {
                            interval: 1,
                            frequency: "Minute"
                        },
                        evaluatedRecurrence: {
                            interval: 1,
                            frequency: "Minute"
                        },
                        splitOn: "@triggerBody()?['incidents']",
                        type: "ApiConnection",
                        inputs: {
                            host: {
                                connection: {
                                    name: "@parameters('$connections')['pagerduty']['connectionId']"
                                }
                            },
                            method: "get",
                            path: "/trigger2/incidents",
                            queries: { "include[]": "first_trigger_log_entries" }
                        }
                    }
                },
                actions: {
                    Send_get_request_to_FirstTriggerLogEntry: {
                        runAfter: {},
                        type: "Http",
                        inputs: {
                            uri: "https://api.pagerduty.com/log_entries/@{triggerBody()?['first_trigger_log_entry']?['id']}",
                            method: "GET",
                            headers: {
                                Authorization: "Token token=@{parameters('pagerDutyApiKey')}"
                            },
                            queries: { "include[]": "channels" }
                        },
                        runtimeConfiguration: {
                            contentTransfer: {
                                transferMode: "Chunked"
                            }
                        }
                    },
                    HTTP: {
                        runAfter: {
                            Send_get_request_to_FirstTriggerLogEntry: [
                                "Succeeded"
                            ]
                        },
                        type: "Http",
                        inputs: {
                            uri: "@{parameters('agentEndpoint')}api/v1/threads/incidents",
                            method: "POST",
                            body: {
                                Title: "@triggerBody()?['summary']",
                                Description: "@body('Send_get_request_to_FirstTriggerLogEntry')['log_entry']['channel']['details']",
                                IncidentId: "@triggerBody()?['id']",
                                Severity: "@triggerBody()?['urgency']",
                                Source: "PagerDuty"
                            }
                        },
                        runtimeConfiguration: {
                            contentTransfer: {
                                transferMode: "Chunked"
                            }
                        }
                    },
                },
                outputs: {}
            },
            parameters: {
                pagerDutyApiKey: {
                    type: "SecureString",
                    value: pagerDutyApiKey
                },
                agentEndpoint: {
                    type: "String",
                    value: agentEndpoint
                },
                $connections: {
                    type: "Object",
                    value: {
                        pagerduty: {
                            id: managedApiResourceId,
                            connectionId: connectionResourceId,
                            connectionName: connectionName
                        }
                    }
                }
            }
        }
    }
}