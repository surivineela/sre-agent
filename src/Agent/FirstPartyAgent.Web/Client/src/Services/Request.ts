import axios, { AxiosRequestConfig, Method } from "axios";
import { AgentDeployment, AlertInfo, AlertStreamPostBody, ArmListResponse, DeployAgentPostBody, GenerateInstructionsRequest, GenerateInstructionsResponse, IcmIncident, IcmService, IcmTeamInfo, IcmTeams, Location, ResourceGroup, Subscription, TeamConfig } from "../Models/Response";
import { ICMAlertConfig } from "../Models/ICMAlertConfig";
import { getAgentHeaders, getArmHeaders } from "../Helpers/Headers";


const makeRequest = async<T>(apiPath: string, method: Method, data: any = null): Promise<T | null> => {
    let url = "";
    if (apiPath.startsWith("/")) {
        url = `${apiPath}`;
    } else {
        url = `/${apiPath}`;
    }

    const request: AxiosRequestConfig = {
        method: method,
        url: url,
        data: data,
        headers: getAgentHeaders(),
    };
    const response = await axios.request(request);
    return response.data;
}

const makeArmRequest = async<T>(url: string, method: Method, data: any = null): Promise<T | null> => {

    const request: AxiosRequestConfig = {
        method: method,
        url: url,
        data: data,
        headers: getArmHeaders(),
    };
    const response = await axios.request(request);
    return response.data;
}

const get = async<T>(apiPath: string): Promise<T | null> => {
    return await makeRequest<T>(apiPath, "GET");
}

const post = async<T>(apiPath: string, data: any): Promise<T | null> => {
    return await makeRequest<T>(apiPath, "POST", data);
}

export const isHotsiteAgentConfigEnabled = async (): Promise<boolean> => {
    const path = `api/icm/isFeatureEnabled`;
    const res = await get(path);
    return res == "true";
}

export const getOnboardedLoops = async () => {
    return await get<any>('api/icm/getOnboardedLoops');
}

export const getLoops = async () => {
    return await get<TeamConfig[]>('api/icm/loops');
}

export const getLoopAlertInfo = async (loopId: string) => {
    return await get<AlertInfo[]>(`api/icm/getLoopAlerts/${loopId}`);
}

export const getLoopAlertConfigs = async (loopId?: number) => {
    const url = loopId ? `api/icm/getLoopAlertConfigs/${loopId}` : 'api/icm/getLoopAlertConfigs';
    return await get<ICMAlertConfig[]>(url);
}

export const getAlertConfig = async (loopId: string, alertId: string) => {
    return await get<any>(`api/icm/getAlertConfig/${loopId}/${alertId}`);
}

export const updateAlertConfig = async (loopId: number, alertId: string, config: ICMAlertConfig) => {
    return await post<any>(`api/icm/updateAlertConfig/${loopId}/${alertId}`, config);
}

export const createAlertConfig = async (config: ICMAlertConfig) => {
    return await post<any>(`api/icm/createAlertConfig`, config)
}

export const getAlertDefinitions = async () => {
    return await get<AlertInfo[]>('api/icm/alerts');
}

export const getIcmTeams = async () => {
    return await get<IcmTeamInfo[]>('api/icm/icmTeams');
}

export const getIcmTeamsByServiceId = async (serviceId: string) => {
    return await get<IcmTeams>(`api/icm/icmTeams/${serviceId}`);
}

export const getIcmServices = async () => {
    return await get<IcmService[]>('api/icm/icmServices');
}

// Method to get Geneva configuration
export const getGenevaConfig = async (loopId: string) => {
    return await get(`api/icm/getGenevaConfig?loopId=${loopId}`);
}

export const saveGenevaConfig = async (config: any) => {
    return await post(`api/icm/saveGenevaConfig`, config);
}

// New methods for deploy agent feature - Mock implementations
export const getSubscriptions = async (): Promise<Subscription[]> => {
    //https://management.azure.com/subscriptions?api-version=2020-01-01
    const response = await makeArmRequest<ArmListResponse<Subscription>>(
        'https://management.azure.com/subscriptions?api-version=2020-01-01', 
        'GET'
    );
    return response?.value || [];
}

export const getResourceGroups = async (subscriptionId: string): Promise<ResourceGroup[]> => {
    // https://management.azure.com/subscriptions/{subscriptionId}/resourcegroups?api-version=2021-04-01
    const response = await makeArmRequest<ArmListResponse<ResourceGroup>>(
        `https://management.azure.com/subscriptions/${subscriptionId}/resourcegroups?api-version=2021-04-01`,
        'GET'
    );
    return response?.value || [];
}

export const getLocations = async (subscriptionId: string): Promise<Location[]> => {
    // https://management.azure.com/subscriptions/{subscriptionId}/locations?api-version=2020-01-01
    const response = await makeArmRequest<ArmListResponse<Location>>(
        `https://management.azure.com/subscriptions/${subscriptionId}/locations?api-version=2020-01-01`,
        'GET'
    );
    return response?.value || [];
}

export const getAgentDeployments = async (loopId: number) => {
    return await get<AgentDeployment[]>(`api/icm/getAgentDeployments/${loopId}`);
}

export const createAgent = async (createConfig: DeployAgentPostBody) => {
    return await post<DeployAgentPostBody>('api/icm/createAgent', createConfig)
}

export const deployAgent = async (deployConfig: any) => {
    return await post<any>('api/icm/deployAgent', deployConfig);
}

export const getAgentFactoryConfig = async (configName: string) => {
    return await get<any>(`api/icm/agentFactoryConfig/${configName}`);
}

export const getAgentFactoryConfigs = async () => {
    return await get<string[]>('api/icm/agentFactoryConfigs');
}

export const saveAgentFactoryConfig = async (config: any) => {
    return await post<any>('api/icm/agentFactoryConfig', config);
}

export const getIncidents = async (teamId: number, title: string, numOfDays: number = 30) => {
    return await get<IcmIncident[]>(`api/icm/getIncidents?loopId=${teamId}&numOfDays=${numOfDays}&title=${title}`);
}

export const getDefaultIcmTeam = async () => {
    return await get<IcmTeamInfo>('api/icm/defaultIcmTeam');
}

export const generateInstructions = async (request: GenerateInstructionsRequest) => {
    return await post<GenerateInstructionsResponse>('api/icm/generateInstructions', request);
    // return await generateInstructionsMock(request);
}

export const getRequestForAlertStream = (postBody: AlertStreamPostBody): AxiosRequestConfig<AlertStreamPostBody> => {
    const url = `/api/icm/ProcessAlertStream`;
    return {
        method: "POST",
        url: url,
        data: postBody,
        headers: getAgentHeaders(),
    };
}

export const listAllContainers = async () => {
    return await get<string[]>('api/config/containers');
}

export const getAllDocumentIds = async (containerName: string) => {
    return await get<string[]>(`api/config/containers/${containerName}/documents`);
}

export const getDocumentById = async (containerName: string, documentId: string) => {
    // Expecting a JSON string as response, so using 'string' as the type.
    return await get<any>(`api/config/containers/${containerName}/documents/${documentId}`);
}

export const upsertDocument = async (containerName: string, documentJson: string) => {
    // Sending a JSON string and expecting a JSON string as response.
    return await post<string>(`api/config/containers/${containerName}/documents`, documentJson);
}