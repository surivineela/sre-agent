import {
    Badge,
    Button,
    Caption1,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Link,
    Menu,
    MenuButton,
    MenuItem,
    MenuList,
    MenuPopover,
    MenuTrigger,
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
import {
    ArrowRightRegular,
    Beaker20Regular,
    CheckmarkCircle20Regular,
    Delete20Regular,
    Dismiss20Regular,
    Edit20Regular,
    ErrorCircle20Regular,
    MoreHorizontal20Regular,
    PanelRightContractRegular,
    PanelRightExpandRegular,
} from '@fluentui/react-icons';
import { memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useLocation, useNavigate } from 'react-router-dom';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { resolveResourceIcon } from '../../Common/Helpers/Resources';
import { SettingNames, useConfigSetting } from '../../Common/Hooks/ConfigSettings';
import {
    ConnectorsResources,
    ExtendedAgentsGraphResources,
    PlaygroundResources,
    SettingsTabResources,
    SreAgentResources,
} from '../../Strings/SREAgentResources';
import {
    ExtendedAgent,
    ExtendedAgentGraphContext,
    ExtendedAgentNodeType,
    ExtendedConnector,
    ExtendedTool,
    ExtendedTrigger,
    SystemTool,
} from '../Contracts/ExtendedAgentGraph';
import { PlaygroundTarget } from '../Playground/PlaygroundModal';
import { useExtendedAgentInfoStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { ExtendedEntityYamlEditor } from './ExtendedAgentYamlEditor';
import { ExtendedEntityType } from './ExtendedAgentYamlUtils';
import { parseCronExpression } from './Utility';

type ExtendedAgentInfoPanelProps = {
    agents?: ExtendedAgent[];
    selectedAgent?: ExtendedAgent;
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
    type: 'agent' | 'tool';
    entity: ExtendedAgent | ExtendedTool;
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
        agents = [],
        selectedAgent,
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
        onOpenPlayground,
        onEditKustoTool,
        onClose,
        collapsibleProps,
    }: ExtendedAgentInfoPanelProps) => {
        const showAgentBuilderPlayground = useConfigSetting(SettingNames.ShowAgentBuilderPlayground);
        const styles = useExtendedAgentInfoStyles();
        const intl = useIntl();
        const navigate = useNavigate();
        const location = useLocation();
        const { selectedNode, triggerAgentQuickAction, triggerTriggerQuickAction } = useContext(ExtendedAgentGraphContext);
        const [yamlEditorContext, setYamlEditorContext] = useState<YamlEditorContext>();
        const [isResizeHandleHovered, setIsResizeHandleHovered] = useState(false);
        const [isDeleting, setIsDeleting] = useState(false);
        const [deleteContext, setDeleteContext] = useState<DeleteContext>();
        const [documentCount, setDocumentCount] = useState<number | null>(null);

        const panelWidth = width ?? 350;
        const panelMinWidth = minWidth ?? 280;
        const panelMaxWidth = maxWidth ?? 720;

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
            (entity: ExtendedTool | ExtendedConnector | ExtendedTrigger | ExtendedAgent | undefined, type: ExtendedEntityType) => {
                if (!entity) return;

                if (type === 'agent' && triggerAgentQuickAction) {
                    triggerAgentQuickAction(entity.name, 'editAgent');
                    return;
                }
                if (type === 'trigger' && triggerTriggerQuickAction) {
                    triggerTriggerQuickAction(entity.name, 'editTrigger');
                    return;
                }
                if (type === 'tool' && onEditKustoTool) {
                    onEditKustoTool(entity as ExtendedTool);
                    return;
                }
                handleOpenYamlEditor(entity, type);
            },
            [triggerAgentQuickAction, triggerTriggerQuickAction, onEditKustoTool, handleOpenYamlEditor]
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
                                        const connectorEnabled = connector?.enabled !== false;
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
                                    {intl.formatMessage(ExtendedAgentsGraphResources.instructions)}
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

        const renderTriggerDetails = useCallback(
            (trigger: ExtendedTrigger) => {
                return (
                    <>
                        <div className={styles.metadataRow}>
                            <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.statusLabel)}</Text>
                            <div className={styles.badgeRow}>
                                <Badge
                                    appearance={trigger.status === 'Active' ? 'tint' : 'outline'}
                                    size="medium"
                                    color={trigger.status === 'Active' ? 'success' : 'danger'}
                                >
                                    {trigger.status === 'Active'
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.onLabel)
                                        : intl.formatMessage(ExtendedAgentsGraphResources.offLabel)}
                                </Badge>
                            </div>
                        </div>

                        <div className={styles.metadataRow}>
                            <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.subagent)}</Text>
                            <Text>{trigger?.subAgent || trigger?.data?.agentName}</Text>
                        </div>
                        {trigger.type === 'incident' && (
                            <>
                                <div className={styles.metadataRow}>
                                    <Text className={styles.metadataKey}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.severityLabel)}
                                    </Text>
                                    <Text>{trigger.severity ?? EMPTY_DISPLAY}</Text>
                                </div>

                                <div className={styles.metadataRow}>
                                    <Text className={styles.metadataKey}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.triggerIncidentTypeLabel)}
                                    </Text>
                                    <Text>{trigger.incidentType ?? EMPTY_DISPLAY}</Text>
                                </div>

                                <div className={styles.metadataRow}>
                                    <Text className={styles.metadataKey}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.incidentImpactedService)}
                                    </Text>
                                    <Text>{trigger.impactedService ?? EMPTY_DISPLAY}</Text>
                                </div>
                                <div className={styles.metadataRow}>
                                    <Text className={styles.metadataKey}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.agentAutonomy)}
                                    </Text>
                                    <Text>{intl.formatMessage(SreAgentResources.reviewWord)}</Text>
                                </div>
                            </>
                        )}

                        {trigger.type === 'scheduled' && (
                            <>
                                <div className={styles.metadataRow}>
                                    <Text className={styles.metadataKey}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.scheduleTitle)}
                                    </Text>
                                    <Text>
                                        {parseCronExpression(
                                            trigger.schedule || trigger.cronExpression || intl.formatMessage(SreAgentResources.NA)
                                        )}
                                    </Text>
                                </div>
                                <div className={styles.metadataRow}>
                                    <Text className={styles.metadataKey}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.agentAutonomy)}
                                    </Text>
                                    <Text>{intl.formatMessage(SreAgentResources.autonomousWord)}</Text>
                                </div>
                            </>
                        )}

                        <div className={styles.instructionsSection}>
                            <Text className={styles.sectionTitle}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.instructionsTitle)}
                            </Text>
                            <Text className={styles.instructions}>
                                {trigger?.data?.description ||
                                    trigger?.description ||
                                    intl.formatMessage(ExtendedAgentsGraphResources.listViewDescriptionFallback)}
                            </Text>
                        </div>

                        {trigger.type === 'incident' && (
                            <div className={styles.subSection}>
                                <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.incidents)}
                                </Text>
                                <Text color={tokens.colorNeutralForeground3}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.incidentDescription)}
                                </Text>
                                <Button
                                    appearance="outline"
                                    icon={<ArrowRightRegular />}
                                    className={styles.actionButton}
                                    onClick={handleGoToIncidents}
                                >
                                    {intl.formatMessage(ExtendedAgentsGraphResources.goToIncidents)}
                                </Button>
                            </div>
                        )}

                        {trigger.type === 'scheduled' && (
                            <div className={styles.subSection}>
                                <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom8)}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.runs)}
                                </Text>
                                <Text>{intl.formatMessage(ExtendedAgentsGraphResources.runDescription)}</Text>
                                <Button
                                    appearance="outline"
                                    icon={<ArrowRightRegular />}
                                    className={styles.actionButton}
                                    onClick={handleGoToScheduled}
                                >
                                    {intl.formatMessage(ExtendedAgentsGraphResources.goToScheduledTasks)}
                                </Button>
                            </div>
                        )}
                    </>
                );
            },
            [
                intl,
                styles.metadataRow,
                styles.metadataKey,
                styles.badgeRow,
                styles.instructionsSection,
                styles.sectionTitle,
                styles.instructions,
                styles.subSection,
                styles.marginBottom8,
                styles.actionButton,
            ]
        );

        const handleDeleteClick = useCallback(
            (type: 'agent' | 'tool', entity: ExtendedAgent | ExtendedTool | undefined) => {
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

            const endpointSegment = context.type === 'agent' ? 'agents' : 'tools';
            const entityName = encodeURIComponent(context.entity.name);

            try {
                const response = await fetch(`${sreAgentEndpoint}/api/v1/extendedAgent/${endpointSegment}/${entityName}`, {
                    method: 'DELETE',
                    credentials: 'include',
                    headers: getAgentHeaders(),
                });

                if (!response.ok) {
                    throw new Error(`Failed to delete ${context.type}: ${response.status} ${response.statusText}`);
                }

                await onRefresh?.();
            } catch (error) {
                console.error('Error deleting entity:', error);
                const message =
                    context.type === 'agent'
                        ? intl.formatMessage(SreAgentResources.deleteAgentNotificationError, { name: context.entity.name })
                        : intl.formatMessage(SreAgentResources.deleteToolNotificationError, { name: context.entity.name });

                alert(message);
            } finally {
                setIsDeleting(false);
            }
        }, [deleteContext, isDeleting, intl, onRefresh, sreAgentEndpoint]);

        const handleCancelDelete = useCallback(() => {
            setDeleteContext(undefined);
        }, []);

        const handleGoToIncidents = useCallback(() => {
            navigate({ ...location, pathname: '/views/incidentmanagement' });
        }, [navigate, location]);

        const handleGoToScheduled = useCallback(() => {
            navigate({ ...location, pathname: '/views/scheduledtasks' });
        }, [navigate, location]);

        const selectedTool = selectedNode?.type === ExtendedAgentNodeType.Tool ? (selectedNode.data as ExtendedTool) : undefined;
        const selectedConnector =
            selectedNode?.type === ExtendedAgentNodeType.Connector ? (selectedNode.data as ExtendedConnector) : undefined;
        const selectedTrigger = selectedNode?.type === ExtendedAgentNodeType.Trigger ? (selectedNode.data as ExtendedTrigger) : undefined;
        const selectedSystemTool = selectedNode?.type === ExtendedAgentNodeType.SystemTool ? (selectedNode.data as SystemTool) : undefined;

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

        const isAgentContext = !selectedTool && !selectedConnector && !selectedTrigger && !selectedSystemTool;

        const headerEditContext = useMemo(() => {
            if (selectedTool) return selectedTool.type === 'mcp' ? undefined : { entity: selectedTool, type: 'tool' as const };
            if (selectedConnector) return { entity: selectedConnector, type: 'connector' as const };
            if (selectedTrigger) return { entity: selectedTrigger, type: 'trigger' as const };
            if (selectedSystemTool) return undefined;
            if (selectedAgent) return { entity: selectedAgent, type: 'agent' as const };
            return undefined;
        }, [selectedAgent, selectedConnector, selectedTool, selectedTrigger, selectedSystemTool]);

        const playgroundTarget = useMemo<PlaygroundTarget | undefined>(() => {
            if (selectedTool) {
                if (selectedTool.type === 'mcp') {
                    return undefined;
                }
                const owningAgent = selectedAgent?.tools?.includes(selectedTool.name ?? '') ? selectedAgent : undefined;
                return {
                    type: 'tool',
                    tool: selectedTool,
                    agent: owningAgent,
                };
            }

            if (selectedSystemTool) {
                return {
                    type: 'systemTool',
                    tool: selectedSystemTool,
                    agent: selectedAgent,
                };
            }

            if (selectedAgent && isAgentContext) {
                return {
                    type: 'agent',
                    agent: selectedAgent,
                };
            }

            return undefined;
        }, [isAgentContext, selectedAgent, selectedSystemTool, selectedTool]);

        const handleOpenPlaygroundClick = useCallback(() => {
            if (!playgroundTarget) {
                return;
            }

            onOpenPlayground?.(playgroundTarget);
        }, [onOpenPlayground, playgroundTarget]);

        const headerIconType = useMemo(() => {
            if (selectedTool) return 'toolWithGear';
            if (selectedConnector) return 'connector';
            if (selectedTrigger) return selectedTrigger.type === 'incident' ? 'incidentTrigger' : 'scheduledTask';
            if (selectedSystemTool) return 'tool';
            if (selectedAgent) return selectedAgent.name === 'meta_agent' ? 'metaAgent' : 'agent';
            return undefined;
        }, [selectedTool, selectedConnector, selectedTrigger, selectedSystemTool, selectedAgent?.name]);

        const headerTitle =
            selectedTool?.name ??
            selectedConnector?.name ??
            selectedTrigger?.name ??
            selectedSystemTool?.name ??
            selectedAgent?.name ??
            intl.formatMessage(ExtendedAgentsGraphResources.agentSummaryTitle);

        const headerSubtitle = useMemo(() => {
            if (selectedTool) {
                return intl.formatMessage(ExtendedAgentsGraphResources.customTool);
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
            if (selectedAgent && isAgentContext) {
                return intl.formatMessage(ExtendedAgentsGraphResources.agent);
            }
            return '';
        }, [selectedTool, selectedSystemTool, selectedConnector, selectedTrigger, selectedAgent, isAgentContext, intl]);

        const agentDetails =
            selectedAgent && !selectedTool && !selectedConnector && !selectedTrigger && !selectedSystemTool ? (
                <>
                    <div className={styles.paddingVertical10}>
                        <div className={styles.badgeRow}>
                            <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, {
                                    count: agentToolNames.length,
                                })}
                            </Badge>
                            <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.handoffCountBadge, {
                                    count: selectedAgent.handoffs?.length ?? 0,
                                })}
                            </Badge>
                            {memoryEnabled && (
                                <Badge appearance="outline" size="medium" className={styles.neutralBadge}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.memoryEnabledBadge)}
                                </Badge>
                            )}
                        </div>
                        {memoryEnabled && documentCount !== null && (
                            <div className={styles.marginTopLeft}>
                                <Link onClick={() => navigate('/views/settings/knowledgeBase')} className={styles.knowledgeBaseLink}>
                                    {documentCount > 0
                                        ? `View ${documentCount} documents in Knowledge Base`
                                        : 'No documents in Knowledge Base - Add documents'}
                                </Link>
                            </div>
                        )}
                    </div>

                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.instructionsTitle)}</Text>
                        {selectedAgent.instructions && selectedAgent.instructions.trim().length > 0 ? (
                            <textarea readOnly value={selectedAgent.instructions} className={styles.textArea} />
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noInstructions)}</Text>
                        )}
                        {selectedAgent.handoffDescription && (
                            <div className={styles.handoffSection}>
                                <Text className={mergeClasses(styles.sectionTitle, styles.marginBottom10)}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.agentHandoffInstructions)}
                                </Text>
                                <textarea readOnly value={selectedAgent.handoffDescription} className={styles.textAreaSmall} />
                            </div>
                        )}
                    </div>

                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.tools)}</Text>
                        {agentToolNames.length > 0 ? (
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHeaderCell className={styles.tableCellTruncate}>
                                            <Text
                                                weight="semibold"
                                                className={styles.tableCellTextTruncate}
                                                title={intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                                            >
                                                {intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                                            </Text>
                                        </TableHeaderCell>
                                        <TableHeaderCell className={styles.tableCellTruncate}>
                                            <Text
                                                weight="semibold"
                                                className={styles.tableCellTextTruncate}
                                                title={intl.formatMessage(ExtendedAgentsGraphResources.description)}
                                            >
                                                {intl.formatMessage(ExtendedAgentsGraphResources.description)}
                                            </Text>
                                        </TableHeaderCell>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {agentToolNames.map(name => {
                                        const tool = toolMap.get(name);
                                        const systemTool = systemToolMap.get(name);

                                        let iconType: 'tool' | 'toolWithGear' = 'tool';
                                        let description = tool?.description || EMPTY_DISPLAY;

                                        if (systemTool) {
                                            iconType = 'tool';
                                            description =
                                                systemTool.description ||
                                                intl.formatMessage(ExtendedAgentsGraphResources.listViewDescriptionFallback);
                                        } else if (tool?.type === 'KustoTool') {
                                            iconType = 'toolWithGear';
                                        }

                                        return (
                                            <TableRow key={`tool-${name}`}>
                                                <TableCell className={styles.tableCellTruncate}>
                                                    <div className={styles.flexRowCenter8}>
                                                        <EntityIcon
                                                            type={iconType}
                                                            shorthandStyle={{ wrapperSize: 20, iconSize: 16, borderRadius: 4 }}
                                                        />
                                                        <Text title={name} className={styles.tableCellTextTruncate}>
                                                            {name}
                                                        </Text>
                                                    </div>
                                                </TableCell>
                                                <TableCell className={styles.tableCellTruncate}>
                                                    <div className={styles.flexRowCenter8}>
                                                        <Text title={description} className={styles.tableCellTextTruncate}>
                                                            {description}
                                                        </Text>
                                                    </div>
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}
                                </TableBody>
                            </Table>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noTools)}</Text>
                        )}
                    </div>

                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.handoffsSectionTitle)}</Text>
                        {selectedAgent.handoffs && selectedAgent.handoffs.length > 0 ? (
                            <Table>
                                <TableHeader>
                                    <TableRow>
                                        <TableHeaderCell className={styles.tableCellTruncate}>
                                            <Text
                                                weight="semibold"
                                                className={styles.tableCellTextTruncate}
                                                title={intl.formatMessage(ExtendedAgentsGraphResources.agentName)}
                                            >
                                                {intl.formatMessage(ExtendedAgentsGraphResources.agentName)}
                                            </Text>
                                        </TableHeaderCell>
                                        <TableHeaderCell className={styles.tableCellTruncate}>
                                            <Text
                                                weight="semibold"
                                                className={styles.tableCellTextTruncate}
                                                title={intl.formatMessage(ExtendedAgentsGraphResources.tools)}
                                            >
                                                {intl.formatMessage(ExtendedAgentsGraphResources.tools)}
                                            </Text>
                                        </TableHeaderCell>
                                    </TableRow>
                                </TableHeader>
                                <TableBody>
                                    {selectedAgent.handoffs.map(handoffAgentName => {
                                        const handoffAgent = agents.find(agent => agent.name === handoffAgentName);

                                        const explicitSystemTools = handoffAgent?.systemTools ?? [];
                                        const implicitSystemTools =
                                            handoffAgent?.tools?.filter(toolName => systemToolMap.has(toolName)) ?? [];
                                        const systemToolCount = Array.from(
                                            new Set([...explicitSystemTools, ...implicitSystemTools])
                                        ).length;

                                        const kustoToolCount =
                                            handoffAgent?.tools?.filter(toolName => {
                                                const tool = toolMap.get(toolName);
                                                return tool?.type === 'KustoTool';
                                            })?.length ?? 0;

                                        return (
                                            <TableRow key={handoffAgentName}>
                                                <TableCell className={styles.tableCellTruncate}>
                                                    <div className={styles.flexRowCenter8}>
                                                        <Text title={handoffAgentName} className={styles.tableCellTextTruncate}>
                                                            {handoffAgentName}
                                                        </Text>
                                                    </div>
                                                </TableCell>
                                                <TableCell className={styles.tableCellTruncate}>
                                                    <div className={styles.flexRowCenter8}>
                                                        {systemToolCount === 0 && kustoToolCount === 0 ? (
                                                            <Text title={EMPTY_DISPLAY} className={styles.tableCellTextTruncate}>
                                                                {EMPTY_DISPLAY}
                                                            </Text>
                                                        ) : (
                                                            <>
                                                                {systemToolCount > 0 && (
                                                                    <div className={styles.flexRowCenter4}>
                                                                        <EntityIcon
                                                                            type="tool"
                                                                            shorthandStyle={{
                                                                                wrapperSize: 20,
                                                                                iconSize: 16,
                                                                                borderRadius: 3,
                                                                            }}
                                                                        />
                                                                        <Text
                                                                            title={systemToolCount.toString()}
                                                                            className={styles.tableCellTextTruncate}
                                                                        >
                                                                            {systemToolCount}
                                                                        </Text>
                                                                    </div>
                                                                )}

                                                                {kustoToolCount > 0 && (
                                                                    <div className={styles.flexRowCenter4}>
                                                                        <EntityIcon
                                                                            type="toolWithGear"
                                                                            shorthandStyle={{
                                                                                wrapperSize: 20,
                                                                                iconSize: 16,
                                                                                borderRadius: 3,
                                                                            }}
                                                                        />
                                                                        <Text
                                                                            title={kustoToolCount.toString()}
                                                                            className={styles.tableCellTextTruncate}
                                                                        >
                                                                            {kustoToolCount}
                                                                        </Text>
                                                                    </div>
                                                                )}
                                                            </>
                                                        )}
                                                    </div>
                                                </TableCell>
                                            </TableRow>
                                        );
                                    })}
                                </TableBody>
                            </Table>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noHandoffs)}</Text>
                        )}
                    </div>
                </>
            ) : !selectedAgent && !collapsibleProps?.isCollapsed ? (
                <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noAgentSelected)}</Text>
            ) : null;

        const systemToolDetails = selectedSystemTool ? (
            <>
                <div className={styles.paddingVertical10}>
                    <div className={styles.metadataRow}>
                        <Text className={styles.metadataKey}>{intl.formatMessage(ExtendedAgentsGraphResources.category)}</Text>
                        <Text>{selectedSystemTool.category}</Text>
                    </div>
                </div>

                {selectedSystemTool.description && (
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.toolDescriptionLabel)}</Text>
                        <Text className={styles.subtitle}>{selectedSystemTool.description}</Text>
                    </div>
                )}

                {selectedSystemTool.name?.toLowerCase() === 'searchmemory' && (
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connectsTo)}</Text>
                        <div className={styles.flexColumnGap8}>
                            <Link
                                appearance="subtle"
                                onClick={() => navigate('/views/settings/dataKnowledgeSpace')}
                                className={styles.flexRowCenter}
                            >
                                {intl.formatMessage(SettingsTabResources.knowledgeBase)}
                            </Link>
                            <Link
                                appearance="subtle"
                                onClick={() => navigate('/views/settings/data-connectors')}
                                className={styles.flexRowCenter}
                            >
                                {intl.formatMessage(SettingsTabResources.dataConnectors)}
                            </Link>
                        </div>
                    </div>
                )}

                {selectedSystemTool.parameters && selectedSystemTool.parameters.length > 0 && (
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
                        </Text>
                        <Table>
                            <TableHeader>
                                <TableRow>
                                    <TableHeaderCell className={styles.tableCellTruncate}>
                                        <Text
                                            weight="semibold"
                                            className={styles.tableCellTextTruncate}
                                            title={intl.formatMessage(ExtendedAgentsGraphResources.parameter)}
                                        >
                                            {intl.formatMessage(ExtendedAgentsGraphResources.parameter)}
                                        </Text>
                                    </TableHeaderCell>
                                </TableRow>
                            </TableHeader>
                            <TableBody>
                                {selectedSystemTool.parameters.map((param: string, index: number) => (
                                    <TableRow key={index}>
                                        <TableCell className={styles.tableCellTruncate}>
                                            <div className={styles.flexRowCenter8}>
                                                <Text title={param} className={styles.tableCellTextTruncate}>
                                                    {param}
                                                </Text>
                                            </div>
                                        </TableCell>
                                    </TableRow>
                                ))}
                            </TableBody>
                        </Table>
                    </div>
                )}
            </>
        ) : null;

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
                            <div className={styles.header}>
                                <div
                                    className={styles.headerInfo}
                                    onPointerDown={event => {
                                        if (event.button !== 0) return;
                                        onDragHandlePointerDown?.(event);
                                    }}
                                >
                                    <div className={styles.headerIconAndText}>
                                        {headerIconType && (
                                            <div className={styles.flexShrinkNone}>
                                                <EntityIcon
                                                    type={headerIconType}
                                                    shorthandStyle={{ wrapperSize: 40, iconSize: 28, borderRadius: 8 }}
                                                />
                                            </div>
                                        )}
                                        <div className={styles.headerTitleAndSubtitle}>
                                            <Text weight="semibold" size={500} className={styles.headerTitleText}>
                                                {headerTitle}
                                            </Text>
                                            {headerSubtitle && <Caption1>{headerSubtitle}</Caption1>}
                                        </div>
                                    </div>
                                </div>
                                <div className={styles.flexRowCenter4}>
                                    {headerEditContext && headerEditContext.type !== 'connector' && (
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            icon={<Edit20Regular />}
                                            onClick={() => onEdit(headerEditContext.entity, headerEditContext.type)}
                                            title={intl.formatMessage(ExtendedAgentsGraphResources.yamlOpenButton)}
                                        />
                                    )}
                                    {((playgroundTarget && showAgentBuilderPlayground) ||
                                        (headerEditContext?.type === 'agent' && isAgentContext && selectedAgent) ||
                                        (headerEditContext?.type === 'tool' && selectedTool)) && (
                                        <Menu>
                                            <MenuTrigger disableButtonEnhancement>
                                                <MenuButton appearance="subtle" size="small" icon={<MoreHorizontal20Regular />} />
                                            </MenuTrigger>
                                            <MenuPopover>
                                                <MenuList>
                                                    {showAgentBuilderPlayground && playgroundTarget && (
                                                        <MenuItem icon={<Beaker20Regular />} onClick={handleOpenPlaygroundClick}>
                                                            {intl.formatMessage(PlaygroundResources.openPlaygroundButton)}
                                                        </MenuItem>
                                                    )}
                                                    {headerEditContext?.type === 'agent' && isAgentContext && selectedAgent && (
                                                        <MenuItem
                                                            icon={<Delete20Regular />}
                                                            onClick={() => handleDeleteClick('agent', selectedAgent)}
                                                            disabled={isDeleting}
                                                        >
                                                            {intl.formatMessage(SreAgentResources.deleteSubagentTitle)}
                                                        </MenuItem>
                                                    )}
                                                    {headerEditContext?.type === 'tool' && selectedTool && (
                                                        <MenuItem
                                                            icon={<Delete20Regular />}
                                                            onClick={() => handleDeleteClick('tool', selectedTool)}
                                                            disabled={isDeleting}
                                                        >
                                                            {intl.formatMessage(SreAgentResources.deleteToolTitle)}
                                                        </MenuItem>
                                                    )}
                                                </MenuList>
                                            </MenuPopover>
                                        </Menu>
                                    )}
                                    {onClose && (
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            icon={<Dismiss20Regular />}
                                            onClick={onClose}
                                            title={intl.formatMessage(SreAgentResources.closePanel)}
                                            aria-label={intl.formatMessage(SreAgentResources.closePanel)}
                                        />
                                    )}
                                    {collapsibleProps && (
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            icon={<PanelRightContractRegular />}
                                            onClick={() => collapsibleProps.setCollapsed(true)}
                                            title={intl.formatMessage(SreAgentResources.collapsePanel)}
                                            aria-label={intl.formatMessage(SreAgentResources.collapsePanel)}
                                        />
                                    )}
                                </div>
                            </div>

                            <div className={styles.content}>
                                {selectedTool && <div className={styles.section}>{renderToolDetails(selectedTool)}</div>}
                                {selectedConnector && <div className={styles.section}>{renderConnectorDetails(selectedConnector)}</div>}
                                {selectedTrigger && <div className={styles.section}>{renderTriggerDetails(selectedTrigger)}</div>}
                                {agentDetails}
                                {systemToolDetails}
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
                                    : intl.formatMessage(SreAgentResources.deleteSubagentTitle)}
                            </DialogTitle>
                            <DialogContent>
                                <Text>
                                    {deleteContext?.type === 'tool'
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.deleteExtendedToolWarning)
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
