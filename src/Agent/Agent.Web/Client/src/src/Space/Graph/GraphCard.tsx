import {
    Caption1,
    Text,
    Link,
    mergeClasses,
} from "@fluentui/react-components";
import { Card, CardHeader } from "@fluentui/react-components";
import { NodeProps, Node, Handle, Position } from "@xyflow/react";
import { memo, useContext } from "react";
import { GraphContext, GraphNode, HandlePosition } from "../Contracts/Graph";
import HealthStatus from "./HealthStatus";
import { getAppHealthStatus, getHandleId } from "./Utility";
import { useGraphNodeStyles } from "../Styles/Graph.styles";

export const GraphCard = (props: NodeProps<Node<GraphNode>>) => {
    const { id, data } = props;

    const {
        openPanel,
        hoverNode,
        unHoverNode,
        nodesToHightlight
    } = useContext(GraphContext)

    const { card, cardHightlight, header, headerText, description } = useGraphNodeStyles();

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
            </Card >
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


