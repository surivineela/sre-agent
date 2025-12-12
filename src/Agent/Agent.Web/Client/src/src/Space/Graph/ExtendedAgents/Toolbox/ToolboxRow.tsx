import { Badge, Card, mergeClasses, Text, Tooltip } from '@fluentui/react-components';
import { useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { McpServerResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgentGraphContext, ExtendedConnector, ExtendedTool, SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { ConnectorType, getConnectorIcon } from '../../../Settings/Connectors/Wizard/Common/ConnectorType';
import { useToolNodeStyles } from '../../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../../EntityIcon';
import { useToolboxCardStyles } from './ToolboxCard.styles';

interface ToolboxRowProps {
    agentName: string;
    tool: ExtendedTool | SystemTool;
    connector?: ExtendedConnector;
    isSystemTool: boolean;
}

export const ToolboxRow = ({ agentName, tool, connector, isSystemTool }: ToolboxRowProps) => {
    const intl = useIntl();
    const { selectedNodeId, setSelectedNodeId, expandInfoPanel } = useContext(ExtendedAgentGraphContext);

    const { toolCard, connectorCard, cardSelected, cardContent, titleRow, nameBlock, nameText } = useToolNodeStyles();
    const { compactToolCard } = useToolboxCardStyles();

    const toolId = `toolbox_tool_${agentName}_${tool.name}`;
    const connectorId = connector ? `toolbox_connector_${agentName}_${tool.name}_${connector.name}` : undefined;

    const isToolSelected = selectedNodeId === toolId;
    const isConnectorSelected = connectorId && selectedNodeId === connectorId;

    const extendedTool = isSystemTool ? undefined : (tool as ExtendedTool);
    const systemTool = isSystemTool ? (tool as SystemTool) : undefined;
    const toolType = isSystemTool ? (systemTool?.category ?? 'System tool') : (extendedTool?.type ?? 'Tool');
    const isPythonTool = toolType === 'PythonFunctionTool';

    const isMcpTool = useMemo(() => {
        return extendedTool?.type?.toLowerCase() === 'mcp';
    }, [extendedTool?.type]);

    const toolIconType = useMemo(() => {
        if (isSystemTool) {
            return 'toolWithGear';
        }
        if (isPythonTool) {
            return 'pythonTool';
        }
        if (isMcpTool) {
            return 'windowWrenchRegular';
        }
        return 'tool';
    }, [isPythonTool, isSystemTool, isMcpTool]);

    // Get connector icon based on connector type
    const connectorIconSrc = useMemo(() => {
        if (!connector?.connectorType) return null;
        return getConnectorIcon(connector.connectorType as ConnectorType, intl);
    }, [connector?.connectorType, intl]);

    const toolCardStyles = mergeClasses(toolCard, compactToolCard, isToolSelected ? cardSelected : undefined);
    const connectorCardStyles = mergeClasses(connectorCard, compactToolCard, isConnectorSelected ? cardSelected : undefined);

    const handleToolClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        setSelectedNodeId(toolId);
        expandInfoPanel();
    };

    const handleConnectorClick = (e: React.MouseEvent) => {
        e.stopPropagation();
        if (connectorId) {
            setSelectedNodeId(connectorId);
            expandInfoPanel();
        }
    };

    return (
        <div style={{ display: 'flex', gap: '8px', marginLeft: '2px', marginRight: '2px' }}>
            <Card onClick={handleToolClick} className={toolCardStyles}>
                <div className={cardContent}>
                    <div className={titleRow}>
                        <EntityIcon type={toolIconType} iconStyle={{ height: '24px', width: '24px' }} />
                        <div className={nameBlock}>
                            <Text className={nameText}>{tool.name}</Text>
                        </div>
                        <div style={{ display: 'flex', gap: '8px', flexShrink: 0 }}>
                            {!isSystemTool && extendedTool?.connector && !isMcpTool && (
                                <Badge appearance="outline" size="tiny">
                                    {extendedTool.connector}
                                </Badge>
                            )}
                            {!isSystemTool && isMcpTool && (
                                <Badge appearance="filled" size="small" color={'informative'}>
                                    {intl.formatMessage(McpServerResources.mcp)}
                                </Badge>
                            )}
                            {isSystemTool && systemTool?.resourceType && (
                                <Badge appearance="outline" size="tiny">
                                    {systemTool.resourceType}
                                </Badge>
                            )}
                        </div>
                    </div>
                </div>
            </Card>

            {!isSystemTool && extendedTool?.connector && !isMcpTool && connector && (
                <Card onClick={handleConnectorClick} className={connectorCardStyles}>
                    <div className={cardContent}>
                        <div className={titleRow}>
                            <Tooltip content={connector.name} relationship="label">
                                {connectorIconSrc && connectorIconSrc ? (
                                    <div
                                        style={{
                                            display: 'flex',
                                            flexDirection: 'column',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            height: '36px',
                                            width: '36px',
                                        }}
                                    >
                                        <img src={connectorIconSrc} alt={connector.name} style={{ height: '24px', width: '24px' }} />
                                    </div>
                                ) : (
                                    <EntityIcon type="connector" iconStyle={{ height: '24px', width: '24px' }} />
                                )}
                            </Tooltip>
                        </div>
                    </div>
                </Card>
            )}
        </div>
    );
};

ToolboxRow.displayName = 'ToolboxRow';
