import { EntityCard, EntityTitle } from '@fluentui-copilot/react-entity-cards';
import { mergeClasses } from '@fluentui/react-components';
import { Handle, Node, NodeProps, Position } from '@xyflow/react';
import { memo, useContext, useMemo } from 'react';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { getResourceTypeFriendlyName, resolveResourceIcon } from '../../Common/Helpers/Resources';
import { GraphContext, GraphNode, HandlePosition } from '../Contracts/Graph';
import { useGraphNodeStyles } from '../Styles/Graph.styles';
import HealthStatus from './HealthStatus';
import { getAppHealthInfo, getHandleId } from './Utility';

export const GraphCard = (props: NodeProps<Node<GraphNode>>) => {
    const { id, data } = props;
    const { logAmplitudeControlEvent } = useAzPortalContext();
    const { hoverNode, unHoverNode, selectedNode, setSelectedNode, selectedAppGroupId } = useContext(GraphContext);

    const styles = useGraphNodeStyles();

    const isAppGroup = id === selectedAppGroupId;
    const isSelectedNode = selectedNode?.id === id;

    const cardStyles = mergeClasses(
        styles.card,
        isAppGroup ? styles.appGroupCard : undefined,
        isSelectedNode ? styles.cardSelected : undefined
    );

    const type = useMemo(() => {
        const resourceType = data?.properties?.kind || data?.properties?.type;
        if (resourceType) {
            return getResourceTypeFriendlyName(resourceType);
        } else {
            return 'subscription';
        }
    }, [data?.properties?.type, data?.properties?.kind]);

    const appHealthInfo = useMemo(() => getAppHealthInfo(data?.properties)?.Health, [data?.properties]);

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles handleClassName={styles.handle} />
            <EntityCard
                onClick={() => {
                    setSelectedNode(data);

                    logAmplitudeControlEvent({
                        targetType: 'button',
                        targetAction: 'clicked',
                        targetName: 'graphAppGroupCard',
                        targetFriendlyName: 'Graph App Group card',
                        valueObjectName: id,
                        valueObjectFriendlyName: id,
                    });
                }}
                className={cardStyles}
                entityTitle={
                    <EntityTitle
                        media={
                            <img
                                width={32}
                                height={32}
                                src={resolveResourceIcon(data?.properties?.kind || data?.properties?.type)}
                                alt={data?.properties?.type ?? 'resource type icon'}
                            />
                        }
                        primaryText={data.name ?? ''}
                        secondaryText={type}
                    />
                }
                content={{ style: { minHeight: 'unset', marginBottom: 'unset', padding: 'unset' } }}
            >
                <HealthStatus health={appHealthInfo} />
            </EntityCard>
        </div>
    );
};

const Handles = memo(({ handleClassName }: { handleClassName: string }) => {
    const HandlePort = ({ position, isTarget }: { position: HandlePosition; isTarget: boolean }) => {
        const pos =
            position === 'T' ? Position.Top : position === 'B' ? Position.Bottom : position === 'L' ? Position.Left : Position.Right;
        return (
            <Handle
                type={isTarget ? 'target' : 'source'}
                position={pos}
                id={getHandleId(position, isTarget)}
                isConnectable={false}
                className={handleClassName}
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
