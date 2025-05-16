export interface ICMAlertConfig {
    id: string;
    teamId: number;
    alertingId: string;
    incidentTitle?: string;
    incidentTitleContains?: string;
    owningTeams: string[];
    agentMode: string | null;
    useCorrelationIdForKustoQuery: boolean;
    genevaActions: GenevaActionConfigBase[];
    allowedGenevaActions: string[];
    kustoQueries: ICMConfigKustoQueryModel[];
    owners: string[];
    actionTimeoutIntervalInMinutes: number;
    defaultHumanInterventionLoop: string;
    routingInstructions: string[];
    mitigationInstructions: string[];
    monitoringInstructions: string[];
    incidentProcessingGuide: string[];
    agentName?: string;
}

interface GenevaActionConfigBase {
    actionName: string;
    tenantId: string;
    workflowName: string;
    workflowInputParameters: string[];
}

interface KustoQueryModel {
    title: string;
    kustoQuery: string;
}

interface ICMConfigKustoQueryModel extends KustoQueryModel {
    cloud: string;
    cluster: string;
    database: string;
}

// This is the JSON schema for using in monaco editor.
export const monacoJsonSchema =
{
    "$schema": "http://json-schema.org/draft-07/schema#",
    "title": "ICMAlertConfig",
    "type": "object",
    "properties": {
        "teamId": {
            "type": "number"
        },
        "alertingId": {
            "type": "string"
        },
        "incidentTitle": {
            "description": "The title of the incident",
            "type": "string"
        },
        "incidentTitleContains": {
            "description": "This is for generation of incidentProcessingGuide",
            "type": ["string","null"]
        },
        "owningTeams": {
            "description": "The teams that own the alert",
            "type": "array",
            "items": {
                "type": "string"
            }
        },
        "agentMode": {
            "type": ["string", "null"]
        },
        "useCorrelationIdForKustoQuery": {
            "type": "boolean"
        },
        "genevaActions": {
            "type": ["array","null"],
            "description": "List of Geneva actions to be executed",
            "items": {
                "$ref": "#/definitions/GenevaActionConfigBase"
            }
        },
        "allowedGenevaActions": {
            "type": ["array","null"],
            "description": "List of allowed Geneva actions",
            "items": {
                "type": "string"
            }
        },
        "kustoQueries": {
            "description": "List of Kusto queries to be executed",
            "type": "array",
            "items": {
                "$ref": "#/definitions/ICMConfigKustoQueryModel"
            }
        },
        "owners": {
            "description": "List of owners alias for the alert",
            "type": "array",
            "items": {
                "type": "string"
            }
        },
        "actionTimeoutIntervalInMinutes": {
            "type": "number"
        },
        "defaultHumanInterventionLoop": {
            "type": "string"
        },
        "routingInstructions": {
            "type": "array",
            "items": {
                "type": "string"
            }
        },
        "mitigationInstructions": {
            "type": "array",
            "items": {
                "type": "string"
            }
        },
        "monitoringInstructions": {
            "type": "array",
            "items": {
                "type": "string"
            }
        },
        "incidentProcessingGuide": {
            "type": "array",
            "items": {
                "type": "string"
            }
        },
        "agentName": {
            "type": ["string","null"],
            "description": "The name of the agent to be used for the alert"
        }
    },
    "required": [
        "alertingId",
        "owningTeams",
        "kustoQueries",
        "owners",
        "incidentProcessingGuide"
    ],
    "additionalProperties": true,
    "definitions": {
        "GenevaActionConfigBase": {
            "type": "object",
            "properties": {
                "actionName": {
                    "type": "string"
                },
                "tenantId": {
                    "type": "string"
                },
                "workflowName": {
                    "type": "string"
                },
                "workflowInputParameters": {
                    "type": "array",
                    "items": {
                        "type": "string"
                    }
                }
            },
            "required": [
                "actionName",
                "tenantId",
                "workflowName",
                "workflowInputParameters"
            ],
            "additionalProperties": false
        },
        "ICMConfigKustoQueryModel": {
            "type": "object",
            "properties": {
                "cloud": {
                    "type": ["string","null"],
                    "description": "Cloud to be used for the Kusto query"
                },
                "cluster": {
                    "type": ["string","null"],
                    "description": "Cluster to be used for the Kusto query"
                },
                "database": {
                    "type": ["string","null"],
                    "description": "Database to be used for the Kusto query"
                },
                "title": {
                    "type": "string",
                    "description": "Title of the Kusto query"
                },
                "kustoQuery": {
                    "type": "string",
                    "description": "Kusto query to be executed"
                }
            },
            "required": [
                "title",
                "kustoQuery"
            ],
            "additionalProperties": false
        }
    }
}