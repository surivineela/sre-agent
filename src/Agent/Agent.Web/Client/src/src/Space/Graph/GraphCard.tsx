import { Card, CardHeader, mergeClasses, Text } from '@fluentui/react-components';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { getResourceTypeFriendlyName, resolveResourceIcon } from '../../Common/Helpers/Resources';
import { GraphContext, GraphNode, HandlePosition } from '../Contracts/Graph';
import { useGraphNodeStyles } from '../Styles/Graph.styles';
import HealthStatus from './HealthStatus';
import { getAppHealthInfo, getHandleId } from './Utility';

export const GraphCard = (props: NodeProps<Node<GraphNode>>) => {
    const { id, data } = props;
    const { hoverNode, unHoverNode, nodesToHighlight, selectedNode, setSelectedNode, hoveredNodeId, selectedAppGroupId } =
        useContext(GraphContext);

    const { card, appGroupCard, cardHighlighted, cardHovered, appGroupCardHovered, cardSelected, header, headerText, description } =
        useGraphNodeStyles();

    const isAppGroup = id === selectedAppGroupId;
    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNode?.id === id;

    const cardStyles = mergeClasses(
        card,
        isAppGroup ? appGroupCard : undefined,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? (isAppGroup ? appGroupCardHovered : cardHovered) : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    const type = useMemo(() => {
        const resourceType = data?.properties?.kind || data?.properties?.type;
        if (resourceType) {
            return getResourceTypeFriendlyName(resourceType);
        } else {
            return 'subscription';
        }
    }, [data?.properties?.type]);

    const ResourceNameHeader = () =>
        data.name ? (
            <Text className={headerText} wrap={false} block={false} size={500}>
                {data.name}
            </Text>
        ) : null;

    const ResourceTypeDescription = () => (
        <Text className={mergeClasses(headerText, description)} wrap={false} block={false} size={300}>
            {type}
        </Text>
    );

    const appHealthInfo = useMemo(() => getAppHealthInfo(data?.properties)?.Health, [data?.properties]);

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card
                onClick={() => {
                    setSelectedNode(data);
                }}
                className={cardStyles}
            >
                <CardHeader
                    className={header}
                    image={
                        <img
                            width={32}
                            height={32}
                            src={resolveResourceIcon(data?.properties?.kind)}
                            alt={data?.properties?.type ?? 'resource type icon'}
                        />
                    }
                    header={<ResourceNameHeader />}
                    description={<ResourceTypeDescription />}
                />
                <HealthStatus health={appHealthInfo} />
            </Card>
        </div>
    );
};

const Handles = memo(() => {
    const { handle } = useGraphNodeStyles();

    const HandlePort = ({ position, isTarget }: { position: HandlePosition; isTarget: boolean }) => {
        const pos =
            position === 'T' ? Position.Top : position === 'B' ? Position.Bottom : position === 'L' ? Position.Left : Position.Right;
        return (
            <Handle
                type={isTarget ? 'target' : 'source'}
                position={pos}
                id={getHandleId(position, isTarget)}
                isConnectable={false}
                className={handle}
            />
        );
    };

    const handlePortInput: { position: HandlePosition; isTarget: boolean }[] = [
        { position: 'T', isTarget: false },
        { position: 'T', isTarget: true },
        { position: 'B', isTarget: false },
        { position: 'B', isTarget: true },
        { position: 'L', isTarget: false },
        { position: 'L', isTarget: true },
        { position: 'R', isTarget: false },
        { position: 'R', isTarget: true },
    ];

    return handlePortInput.map((port, idx) => <HandlePort key={idx} position={port.position} isTarget={port.isTarget} />);
});

Handles.displayName = 'Handles';

export default GraphCard;
