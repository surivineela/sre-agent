import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    mergeClasses,
    Table,
    TableBody,
    TableCell,
    TableHeader,
    TableHeaderCell,
    TableRow,
    Text,
    tokens,
} from '@fluentui/react-components';
import { CheckmarkCircle20Regular, ErrorCircle20Regular, PanelRightExpandRegular } from '@fluentui/react-icons';
import { memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import { ScheduledTasksClient } from '../../../Common/Clients/ScheduledTasksClient';
import { getAgentHeaders } from '../../../Common/Helpers/headers';
import { resolveResourceIcon } from '../../../Common/Helpers/Resources';
import {
    ConnectorsResources,
    ExtendedAgentsGraphResources,
    ScheduledTasksResources,
    SreAgentResources,
} from '../../../Strings/SREAgentResources';
import {
    ExtendedAgent,
    ExtendedAgentGraphContext,
    ExtendedAgentGraphNode,
    ExtendedAgentGraphView,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    PlaygroundEntity,
    Skill,
    SystemTool,
} from '../../Contracts/ExtendedAgentGraph';
import { PlaygroundTarget } from '../../Playground/PlaygroundModal';
import { useExtendedAgentInfoStyles } from '../../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from '../EntityIcon';
import { ExtendedEntityYamlEditor } from '../ExtendedAgentYamlEditor';
import { ExtendedEntityType } from '../ExtendedAgentYamlUtils';
import { AgentDetails } from './AgentDetails';
import { PanelHeader } from './PanelHeader';
import { SkillDetails } from './SkillDetails';
import { SystemToolDetails } from './SystemToolDetails';
import { TriggerDetails } from './TriggerDetails';

type ExtendedAgentInfoPanelProps = {
    selectedNode?: ExtendedAgentGraphNode;
    agents?: ExtendedAgent[];
    tools: ExtendedTool[];
    connectors: ExtendedConnector[];
    triggers?: ExtendedTrigger[];
    systemTools?: SystemTool[];
    sreAgentEndpoint: string;
    onRefresh?: () => Promise<void> | void;
    onDragHandlePointerDown?: (event: React.PointerEvent<HTMLDivElement>) => void;
    onResizeHandlePointerDown?: (event: React.PointerEvent<HTMLDivElement>) => void;
    width?: number;
    minWidth?: number;
    maxWidth?: number;
    onOpenPlayground?: (target: PlaygroundTarget) => void;
    onEditKustoTool?: (tool: ExtendedTool) => void;
    onEditPythonTool?: (tool: ExtendedTool) => void;
    onEditSkill?: (skill: Skill) => void;
    onClose?: () => void;
    collapsibleProps?: {
        isCollapsed: boolean;
        setCollapsed: (collapsed: boolean) => void;
    };
};

type YamlEditorContext = {
    entity: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger;
    type: ExtendedEntityType;
};

type DeleteContext = {
    type: 'agent' | 'tool' | 'skill' | 'incidentTrigger' | 'scheduledTrigger';
    entity: ExtendedAgent | ExtendedTool | Skill | ExtendedTrigger;
};

const EMPTY_DISPLAY = '-' as const;

const getConnectorTypeInfo = (connectorType: string, intl: any) => {
    switch (connectorType) {
        case 'SendOutlookEmail':
            return {
                iconSrc: resolveResourceIcon('Outlook'),
                displayText: intl.formatMessage(ConnectorsResources.outlook),
            };
        case 'Teams':
            return {
                iconSrc: resolveResourceIcon('Teams'),
                displayText: intl.formatMessage(ConnectorsResources.microsoftTeams),
            };
        case 'Kusto':
            return {
                iconSrc: resolveResourceIcon('AzureDataExplorer'),
                displayText: intl.formatMessage(ConnectorsResources.azureDataExplorer),
            };
        default:
            return null;
    }
};

const SERVICE_TYPE = {
    AZURE_DATA_EXPLORER: 'Azure Data Explorer (Kusto)',
    CUSTOM_TOOL: 'Custom Tool',
} as const;

export const ExtendedAgentInfoPanel = memo(
    ({
        selectedNode,
        agents = [],
        tools,
        connectors,
        triggers = [],
        systemTools = [],
        sreAgentEndpoint,
        onRefresh,
        onDragHandlePointerDown,
        onResizeHandlePointerDown,
        width,
        minWidth,
        maxWidth,
        onEditKustoTool,
        onEditPythonTool,
        onEditSkill,
        onClose,
        collapsibleProps,
    }: ExtendedAgentInfoPanelProps) => {
        const styles = useExtendedAgentInfoStyles();
        const intl = useIntl();
        const { triggerAgentQuickAction, triggerTriggerQuickAction, setPlaygroundEntity, onViewChange } =
            useContext(ExtendedAgentGraphContext);
        const [yamlEditorContext, setYamlEditorContext] = useState<YamlEditorContext>();
        const [isResizeHandleHovered, setIsResizeHandleHovered] = useState(false);
        const [isDeleting, setIsDeleting] = useState(false);
        const [deleteContext, setDeleteContext] = useState<DeleteContext>();
        const [documentCount, setDocumentCount] = useState<number | null>(null);

        const panelWidth = width ?? 350;
        const panelMinWidth = minWidth ?? 280;
        const panelMaxWidth = maxWidth ?? 720;

        const selectedAgent = useMemo(() => {
            if (selectedNode?.type === ExtendedAgentNodeType.Agent) {
                return selectedNode.data as ExtendedAgent;
            }

            return undefined;
        }, [selectedNode]);

        const selectedTool = useMemo(() => {
            if (selectedNode?.type === ExtendedAgentNodeType.Tool) {
                return selectedNode.data as ExtendedTool;
            }

            return undefined;
        }, [selectedNode]);

        const selectedConnector = useMemo(() => {
            if (selectedNode?.type === ExtendedAgentNodeType.Connector) {
                return selectedNode.data as ExtendedConnector;
            }

            return undefined;
        }, [selectedNode]);

        const selectedTrigger = useMemo(() => {
            if (selectedNode?.type === ExtendedAgentNodeType.Trigger) {
                return selectedNode.data as ExtendedTrigger;
            }

            return undefined;
        }, [selectedNode]);

        const selectedSystemTool = useMemo(() => {
            if (selectedNode?.type === ExtendedAgentNodeType.SystemTool) {
                return selectedNode.data as SystemTool;
            }

            return undefined;
        }, [selectedNode]);

        const selectedSkill = useMemo(() => {
            if (selectedNode?.type === ExtendedAgentNodeType.Skill) {
                return selectedNode.data as Skill;
            }

            return undefined;
        }, [selectedNode]);

        const memoryEnabled =
            selectedAgent?.tools?.some(t => t.toLowerCase() === 'searchmemory') ||
            selectedAgent?.systemTools?.some(t => t.toLowerCase() === 'searchmemory') ||
            false;

        const connectorMap = useMemo(() => new Map(connectors.map(connector => [connector.name, connector])), [connectors]);
        const triggerMap = useMemo(() => new Map(triggers.map(trigger => [trigger.name, trigger])), [triggers]);
        const systemToolMap = useMemo(() => new Map(systemTools.map(tool => [tool.name, tool])), [systemTools]);
        const toolMap = useMemo(() => new Map(tools.map(tool => [tool.name, tool])), [tools]);

        const agentToolNames = useMemo(() => {
            return [...(selectedAgent?.tools || []), ...(selectedAgent?.mcpTools || [])];
        }, [selectedAgent?.tools, selectedAgent?.mcpTools]);

        useEffect(() => {
            setYamlEditorContext(undefined);
        }, [selectedAgent?.name, selectedNode?.id, triggerMap]);

        useEffect(() => {
            if (memoryEnabled) {
                const fetchDocumentCount = async () => {
                    try {
                        const url = `${sreAgentEndpoint}/api/v1/AgentMemory/files/count`;
                        console.log('[DocumentCount] Fetching from:', url);
                        const response = await fetch(url, {
                            headers: getAgentHeaders(),
                        });
                        console.log('[DocumentCount] Response status:', response.status);
                        if (response.ok) {
                            const data = await response.json();
                            console.log('[DocumentCount] Response data:', data);
                            setDocumentCount(data.count ?? 0);
                        } else {
                            const errorText = await response.text();
                            console.error('[DocumentCount] Error response:', errorText);
                        }
                    } catch (error) {
                        console.error('[DocumentCount] Failed to fetch document count:', error);
                    }
                };
                fetchDocumentCount();
            } else {
                setDocumentCount(null);
            }
        }, [memoryEnabled, sreAgentEndpoint]);

        const handleOpenYamlEditor = useCallback(
            (entity: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger, type: ExtendedEntityType) => {
                setYamlEditorContext({ entity, type });
            },
            []
        );

        const onEdit = useCallback(
            (
                entity: ExtendedTool | ExtendedConnector | ExtendedTrigger | ExtendedAgent | Skill | undefined,
                type: ExtendedEntityType | 'skill'
            ) => {
                if (!entity) return;

                if (type === 'agent' && triggerAgentQuickAction) {
                    triggerAgentQuickAction(entity.name, 'editAgent');
                    return;
                }
                if (type === 'trigger' && triggerTriggerQuickAction) {
                    triggerTriggerQuickAction(entity.name, 'editTrigger');
                    return;
                }
                if (type === 'tool') {
                    const tool = entity as ExtendedTool;
                    if (tool.type === 'PythonFunctionTool' && onEditPythonTool) {
                        onEditPythonTool(tool);
                        return;
                    }
                    if (tool.type === 'KustoTool' && onEditKustoTool) {
                        onEditKustoTool(tool);
                        return;
                    }
                }
                if (type === 'skill' && onEditSkill) {
                    onEditSkill(entity as Skill);
                    return;
                }
                handleOpenYamlEditor(entity, type as ExtendedEntityType);
            },
            [triggerAgentQuickAction, triggerTriggerQuickAction, onEditKustoTool, onEditPythonTool, onEditSkill, handleOpenYamlEditor]
        );

        const renderToolDetails = useCallback(
            (tool: ExtendedTool) => {
                const connector = tool.connector ? connectorMap.get(tool.connector) : undefined;

                return (
                    <>
                        <div className={styles.metadataRow}>
                            <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.connector)}</Text>
                            <Text>{tool.connector || EMPTY_DISPLAY}</Text>
                        </div>

                        {tool.type !== 'mcp' && (
                            <div className={styles.metadataRow}>
                                <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.service)}</Text>
                                <div className={styles.flexRowCenter}>
                                    {tool.type === 'KustoTool' ? (
                                        <img
                                            src={resolveResourceIcon('AzureDataExplorer')}
                                            alt={intl.formatMessage(ConnectorsResources.azureDataExplorer)}
                                            className={styles.smallIcon}
                                        />
                                    ) : (
                                        <EntityIcon type="tool" shorthandStyle={{ wrapperSize: 16, iconSize: 12, borderRadius: 3 }} />
                                    )}
                                    <Text>
                                        {tool.type === 'KustoTool'
                                            ? SERVICE_TYPE.AZURE_DATA_EXPLORER
                                            : tool.type || SERVICE_TYPE.CUSTOM_TOOL}
                                    </Text>
                                </div>
                            </div>
                        )}

                        {tool.type === 'KustoTool' && (
                            <div className={styles.metadataRow}>
                                <Text className={styles.metadataKey}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoDatabaseLabel)}
                                </Text>
                                <Text>{tool.database || EMPTY_DISPLAY}</Text>
                            </div>
                        )}

                        {tool.connector && (
                            <div className={styles.metadataRow}>
                                <Text className={styles.metadataKey}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.connectorStatus)}
                                </Text>
                                <div className={styles.flexRowCenter}>
                                    {(() => {
                                        const connectorEnabled =
                                            tool.type === 'mcp' ? connector && connector.enabled !== false : connector?.enabled !== false;
                                        return connectorEnabled ? (
                                            <>
                                                <CheckmarkCircle20Regular className={styles.successIcon} />
                                                <Text>{intl.formatMessage(ExtendedAgentsGraphResources.connectedStatus)}</Text>
                                            </>
                                        ) : (
                                            <>
                                                <ErrorCircle20Regular className={styles.errorIcon} />
                                                <Text>{intl.formatMessage(ExtendedAgentsGraphResources.disconnectedStatus)}</Text>
                                            </>
                                        );
                                    })()}
                                </div>
                            </div>
                        )}

                        <div className={styles.paddingVertical10}>
                            <div className={styles.subSection}>
                                <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                                    {tool.type === 'mcp'
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.description)
                                        : intl.formatMessage(ExtendedAgentsGraphResources.instructions)}
                                </Text>
                                <Text className={styles.subText}>
                                    {tool.description || intl.formatMessage(ExtendedAgentsGraphResources.listViewDescriptionFallback)}
                                </Text>
                            </div>
                        </div>

                        {tool.type === 'KustoTool' && tool.query && (
                            <div className={styles.paddingBottom10}>
                                <div className={styles.subSection}>
                                    <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.kustoQueryLabel)}
                                    </Text>
                                    <div className={styles.subText}>{tool.query}</div>
                                </div>
                            </div>
                        )}

                        {tool.type === 'PythonFunctionTool' && tool.functionCode && (
                            <div className={styles.paddingBottom10}>
                                <div className={styles.subSection}>
                                    <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.pythonToolCodeSectionTitle)}
                                    </Text>
                                    <pre
                                        className={styles.subText}
                                        style={{
                                            fontFamily: 'Consolas, Monaco, monospace',
                                            fontSize: '12px',
                                            whiteSpace: 'pre-wrap',
                                            backgroundColor: tokens.colorNeutralBackground3,
                                            padding: '8px',
                                            borderRadius: '4px',
                                            overflow: 'auto',
                                            maxHeight: '200px',
                                        }}
                                    >
                                        {tool.functionCode}
                                    </pre>
                                </div>
                            </div>
                        )}

                        {tool.type !== 'mcp' && (
                            <div className={styles.subSection}>
                                <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
                                </Text>
                                {tool.parameters && tool.parameters.length > 0 ? (
                                    <Table>
                                        <TableHeader>
                                            <TableRow>
                                                <TableHeaderCell className={styles.tableCellTruncate}>
                                                    <Text weight="semibold" className={styles.tableCellTextTruncate}>
                                                        {intl.formatMessage(ExtendedAgentsGraphResources.parameterName)}
                                                    </Text>
                                                </TableHeaderCell>
                                                <TableHeaderCell className={styles.tableCellTruncate}>
                                                    <Text weight="semibold" className={styles.tableCellTextTruncate}>
                                                        {intl.formatMessage(ExtendedAgentsGraphResources.type)}
                                                    </Text>
                                                </TableHeaderCell>
                                                <TableHeaderCell className={styles.tableCellTruncate}>
                                                    <Text weight="semibold" className={styles.tableCellTextTruncate}>
                                                        {intl.formatMessage(ExtendedAgentsGraphResources.value)}
                                                    </Text>
                                                </TableHeaderCell>
                                            </TableRow>
                                        </TableHeader>
                                        <TableBody>
                                            {tool.parameters.map((param, index) => (
                                                <TableRow key={index}>
                                                    <TableCell className={styles.tableCellTruncate}>
                                                        <div className={styles.flexRowCenter8}>
                                                            <Text className={styles.tableCellTextTruncate}>{param.name}</Text>
                                                        </div>
                                                    </TableCell>
                                                    <TableCell className={styles.tableCellTruncate}>
                                                        <div className={styles.flexRowCenter8}>
                                                            <Text className={styles.tableCellTextTruncate}>{param.type}</Text>
                                                        </div>
                                                    </TableCell>
                                                    <TableCell className={styles.tableCellTruncate}>
                                                        <div className={styles.flexRowCenter8}>
                                                            <Text className={styles.tableCellTextTruncate}>{param.value}</Text>
                                                        </div>
                                                    </TableCell>
                                                </TableRow>
                                            ))}
                                        </TableBody>
                                    </Table>
                                ) : (
                                    <Text className={styles.emptyState}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.noParametersConfigured)}
                                    </Text>
                                )}
                            </div>
                        )}
                    </>
                );
            },
            [connectorMap, intl, styles]
        );

        const handleDeleteClick = useCallback(
            (
                type: 'agent' | 'tool' | 'skill' | 'incidentTrigger' | 'scheduledTrigger',
                entity: ExtendedAgent | ExtendedTool | Skill | ExtendedTrigger | undefined
            ) => {
                if (!entity || isDeleting) return;
                setDeleteContext({ type, entity });
            },
            [isDeleting]
        );

        const handleConfirmDelete = useCallback(async () => {
            if (!deleteContext || isDeleting) return;

            setIsDeleting(true);
            const context = deleteContext;
            setDeleteContext(undefined);

            try {
                if (context.type === 'incidentTrigger') {
                    const trigger = context.entity as ExtendedTrigger;
                    const incidentHandlerClient = new IncidentHandlerClient(sreAgentEndpoint);
                    const response = await incidentHandlerClient.deleteIncidentFilter(trigger.name);
                    if (!response.isSuccessful) {
                        throw new Error('Failed to delete incident trigger');
                    }
                } else if (context.type === 'scheduledTrigger') {
                    const trigger = context.entity as ExtendedTrigger;
                    const scheduledTasksClient = new ScheduledTasksClient(sreAgentEndpoint, () => {});
                    const response = await scheduledTasksClient.deleteScheduledTask(trigger.id ?? trigger.name);
                    if (!response.isSuccessful) {
                        throw new Error('Failed to delete scheduled task');
                    }
                } else {
                    const endpointSegment = context.type === 'agent' ? 'agents' : context.type === 'tool' ? 'tools' : 'skills';
                    const apiVersion = context.type === 'skill' ? 'v2' : 'v1';
                    const entityName = encodeURIComponent(context.entity.name);

                    const response = await fetch(`${sreAgentEndpoint}/api/${apiVersion}/extendedAgent/${endpointSegment}/${entityName}`, {
                        method: 'DELETE',
                        credentials: 'include',
                        headers: getAgentHeaders(),
                    });

                    if (!response.ok) {
                        throw new Error(`Failed to delete ${context.type}: ${response.status} ${response.statusText}`);
                    }
                }

                await onRefresh?.();
            } catch (error) {
                console.error('Error deleting entity:', error);
                let message: string;
                if (context.type === 'agent') {
                    message = intl.formatMessage(SreAgentResources.deleteAgentNotificationFailure, { count: 1, name: context.entity.name });
                } else if (context.type === 'tool') {
                    message = intl.formatMessage(SreAgentResources.deleteToolNotificationError, { name: context.entity.name });
                } else if (context.type === 'incidentTrigger') {
                    message = intl.formatMessage(SreAgentResources.deleteIncidentTriggerNotificationFailure, {
                        name: context.entity.name,
                        count: 1,
                    });
                } else if (context.type === 'scheduledTrigger') {
                    message = intl.formatMessage(SreAgentResources.deleteScheduledTaskNotificationFailure, {
                        name: context.entity.name,
                        count: 1,
                    });
                } else {
                    message = intl.formatMessage(ExtendedAgentsGraphResources.deleteSkillNotificationError, { name: context.entity.name });
                }

                alert(message);
            } finally {
                setIsDeleting(false);
            }
        }, [deleteContext, isDeleting, intl, onRefresh, sreAgentEndpoint]);

        const handleCancelDelete = useCallback(() => {
            setDeleteContext(undefined);
        }, []);

        const connectorTypeInfo = useMemo(() => {
            if (!selectedConnector?.connectorType) return null;
            return getConnectorTypeInfo(selectedConnector.connectorType, intl);
        }, [selectedConnector?.connectorType, intl]);

        const renderConnectorDetails = useCallback(
            (connector: ExtendedConnector) => (
                <>
                    {connector.connectorType && (
                        <div className={styles.metadataRow}>
                            <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.service)}</Text>
                            <div className={styles.flexRowCenter}>
                                {connectorTypeInfo ? (
                                    <>
                                        <img
                                            src={connectorTypeInfo.iconSrc}
                                            alt={connectorTypeInfo.displayText}
                                            className={styles.smallIcon}
                                        />
                                        <Text>{connectorTypeInfo.displayText}</Text>
                                    </>
                                ) : (
                                    <Text>{connector.connectorType}</Text>
                                )}
                            </div>
                        </div>
                    )}

                    {connector.dataSource && (
                        <div className={styles.metadataRow}>
                            <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.url)}</Text>
                            <Text>{connector.dataSource}</Text>
                        </div>
                    )}

                    {connector.identity && (
                        <div className={styles.metadataRow}>
                            <Text className={styles.metadataKey}>{intl.formatMessage(SreAgentResources.managedIdentity)}</Text>
                            <Text>{connector.identity}</Text>
                        </div>
                    )}
                </>
            ),
            [connectorTypeInfo, intl, styles.metadataRow, styles.metadataKey, styles.flexRowCenter, styles.smallIcon]
        );

        const isAgentContext = !selectedTool && !selectedConnector && !selectedTrigger && !selectedSystemTool && !selectedSkill;

        const headerEditContext = useMemo(() => {
            if (selectedTool) return selectedTool.type === 'mcp' ? undefined : { entity: selectedTool, type: 'tool' as const };
            if (selectedConnector) return { entity: selectedConnector, type: 'connector' as const };
            if (selectedTrigger) return { entity: selectedTrigger, type: 'trigger' as const };
            if (selectedSystemTool) return undefined;
            if (selectedSkill) return { entity: selectedSkill, type: 'skill' as const };
            if (selectedAgent) return { entity: selectedAgent, type: 'agent' as const };
            return undefined;
        }, [selectedAgent, selectedConnector, selectedTool, selectedTrigger, selectedSystemTool, selectedSkill]);

        const playgroundEntity = useMemo<PlaygroundEntity | undefined>(() => {
            if (selectedTool) {
                if (selectedTool.type === 'mcp') {
                    return {
                        entityType: 'McpTool',
                        entity: selectedTool,
                    };
                }
                return {
                    entityType: 'ExtendedTool',
                    entity: selectedTool,
                };
            }

            if (selectedSystemTool) {
                return {
                    entityType: 'SystemTool',
                    entity: selectedSystemTool,
                };
            }

            if (selectedAgent && isAgentContext) {
                return {
                    entityType: 'Agent',
                    entity: selectedAgent,
                };
            }

            return undefined;
        }, [isAgentContext, selectedAgent, selectedSystemTool, selectedTool]);

        const handleOpenPlaygroundClick = useCallback(() => {
            if (!playgroundEntity) {
                return;
            }

            setPlaygroundEntity(playgroundEntity);
            onViewChange(ExtendedAgentGraphView.Playground);
        }, [setPlaygroundEntity, onViewChange, playgroundEntity]);

        const headerIconType = useMemo(() => {
            if (selectedTool) {
                if (selectedTool.type === 'mcp') return 'windowWrenchRegular';
                if (selectedTool.type === 'KustoTool') return 'toolWithGear';
                if (selectedTool.type === 'PythonFunctionTool') return 'pythonTool';
                return 'tool';
            }
            if (selectedConnector) return 'connector';
            if (selectedTrigger) return selectedTrigger.type === 'incident' ? 'incidentTrigger' : 'scheduledTask';
            if (selectedSystemTool) return 'tool';
            if (selectedSkill) return 'skill';
            if (selectedAgent) return selectedAgent.name === 'meta_agent' ? 'metaAgent' : 'agent';
            return undefined;
        }, [selectedTool, selectedConnector, selectedTrigger, selectedSystemTool, selectedSkill, selectedAgent]);

        const headerTitle =
            selectedTool?.name ??
            selectedConnector?.name ??
            selectedTrigger?.name ??
            selectedSystemTool?.name ??
            selectedSkill?.name ??
            selectedAgent?.name ??
            intl.formatMessage(ExtendedAgentsGraphResources.agentSummaryTitle);

        const headerSubtitle = useMemo(() => {
            if (selectedTool) {
                const isMcpTool = selectedTool.type?.toLowerCase() === 'mcp';
                return isMcpTool
                    ? intl.formatMessage(ExtendedAgentsGraphResources.mcpTool)
                    : intl.formatMessage(ExtendedAgentsGraphResources.customTool);
            }
            if (selectedSystemTool) {
                return intl.formatMessage(ExtendedAgentsGraphResources.builtInTool);
            }
            if (selectedConnector) {
                return intl.formatMessage(ExtendedAgentsGraphResources.connector);
            }
            if (selectedTrigger) {
                return intl.formatMessage(
                    selectedTrigger.type === 'incident'
                        ? ExtendedAgentsGraphResources.triggerBadgeIncident
                        : ExtendedAgentsGraphResources.triggerBadgeScheduled
                );
            }
            if (selectedSkill) {
                return intl.formatMessage(ExtendedAgentsGraphResources.skill);
            }
            if (selectedAgent && isAgentContext) {
                return intl.formatMessage(ExtendedAgentsGraphResources.agent);
            }
            return '';
        }, [selectedTool, selectedSystemTool, selectedConnector, selectedTrigger, selectedSkill, selectedAgent, isAgentContext, intl]);

        const agentDetails =
            selectedAgent && !selectedTool && !selectedConnector && !selectedTrigger && !selectedSystemTool ? (
                <AgentDetails
                    agent={selectedAgent}
                    agents={agents}
                    toolNames={agentToolNames}
                    toolMap={toolMap}
                    systemToolMap={systemToolMap}
                    memoryEnabled={memoryEnabled}
                    documentCount={documentCount}
                    skillsEnabled={selectedAgent.enableSkills || (selectedAgent.allowedSkills && selectedAgent.allowedSkills.length > 0)}
                    allowedSkills={selectedAgent.allowedSkills}
                />
            ) : !selectedAgent && !collapsibleProps?.isCollapsed ? (
                <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noAgentSelected)}</Text>
            ) : null;

        const systemToolDetails = selectedSystemTool ? <SystemToolDetails systemTool={selectedSystemTool} /> : null;

        const skillDetails = selectedSkill ? <SkillDetails skill={selectedSkill} toolMap={toolMap} systemToolMap={systemToolMap} /> : null;

        const resizeHandleClassName = mergeClasses(styles.resizeHandle, isResizeHandleHovered ? styles.resizeHandleHovered : undefined);
        const resizeHandleGripClassName = mergeClasses(
            styles.resizeHandleGrip,
            isResizeHandleHovered ? styles.resizeHandleGripVisible : undefined
        );

        return (
            <>
                {!collapsibleProps?.isCollapsed ? (
                    <div className={styles.root} style={{ width: `${panelWidth}px` }}>
                        <div
                            className={resizeHandleClassName}
                            role="separator"
                            aria-orientation="vertical"
                            aria-valuemin={panelMinWidth}
                            aria-valuemax={panelMaxWidth}
                            aria-valuenow={Math.round(panelWidth)}
                            tabIndex={-1}
                            onPointerDown={event => {
                                event.preventDefault();
                                event.stopPropagation();
                                onResizeHandlePointerDown?.(event);
                            }}
                            onPointerEnter={() => setIsResizeHandleHovered(true)}
                            onPointerLeave={() => setIsResizeHandleHovered(false)}
                        >
                            <span className={resizeHandleGripClassName} aria-hidden />
                        </div>
                        <div className={styles.panel}>
                            <PanelHeader
                                headerIconType={headerIconType}
                                headerTitle={headerTitle}
                                headerSubtitle={headerSubtitle}
                                headerEditContext={headerEditContext}
                                playgroundEntity={playgroundEntity}
                                isAgentContext={isAgentContext}
                                selectedAgent={selectedAgent}
                                selectedTool={selectedTool}
                                selectedConnector={selectedConnector}
                                selectedTrigger={selectedTrigger}
                                selectedSkill={selectedSkill}
                                isDeleting={isDeleting}
                                onEdit={onEdit}
                                onDeleteClick={handleDeleteClick}
                                onOpenPlaygroundClick={handleOpenPlaygroundClick}
                                onClose={onClose}
                                onDragHandlePointerDown={onDragHandlePointerDown}
                                collapsibleProps={collapsibleProps}
                            />

                            <div className={styles.content}>
                                {selectedTool && <div className={styles.section}>{renderToolDetails(selectedTool)}</div>}
                                {selectedConnector && <div className={styles.section}>{renderConnectorDetails(selectedConnector)}</div>}
                                {selectedTrigger && (
                                    <div className={styles.section}>
                                        <TriggerDetails trigger={selectedTrigger} />
                                    </div>
                                )}
                                {agentDetails}
                                {systemToolDetails}
                                {skillDetails}
                            </div>
                        </div>
                    </div>
                ) : (
                    <div className={mergeClasses(styles.root, styles.rootCollapsed)}>
                        <div className={styles.panel}>
                            <div className={styles.header}>
                                <div className={styles.flexRowCenter4}>
                                    <Button
                                        appearance="transparent"
                                        size="small"
                                        icon={<PanelRightExpandRegular />}
                                        onClick={() => collapsibleProps.setCollapsed(false)}
                                        title={intl.formatMessage(SreAgentResources.expandPanel)}
                                        aria-label={intl.formatMessage(SreAgentResources.expandPanel)}
                                    />
                                </div>
                            </div>
                        </div>
                    </div>
                )}

                <ExtendedEntityYamlEditor
                    entity={yamlEditorContext?.entity}
                    entityType={yamlEditorContext?.type ?? 'agent'}
                    sreAgentEndpoint={sreAgentEndpoint}
                    isOpen={!!yamlEditorContext}
                    onClose={() => setYamlEditorContext(undefined)}
                    onApplied={async () => {
                        await onRefresh?.();
                        setYamlEditorContext(undefined);
                    }}
                />

                <Dialog
                    open={!!deleteContext}
                    onOpenChange={(_, data) => {
                        if (!data.open) {
                            setDeleteContext(undefined);
                        }
                    }}
                >
                    <DialogSurface>
                        <DialogBody>
                            <DialogTitle>
                                {deleteContext?.type === 'tool'
                                    ? intl.formatMessage(SreAgentResources.deleteToolTitle)
                                    : deleteContext?.type === 'skill'
                                      ? intl.formatMessage(ExtendedAgentsGraphResources.deleteSkillTitle)
                                      : deleteContext?.type === 'incidentTrigger'
                                        ? intl.formatMessage(SreAgentResources.deleteIncidentTriggerNotificationTitle, { count: 1 })
                                        : deleteContext?.type === 'scheduledTrigger'
                                          ? intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskNotificationTitleSingle)
                                          : intl.formatMessage(SreAgentResources.deleteSubagentTitle)}
                            </DialogTitle>
                            <DialogContent>
                                <Text>
                                    {deleteContext?.type === 'tool'
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.deleteExtendedToolWarning)
                                        : deleteContext?.type === 'skill'
                                          ? intl.formatMessage(ExtendedAgentsGraphResources.deleteSkillWarning)
                                          : deleteContext?.type === 'incidentTrigger'
                                            ? intl.formatMessage(SreAgentResources.deleteIncidentTriggerConfirmationDescription)
                                            : deleteContext?.type === 'scheduledTrigger'
                                              ? intl.formatMessage(ScheduledTasksResources.deleteScheduledTaskConfirmationDescriptionSingle)
                                              : intl.formatMessage(ExtendedAgentsGraphResources.deleteExtendedAgentWarning)}
                                </Text>
                            </DialogContent>
                            <DialogActions>
                                <Button appearance="secondary" onClick={handleCancelDelete} disabled={isDeleting}>
                                    {intl.formatMessage(SreAgentResources.cancel)}
                                </Button>
                                <Button appearance="primary" onClick={handleConfirmDelete} disabled={isDeleting}>
                                    {intl.formatMessage(SreAgentResources.delete)}
                                </Button>
                            </DialogActions>
                        </DialogBody>
                    </DialogSurface>
                </Dialog>
            </>
        );
    }
);

ExtendedAgentInfoPanel.displayName = 'ExtendedAgentInfoPanel';
