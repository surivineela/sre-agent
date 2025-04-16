import { Resource, ResourceExtended, ScoreCardObject } from "../Contracts/Graph";
import { HandlePosition } from "./Constants";

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

export const getHandleId = (position: HandlePosition, isTarget: boolean) => {
    return `${position}-${isTarget ? 'in' : 'out'}`;
}

export const getSourceAndTargetHandleId = (leftCenterPos: { x: number, y: number }, rightcenterPos: { x: number, y: number }): { sourceHandle: string; targetHandle: string } => {

    const getHandlePosition = (x: number, y: number, position: HandlePosition) => {
        switch (position) {
            case 'T':
                return { x: x, y: y - 85 };
            case 'B':
                return { x: x, y: y + 85 };
            case 'L':
                return { x: x - 100, y: y };
            case 'R':
                return { x: x + 100, y: y };
            default:
                return { x, y };
        }
    }

    const getDistance = (x1: number, y1: number, x2: number, y2: number) => {
        return (x2 - x1) ** 2 + (y2 - y1) ** 2;
    }

    let minDistance = Number.MAX_VALUE;
    let sourceHandle = getHandleId('T', false);
    let targetHandle = getHandleId('B', true);
    const positions: HandlePosition[] = ['T', 'B', 'L', 'R']

    const sourceNodeHandlePos = [
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[0]),
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[1]),
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[2]),
        getHandlePosition(leftCenterPos.x, leftCenterPos.y, positions[3])
    ];
    const targetNodeHandlePos = [
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[0]),
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[1]),
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[2]),
        getHandlePosition(rightcenterPos.x, rightcenterPos.y, positions[3])
    ];


    for (let i = 0; i < 4; i++) {
        for (let j = 0; j < 4; j++) {
            const sourcePos = sourceNodeHandlePos[i];
            const targetPos = targetNodeHandlePos[j];

            const distance = getDistance(sourcePos.x, sourcePos.y, targetPos.x, targetPos.y);

            if (distance < minDistance) {
                minDistance = distance
                sourceHandle = getHandleId(positions[i], false);
                targetHandle = getHandleId(positions[j], true);
            }
        }
    }

    return {
        sourceHandle,
        targetHandle
    }
}
