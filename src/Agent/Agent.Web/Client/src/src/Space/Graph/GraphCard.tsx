import {
    Text,
    Link,
    mergeClasses
} from "@fluentui/react-components";
import { Card, CardHeader } from "@fluentui/react-components";
import { NodeProps, Node, Handle, Position } from "@xyflow/react";
import { memo, useContext, useMemo } from "react";
import { GraphContext, GraphNode, HandlePosition } from "../Contracts/Graph";
import HealthStatus from "./HealthStatus";
import { getAppHealthInfo, getHandleId } from "./Utility";
import { useGraphNodeStyles } from "../Styles/Graph.styles";

// ────────────────────────────────────────────────────────────────────────────────
// Icon resolution helpers
// ────────────────────────────────────────────────────────────────────────────────
const ICON_BASE = ""; // eg: assets
const ICON_LOOKUP: Record<string, string> = {
    // Compute / containers
    containerapp: "ContainerApp.svg",
    containerappjob: "ContainerAppJob.svg",
    managedenvironment: "ManagedEnvironment.svg",

    // Kubernetes / orchestrators
    aks: "AKS.svg",
    managedcluster: "AKS.svg",
    kubernetes: "AKS.svg",
    scaleset: "ScaleSet.svg",

    // Web & Functions
    webapp: "WebApp.svg",
    functionapp: "WebApp.svg",
    site: "WebApp.svg",

    // Databases & caches
    cosmos: "CosmosDB.svg",
    cosmosdb: "CosmosDB.svg",
    sql: "SQLServer.svg",
    sqlserver: "SQLServer.svg",
    redis: "AzureRedisCache.svg",
    cache: "AzureRedisCache.svg",

    // Networking
    vnet: "Vnet.svg",
    virtualnetwork: "Vnet.svg",
    subnet: "Vnet.svg",
    nsg: "NSG.svg",
    networksecuritygroup: "NSG.svg",
};

// Friendly names for resource types
const FRIENDLY_NAMES: Record<string, string> = {
    // Compute / containers
    containerapp: "Container App",
    containerappjob: "Container App Job",
    managedenvironment: "Managed Environment",

    // Kubernetes / orchestrators
    aks: "Kubernetes Service",
    managedcluster: "Kubernetes Service",
    kubernetes: "Kubernetes Service",
    scaleset: "Scale Set",

    // Web & Functions
    webapp: "Web App",
    functionapp: "Function App",
    site: "Web App",

    // Databases & caches
    cosmos: "Cosmos DB",
    cosmosdb: "Cosmos DB",
    sql: "SQL Server",
    sqlserver: "SQL Server",
    redis: "Redis Cache",
    cache: "Redis Cache",

    // Networking
    vnet: "Virtual Network",
    virtualnetwork: "Virtual Network",
    subnet: "Subnet",
    nsg: "Network Security Group",
    networksecuritygroup: "Network Security Group",
};

const DEFAULT_ICON = "azureResource.svg";

const resolveIcon = (azureType?: string): string => {
    if (!azureType) return ICON_BASE + DEFAULT_ICON;
    const t = azureType.toLowerCase();
    const match = Object.keys(ICON_LOOKUP).find((k) => t.includes(k));
    return ICON_BASE + (match ? ICON_LOOKUP[match] : DEFAULT_ICON);
};

// Get friendly name for resource type
const getFriendlyName = (azureType?: string): string => {
    if (!azureType) return 'Subscription';
    const t = azureType.toLowerCase();
    const match = Object.keys(FRIENDLY_NAMES).find((k) => t.includes(k));
    
    if (match) {
        return FRIENDLY_NAMES[match];
    } else {
        // Extract the type from resourceType path as fallback
        const typeArray = azureType.split("/");
        return typeArray[typeArray.length - 1];
    }
};

// ────────────────────────────────────────────────────────────────────────────────
// GraphCard component
// ────────────────────────────────────────────────────────────────────────────────
export const GraphCard = (props: NodeProps<Node<GraphNode>>) => {
    const { id, data } = props;
    const { openPanel, hoverNode, unHoverNode, nodesToHightlight } =
        useContext(GraphContext);

    const { card, cardHightlight, header, headerText, description } =
        useGraphNodeStyles();

    const type = useMemo(() => {
        const resourceType = data?.properties?.type;
        if (resourceType) {
            return getFriendlyName(resourceType);
        } else {
            return 'subscription';
        }
    }, [data?.properties?.type]);

    // Title link
    const Header = () =>
        data.name ? (
            <Link
                className={headerText}
                onClick={(e) => {
                    e.stopPropagation();
                    openPanel(data);
                }}
            >
                <Text wrap={false} block={false} size={600}>
                    {data.name}
                </Text>
            </Link>
        ) : null;

    // Resource‑type subtitle
    const Description = () => (
        <Text
            wrap={false}
            block={false}
            size={400}
            className={mergeClasses(headerText, description)}
        >
            {type}
        </Text>
    );

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card
                onClick={() => {
                    openPanel(data);
                }}
                className={mergeClasses(
                    card,
                    nodesToHightlight.includes(id) ? cardHightlight : undefined
                )}
            >
                <CardHeader
                    className={header}
                    image={
                        <img
                            width={32}
                            height={32}
                            src={resolveIcon(data?.properties?.type)}
                            alt="resource icon"
                        />
                    }
                    header={<Header />}
                    description={<Description />}
                />
                {/* Updated to use getAppHealthInfo */}
                <HealthStatus health={getAppHealthInfo(data.properties)?.Health} />
            </Card>
        </div>
    );
};

// ────────────────────────────────────────────────────────────────────────────────
// Handles component
// ────────────────────────────────────────────────────────────────────────────────
const Handles = memo(() => {
    const { handle } = useGraphNodeStyles();

    const HandlePort = ({
        position,
        isTarget,
    }: {
        position: HandlePosition;
        isTarget: boolean;
    }) => {
        const pos =
            position === "T"
                ? Position.Top
                : position === "B"
                    ? Position.Bottom
                    : position === "L"
                        ? Position.Left
                        : Position.Right;
        return (
            <Handle
                type={isTarget ? "target" : "source"}
                position={pos}
                id={getHandleId(position, isTarget)}
                isConnectable={false}
                className={handle}
            />
        );
    };

    const handlePortInput: { position: HandlePosition; isTarget: boolean }[] = [
        { position: "T", isTarget: false },
        { position: "T", isTarget: true },
        { position: "B", isTarget: false },
        { position: "B", isTarget: true },
        { position: "L", isTarget: false },
        { position: "L", isTarget: true },
        { position: "R", isTarget: false },
        { position: "R", isTarget: true },
    ];

    return handlePortInput.map((port, idx) => (
        <HandlePort key={idx} position={port.position} isTarget={port.isTarget} />
    ));
});

Handles.displayName = "Handles";

export default GraphCard;
