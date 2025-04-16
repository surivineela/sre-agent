import {
    Caption1,
    Text,
    Link,
    Button,
    CardFooter,
    SkeletonItem,
    Skeleton,
    mergeClasses,
} from "@fluentui/react-components";
import { Card, CardHeader } from "@fluentui/react-components";
import { NodeProps, Node, Handle, Position } from "@xyflow/react";
import { useContext, useState } from "react";
import { GraphContext, GraphNode } from "../Contracts/Graph";
import HealthStatus from "./HealthStatus";
import { getAppHealthStatus } from "./Utility";
import { useGraphNodeStyles } from "../Styles/Graph.styles";

export const GraphCard = (props: NodeProps<Node<GraphNode>>) => {
    const { id, data } = props;

    const { showSubresources, hideSubresources, areSubresourcesVisible, isLoadingSubresources, openPanel, hoverNode, unHoverNode, nodesToHightlight } = useContext(GraphContext)

    const [subresouceVisible, setSubresourceVisible] = useState(areSubresourcesVisible(data));

    const { card, cardHightlight, header, headerText, description, footer } = useGraphNodeStyles();

    const Shimmer = () => <Skeleton><SkeletonItem style={{ width: 200, height: 170 }} /></Skeleton>;

    const Header = () => {
        return data.name &&
            <Link
                className={headerText}
                onClick={(e) => {
                    e.stopPropagation();
                    openPanel(data)
                }}>
                <Text wrap={false} block={false}>{data.name}</Text>
            </Link>
    }

    const Description = () =>
        <Caption1 wrap={false} block={false} className={`${headerText} ${description}`}>{data?.properties?.type ?? 'subscription'}</Caption1>

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handle type="target" position={Position.Left} isConnectable={false} />
            {isLoadingSubresources ?
                <Shimmer />
                : <Card className={mergeClasses(card, nodesToHightlight.includes(id) ? cardHightlight : undefined)}>
                    <CardHeader
                        className={header}
                        image={
                            <img
                                width={26}
                                height={26}
                                src={'azureResource.svg'}
                            />
                        }
                        header={<Header />}
                        description={<Description />}
                    />
                    <HealthStatus health={getAppHealthStatus(data.properties)} />
                    <CardFooter style={{ position: 'absolute', bottom: 5 }}>
                        <Button
                            onClick={(e) => {
                                e.stopPropagation();
                                if (subresouceVisible) {
                                    setSubresourceVisible(false);
                                    hideSubresources(data);
                                } else {
                                    setSubresourceVisible(true);
                                    showSubresources(data);
                                }
                            }}
                        >
                            {subresouceVisible ? 'Hide subresources' : 'Show subresources'}
                        </Button>
                    </CardFooter>
                </Card >}
            <Handle type="source" position={Position.Right} className={footer} isConnectable={false} />
        </div>

    );
};
