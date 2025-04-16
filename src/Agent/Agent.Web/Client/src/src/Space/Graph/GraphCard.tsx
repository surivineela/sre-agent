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
import { memo, useContext, useState } from "react";
import { GraphContext, GraphNode } from "../Contracts/Graph";
import HealthStatus from "./HealthStatus";
import { getAppHealthStatus, getHandleId } from "./Utility";
import { useGraphNodeStyles } from "../Styles/Graph.styles";
import { HandlePosition } from "./Constants";

export const GraphCard = (props: NodeProps<Node<GraphNode>>) => {
    const { id, data } = props;

    const {
        showSubresources,
        hideSubresources,
        areSubresourcesVisible,
        isLoadingSubresources,
        isComputingPosition,
        openPanel,
        hoverNode,
        unHoverNode,
        nodesToHightlight
    } = useContext(GraphContext)

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
            <Handles />
            {(isLoadingSubresources || isComputingPosition) ?
                <Shimmer /> :
                <Card className={mergeClasses(card, nodesToHightlight.includes(id) ? cardHightlight : undefined)}>
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
                    <CardFooter className={footer}>
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
        </div>

    );
};

const Handles = memo(() => {
    const { handle } = useGraphNodeStyles();

    const HandlePort = ({ position, isTarget }: { position: HandlePosition, isTarget: boolean }) => {
        const pos = position === 'T' ? Position.Top : position === 'B' ? Position.Bottom : position === 'L' ? Position.Left : Position.Right;
        return <Handle type={isTarget ? 'target' : 'source'} position={pos} id={getHandleId(position, isTarget)} isConnectable={false} className={handle} />;
    }

    const handlePortInput: { position: HandlePosition, isTarget: boolean }[] =
        [
            { position: 'T', isTarget: false },
            { position: 'T', isTarget: true },
            { position: 'B', isTarget: false },
            { position: 'B', isTarget: true },
            { position: 'L', isTarget: false },
            { position: 'L', isTarget: true },
            { position: 'R', isTarget: false },
            { position: 'R', isTarget: true }
        ];

    return (handlePortInput.map((port, index) => (
        <HandlePort key={index} position={port.position} isTarget={port.isTarget} />
    )));
});

Handles.displayName = 'Handles';


