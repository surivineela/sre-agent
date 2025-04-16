import { Resource, ResourceExtended, ScoreCardObject } from "../Contracts/Graph";

const getAppHealthInfoString = (nodeProperties?: ResourceExtended | Resource) => {

    if (nodeProperties) {
        if ('properties' in nodeProperties) {
            const resourceExtended: ResourceExtended = nodeProperties as ResourceExtended;
            return resourceExtended.properties?.appHealthInfo?.[0]
        } else {
            return nodeProperties.appHealthInfo?.[0]
        }
    }
}

export const getAppHealthStatus = (nodeProperties?: ResourceExtended | Resource) => {
    const appHealthInfo = getAppHealthInfoString(nodeProperties);
    if (appHealthInfo) {
        try {
            const healthInfo: ScoreCardObject = JSON.parse(appHealthInfo);
            return healthInfo.Health;
        } catch {
            return undefined;
        }
    }

    return undefined;
}

export const getLinkId = (parentNodeId: string, childNodeId: string) => {
    return `${parentNodeId}-${childNodeId}`;
}
