import { Card, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { Handle, Node, NodeProps } from '@xyflow/react';
import { memo, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedAgentGraphNode, ExtendedConnector } from '../Contracts/ExtendedAgentGraph';
import { HANDLE_POSITION_MAP, HANDLE_POSITIONS } from '../Contracts/Graph';
import { Badge } from '../Foundry/common/components/src/Badge/Badge';
import { McpConnectorStatus } from '../Settings/Connectors/Connectors';
import { ConnectorType, getConnectorIcon, getConnectorName } from '../Settings/Connectors/Wizard/Common/ConnectorType';
import { useConnectorNodeStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { getHandleId } from './Utility';

const Handles = memo(() => {
    const { handle } = useConnectorNodeStyles();

    return (
        <>
            {HANDLE_POSITIONS.map(position => (
                <div key={position}>
                    <Handle className={handle} type="target" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, true)} />
                    <Handle className={handle} type="source" position={HANDLE_POSITION_MAP[position]} id={getHandleId(position, false)} />
                </div>
            ))}
        </>
    );
});

Handles.displayName = 'ConnectorHandles';

export const ConnectorCard = (props: NodeProps<Node<ExtendedAgentGraphNode>>) => {
    const { id, data } = props;
    const { hoverNode, unHoverNode, nodesToHighlight, selectedNodeId, setSelectedNodeId, expandInfoPanel, hoveredNodeId } =
        useContext(ExtendedAgentGraphContext);

    const {
        connectorCard,
        cardHighlighted,
        cardHovered,
        cardSelected,
        cardContent,
        titleRow,
        nameBlock,
        nameText,
        subtitleText,
        badge,
        badgeRow,
    } = useConnectorNodeStyles();
    const intl = useIntl();
    const { sreAgentEndpoint } = useContext(EnvironmentContext);

    const isHovered = hoveredNodeId === id;
    const isSelectedNode = selectedNodeId === id;

    const connector = data?.data as ExtendedConnector | undefined;
    const isEnabled = connector?.enabled ?? true;
    const isMcpConnector = connector?.connectorType === ConnectorType.McpServer;

    const [mcpStatus, setMcpStatus] = useState<string | null>(null);

    const cardStyles = mergeClasses(
        connectorCard,
        !isHovered && nodesToHighlight.includes(id) ? cardHighlighted : undefined,
        isHovered ? cardHovered : undefined,
        isSelectedNode ? cardSelected : undefined
    );

    useEffect(() => {
        if (!isMcpConnector || !sreAgentEndpoint || !connector?.name) {
            return;
        }

        const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

        extendedAgentClient.getConnectorStatus(connector.name).then(response => {
            if (response.isSuccessful && response.content) {
                setMcpStatus(response.content.status);
            }
        });
    }, [isMcpConnector, sreAgentEndpoint, connector?.name]);

    const statusBadgeElement = useMemo(() => {
        let backgroundColor: string | undefined;
        let color: string | undefined;
        let borderColor: string | undefined;
        switch (!!isEnabled) {
            case true:
                backgroundColor = tokens.colorStatusSuccessBackground1;
                color = tokens.colorStatusSuccessForeground1;
                borderColor = tokens.colorStatusSuccessBorder1;
                break;
            case false:
                backgroundColor = tokens.colorNeutralBackgroundDisabled;
                color = tokens.colorNeutralForegroundDisabled;
                borderColor = tokens.colorNeutralForegroundDisabled;
                break;
            default:
                break;
        }

        return (
            <Badge
                appearance="outline"
                size="small"
                className={badge}
                style={{
                    backgroundColor: backgroundColor,
                    color: color,
                    borderColor: borderColor,
                }}
            >
                {isEnabled
                    ? intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusEnabled)
                    : intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusDisabled)}
            </Badge>
        );
    }, [badge, intl, isEnabled]);

    const mcpStatusBadge = useMemo(() => {
        if (!isMcpConnector || !mcpStatus) {
            return null;
        }

        let backgroundColor: string | undefined;
        let color: string | undefined;
        let borderColor: string | undefined;

        switch (mcpStatus) {
            case McpConnectorStatus.Connected:
                backgroundColor = tokens.colorStatusSuccessBackground1;
                color = tokens.colorStatusSuccessForeground1;
                borderColor = tokens.colorStatusSuccessBorder1;
                break;
            case McpConnectorStatus.Failed:
            case McpConnectorStatus.Disconnected:
                backgroundColor = tokens.colorStatusDangerBackground1;
                color = tokens.colorStatusDangerForeground1;
                borderColor = tokens.colorStatusDangerBorder1;
                break;
            case McpConnectorStatus.Initializing:
                backgroundColor = tokens.colorStatusWarningBackground1;
                color = tokens.colorStatusWarningForeground1;
                borderColor = tokens.colorStatusWarningBorder1;
                break;
            default:
                backgroundColor = tokens.colorNeutralBackground3;
                color = tokens.colorNeutralForeground2;
                borderColor = tokens.colorNeutralStroke2;
                break;
        }

        return (
            <Badge
                appearance="outline"
                size="small"
                className={badge}
                style={{
                    backgroundColor,
                    color,
                    borderColor,
                }}
            >
                {mcpStatus}
            </Badge>
        );
    }, [badge, isMcpConnector, mcpStatus]);

    return (
        <div onMouseEnter={() => hoverNode(id)} onMouseLeave={() => unHoverNode()}>
            <Handles />
            <Card
                onClick={() => {
                    setSelectedNodeId(data?.id);
                    expandInfoPanel();
                }}
                className={cardStyles}
            >
                <div className={cardContent}>
                    <div className={titleRow}>
                        {connector?.connectorType === ConnectorType.McpServer ? (
                            <img
                                style={{ height: '24px', width: '24px' }}
                                src={getConnectorIcon(connector?.connectorType as ConnectorType, intl)}
                                alt={getConnectorName(connector?.connectorType as ConnectorType, intl)}
                            />
                        ) : (
                            <EntityIcon type="connector" iconStyle={{ height: '24px', width: '24px' }} />
                        )}
                        <div className={nameBlock}>
                            <Text className={nameText}>{data?.name}</Text>
                            <Text className={subtitleText}>{intl.formatMessage(ExtendedAgentsGraphResources.connector)}</Text>
                        </div>
                    </div>

                    <div className={badgeRow}>
                        {!isMcpConnector && statusBadgeElement}
                        {mcpStatusBadge}
                    </div>
                </div>
            </Card>
        </div>
    );
};

ConnectorCard.displayName = 'ConnectorCard';
