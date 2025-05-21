import { Guid } from "../Helpers/Guid";
import { AlertInfo, IcmTeamInfo } from "../Models/Response";


const getAlertTemplate = (teamId: number, alertingId?: string) => {
    return {
        alertingId: alertingId ?? Guid.newGuid(),
        incidentTitle: '',
        owningTeams: [],
        allowedGenevaActions: [],
        kustoQueries: [{
            title: "A one line description of what the kusto query does",
            kustoQuery: "cluster('your_cluster').database('your_database').your_table | take 1"
        }],
        owners: ["alias1", "alias2"],
        actionTimeoutIntervalInMinutes: 30,
        defaultHumanInterventionLoop: '',
        incidentProcessingGuide: [
            "Fetch the incident details and generate the ask",
            "Extract the key information from the incident and create an EXECUTION_PLAN"
        ],
        agentMode: "",
        teamId: teamId
    }
}

export const generateCustomAlertConfig = (props: IcmTeamInfo) => {
    let defaultTemplate = getAlertTemplate(props.icmTeamId);
    defaultTemplate.owningTeams = [`${props.icmServiceName}/${props.icmTeamName}`];
    return defaultTemplate;
}

export const generateAzureAlertConfig = (props: AlertInfo) => {
    let defaultTemplate = getAlertTemplate(props.teamId, props.id);
    defaultTemplate.incidentTitle = props.title;
    defaultTemplate.defaultHumanInterventionLoop = `${props.serviceName}/${props.teamAssignedTo}`;
    if (props.severity) {
        defaultTemplate["severity"] = props.severity;
    }
    return defaultTemplate;
}