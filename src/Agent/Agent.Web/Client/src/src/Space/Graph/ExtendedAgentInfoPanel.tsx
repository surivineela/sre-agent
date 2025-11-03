import {
    Badge,
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Link,
    mergeClasses,
    Text,
    tokens,
} from '@fluentui/react-components';
import { Beaker20Regular, Delete20Regular, Edit20Regular } from '@fluentui/react-icons';
import { memo, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useNavigate } from 'react-router-dom';
import { getAgentHeaders } from '../../Common/Helpers/headers';
import { ExtendedAgentsGraphResources, PlaygroundResources, SreAgentResources } from '../../Strings/SREAgentResources';
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
import { ExtendedEntityYamlEditor } from './ExtendedAgentYamlEditor';
import { ExtendedEntityType } from './ExtendedAgentYamlUtils';

type ExtendedAgentInfoPanelProps = {
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
};

type YamlEditorContext = {
    entity: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger;
    type: ExtendedEntityType;
};

type DeleteContext = {
    type: 'agent' | 'tool';
    entity: ExtendedAgent | ExtendedTool;
};

const getAgentTypeLabel = (agentType: ExtendedAgent['agentType'], intl: ReturnType<typeof useIntl>) => {
    switch (agentType) {
        case 'Orchestrator':
            return intl.formatMessage(ExtendedAgentsGraphResources.orchestrator);
        case 'Activity':
            return intl.formatMessage(ExtendedAgentsGraphResources.activity);
        case 'Autonomous':
        default:
            return intl.formatMessage(ExtendedAgentsGraphResources.autonomous);
    }
};

export const ExtendedAgentInfoPanel = memo(
    ({
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
    }: ExtendedAgentInfoPanelProps) => {
        const styles = useExtendedAgentInfoStyles();
        const intl = useIntl();
        const navigate = useNavigate();
        const { selectedNode } = useContext(ExtendedAgentGraphContext);
        const [yamlEditorContext, setYamlEditorContext] = useState<YamlEditorContext>();
        const [isResizeHandleHovered, setIsResizeHandleHovered] = useState(false);
        const [isDeleting, setIsDeleting] = useState(false);
        const [deleteContext, setDeleteContext] = useState<DeleteContext>();
        const [documentCount, setDocumentCount] = useState<number | null>(null);

        const panelWidth = width ?? 350;
        const panelMinWidth = minWidth ?? 280;
        const panelMaxWidth = maxWidth ?? 720;

        // Memory is enabled if the SearchMemory tool is available in the agent's tools
        const memoryEnabled =
            selectedAgent?.tools?.some(t => t.toLowerCase() === 'searchmemory') ||
            selectedAgent?.systemTools?.some(t => t.toLowerCase() === 'searchmemory') ||
            false;

        // Fetch document count when memory is enabled
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

        // Keep a tool map so we can validate tool names referenced by the agent.
        const toolMap = useMemo(() => new Map(tools.map(tool => [tool.name, tool])), [tools]);
        const connectorMap = useMemo(() => new Map(connectors.map(connector => [connector.name, connector])), [connectors]);
        const triggerMap = useMemo(() => new Map(triggers.map(trigger => [trigger.name, trigger])), [triggers]);
        const systemToolMap = useMemo(() => new Map(systemTools.map(tool => [tool.name, tool])), [systemTools]);

        const agentToolNames = useMemo(() => {
            if (!selectedAgent?.tools?.length) {
                return [] as string[];
            }

            return selectedAgent.tools.filter(toolName => toolMap.has(toolName));
        }, [selectedAgent?.tools, toolMap]);

        const agentSystemToolNames = useMemo(() => {
            if (!selectedAgent) {
                return [] as string[];
            }

            const explicit = selectedAgent.systemTools ?? [];
            const fallback = selectedAgent.tools?.filter(toolName => systemToolMap.has(toolName)) ?? [];

            return Array.from(new Set<string>([...explicit, ...fallback].filter(Boolean)));
        }, [selectedAgent, systemToolMap]);

        useEffect(() => {
            setYamlEditorContext(undefined);
        }, [selectedAgent?.name, selectedNode?.id, triggerMap]);

        const handleOpenYamlEditor = useCallback(
            (entity: ExtendedAgent | ExtendedTool | ExtendedConnector | ExtendedTrigger | undefined, type: ExtendedEntityType) => {
                if (!entity) return;
                setYamlEditorContext({ entity, type });
            },
            []
        );

        const renderStringList = useCallback(
            (items: string[] | undefined, emptyMessage: string) => {
                if (!items || items.length === 0) {
                    return <Text className={styles.emptyState}>{emptyMessage}</Text>;
                }
                return (
                    <div className={styles.list}>
                        {items.map(item => (
                            <div key={item} className={styles.listItem}>
                                <Text>{item}</Text>
                            </div>
                        ))}
                    </div>
                );
            },
            [styles.emptyState, styles.list, styles.listItem]
        );

        const renderToolDetails = useCallback(
            (tool: ExtendedTool) => {
                const connector = tool.connector ? connectorMap.get(tool.connector) : undefined;

                return (
                    <>
                        <div className={styles.subSection}>
                            <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.toolTypeLabel)}</Text>
                            <Text>{tool.type}</Text>
                        </div>
                        <div className={styles.subSection}>
                            <Text className={styles.sectionTitle}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.toolDescriptionLabel)}
                            </Text>
                            <Text>{tool.description ?? intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}</Text>
                        </div>
                        <div className={styles.subSection}>
                            <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorLabel)}</Text>
                            <Text>{tool.connector ?? intl.formatMessage(SreAgentResources.NA)}</Text>
                        </div>
                        <div className={styles.subSection}>
                            <Text className={styles.sectionTitle}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
                            </Text>
                            {tool.parameters && tool.parameters.length > 0 ? (
                                <div className={styles.list}>
                                    {tool.parameters.map(parameter => {
                                        const paramName = parameter.name || intl.formatMessage(SreAgentResources.NA);
                                        const paramType = (parameter.type || 'string').toUpperCase();
                                        const isRequired = parameter.required !== false;

                                        return (
                                            <div key={paramName} className={styles.listItem}>
                                                <div className={styles.listItemHeader}>
                                                    <Text weight="semibold" className={styles.metadataKey}>
                                                        {paramName}
                                                    </Text>
                                                    <Badge size="small" appearance="tint" color="informative">
                                                        {paramType}
                                                    </Badge>
                                                </div>
                                                {!isRequired && (
                                                    <div className={styles.listItemBadges}>
                                                        <Badge size="tiny" appearance="ghost" color="informative">
                                                            {intl.formatMessage(ExtendedAgentsGraphResources.optional)}
                                                        </Badge>
                                                    </div>
                                                )}
                                                {parameter.description && (
                                                    <Text size={200} className={styles.subtitle}>
                                                        {parameter.description}
                                                    </Text>
                                                )}
                                            </div>
                                        );
                                    })}
                                </div>
                            ) : (
                                <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noParameters)}</Text>
                            )}
                        </div>
                        {connector && (
                            <div className={styles.subSection}>
                                <Text className={styles.sectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.connectorDetailsTitle, {
                                        name: connector.name,
                                    })}
                                </Text>
                                <div className={styles.list}>
                                    <div className={styles.listItem}>
                                        <div className={styles.listItemHeader}>
                                            <Text className={styles.metadataKey}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.connectorTypeLabel)}
                                            </Text>
                                            <Text size={200} className={styles.subtitle}>
                                                {connector.type}
                                            </Text>
                                        </div>
                                    </div>
                                    <div className={styles.listItem}>
                                        <div className={styles.listItemHeader}>
                                            <Text className={styles.metadataKey}>
                                                {intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusLabel)}
                                            </Text>
                                            <Badge
                                                size="small"
                                                appearance={(connector.enabled ?? true) ? 'tint' : 'filled'}
                                                color={(connector.enabled ?? true) ? 'success' : 'danger'}
                                                className={styles.statusBadge}
                                            >
                                                {(connector.enabled ?? true)
                                                    ? intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusEnabled)
                                                    : intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusDisabled)}
                                            </Badge>
                                        </div>
                                    </div>
                                </div>
                            </div>
                        )}
                        {tool.type === 'KustoTool' && (
                            <div className={styles.subSection}>
                                <Text className={styles.sectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoConfigurationTitle)}
                                </Text>
                                <div className={styles.list}>
                                    {tool.mode && (
                                        <div className={styles.listItem}>
                                            <div className={styles.listItemHeader}>
                                                <Text weight="semibold" className={styles.metadataKey}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoModeLabel)}
                                                </Text>
                                                <Badge size="small" appearance="tint" color="informative">
                                                    {tool.mode.toUpperCase()}
                                                </Badge>
                                            </div>
                                        </div>
                                    )}
                                    {tool.database && (
                                        <div className={styles.listItem}>
                                            <div className={styles.listItemHeader}>
                                                <Text weight="semibold" className={styles.metadataKey}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoDatabaseLabel)}
                                                </Text>
                                            </div>
                                            <Text size={200} className={styles.subtitle}>
                                                {tool.database}
                                            </Text>
                                        </div>
                                    )}
                                    {tool.query && (
                                        <div className={styles.listItem}>
                                            <div className={styles.listItemHeader}>
                                                <Text weight="semibold" className={styles.metadataKey}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoQueryLabel)}
                                                </Text>
                                            </div>
                                            <pre
                                                style={{
                                                    fontSize: tokens.fontSizeBase200,
                                                    background: tokens.colorNeutralBackground2,
                                                    color: tokens.colorNeutralForeground1,
                                                    padding: tokens.spacingHorizontalS,
                                                    borderRadius: tokens.borderRadiusSmall,
                                                    border: `1px solid ${tokens.colorNeutralStroke2}`,
                                                    whiteSpace: 'pre-wrap',
                                                    wordWrap: 'break-word',
                                                    maxHeight: '200px',
                                                    overflow: 'auto',
                                                    fontFamily: tokens.fontFamilyMonospace,
                                                }}
                                            >
                                                {tool.query}
                                            </pre>
                                        </div>
                                    )}
                                    {tool.function && (
                                        <div className={styles.listItem}>
                                            <div className={styles.listItemHeader}>
                                                <Text weight="semibold" className={styles.metadataKey}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoFunctionLabel)}
                                                </Text>
                                            </div>
                                            <Text size={200} className={styles.subtitle}>
                                                {tool.function}
                                            </Text>
                                        </div>
                                    )}
                                    {tool.clusterUri && (
                                        <div className={styles.listItem}>
                                            <div className={styles.listItemHeader}>
                                                <Text weight="semibold" className={styles.metadataKey}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.kustoClusterUriLabel)}
                                                </Text>
                                            </div>
                                            <Text size={200} className={styles.subtitle}>
                                                {tool.clusterUri}
                                            </Text>
                                        </div>
                                    )}
                                </div>
                            </div>
                        )}
                    </>
                );
            },
            [connectorMap, intl, styles.list, styles.listItem, styles.sectionTitle, styles.subtitle, styles.emptyState, styles.subSection]
        );

        const renderConnectorDetails = useCallback(
            (connector: ExtendedConnector) => (
                <>
                    <div className={styles.badgeRow}>
                        <Badge appearance={(connector.enabled ?? true) ? 'tint' : 'outline'} size="small">
                            {(connector.enabled ?? true)
                                ? intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusEnabled)
                                : intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusDisabled)}
                        </Badge>
                    </div>
                    <div className={styles.subSection}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorTypeLabel)}</Text>
                        <Text>{connector.type}</Text>
                    </div>
                    <div className={styles.subSection}>
                        <Text className={styles.sectionTitle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.connectorDescriptionLabel)}
                        </Text>
                        <Text>{connector.description ?? intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}</Text>
                    </div>
                    <div className={styles.subSection}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connectorAuthLabel)}</Text>
                        <Text>{connector.auth?.type ?? intl.formatMessage(SreAgentResources.NA)}</Text>
                    </div>
                </>
            ),
            [intl, styles.badgeRow, styles.sectionTitle, styles.subSection]
        );

        const renderTriggerDetails = useCallback(
            (trigger: ExtendedTrigger) => (
                <>
                    <div className={styles.badgeRow}>
                        <Badge
                            appearance={trigger.enabled ? 'tint' : 'outline'}
                            size="small"
                            color={trigger.type === 'incident' ? 'danger' : 'informative'}
                        >
                            {intl.formatMessage(
                                trigger.type === 'incident'
                                    ? ExtendedAgentsGraphResources.triggerBadgeIncident
                                    : ExtendedAgentsGraphResources.triggerBadgeScheduled
                            )}
                        </Badge>
                        <Badge appearance={trigger.enabled ? 'tint' : 'outline'} size="small">
                            {trigger.enabled
                                ? intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusEnabled)
                                : intl.formatMessage(ExtendedAgentsGraphResources.connectorStatusDisabled)}
                        </Badge>
                    </div>
                    <div className={styles.subSection}>
                        <Text className={styles.sectionTitle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.triggerDescriptionLabel)}
                        </Text>
                        <Text>{trigger.description ?? intl.formatMessage(ExtendedAgentsGraphResources.noDescription)}</Text>
                    </div>
                    {trigger.type === 'incident' && (
                        <>
                            <div className={styles.subSection}>
                                <Text className={styles.sectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerSeverityLabel)}
                                </Text>
                                <Text>{trigger.severity ?? intl.formatMessage(SreAgentResources.NA)}</Text>
                            </div>
                            <div className={styles.subSection}>
                                <Text className={styles.sectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.triggerServiceLabel)}
                                </Text>
                                <Text>{trigger.service ?? intl.formatMessage(SreAgentResources.NA)}</Text>
                            </div>
                        </>
                    )}
                    {trigger.type === 'scheduled' && (
                        <div className={styles.subSection}>
                            <Text className={styles.sectionTitle}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.triggerSchedulePresetLabel)}
                            </Text>
                            <Text>{trigger.schedule ?? intl.formatMessage(SreAgentResources.NA)}</Text>
                        </div>
                    )}
                    <div className={styles.subSection}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.triggerAgentLabel)}</Text>
                        <Text>{trigger.agentName ?? intl.formatMessage(SreAgentResources.NA)}</Text>
                    </div>
                </>
            ),
            [intl, styles.badgeRow, styles.sectionTitle, styles.subSection]
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

        const selectedTool = selectedNode?.type === ExtendedAgentNodeType.Tool ? (selectedNode.data as ExtendedTool) : undefined;
        const selectedConnector =
            selectedNode?.type === ExtendedAgentNodeType.Connector ? (selectedNode.data as ExtendedConnector) : undefined;
        const selectedTrigger = selectedNode?.type === ExtendedAgentNodeType.Trigger ? (selectedNode.data as ExtendedTrigger) : undefined;
        const selectedSystemTool = selectedNode?.type === ExtendedAgentNodeType.SystemTool ? (selectedNode.data as SystemTool) : undefined;

        const isAgentContext = !selectedTool && !selectedConnector && !selectedTrigger && !selectedSystemTool;

        const headerEditContext = useMemo(() => {
            if (selectedTool) return { entity: selectedTool, type: 'tool' as const };
            if (selectedConnector) return { entity: selectedConnector, type: 'connector' as const };
            if (selectedTrigger) return { entity: selectedTrigger, type: 'trigger' as const };
            if (selectedSystemTool) return undefined; // System tools are read-only
            if (selectedAgent) return { entity: selectedAgent, type: 'agent' as const };
            return undefined;
        }, [selectedAgent, selectedConnector, selectedTool, selectedTrigger, selectedSystemTool]);

        const playgroundTarget = useMemo<PlaygroundTarget | undefined>(() => {
            if (selectedTool) {
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

        const headerTitle =
            selectedTool?.name ??
            selectedConnector?.name ??
            selectedTrigger?.name ??
            selectedSystemTool?.name ??
            selectedAgent?.name ??
            intl.formatMessage(ExtendedAgentsGraphResources.agentSummaryTitle);

        const headerSubtitle = selectedTool
            ? `${intl.formatMessage(ExtendedAgentsGraphResources.toolTypeLabel)}: ${
                  selectedTool.type ?? intl.formatMessage(SreAgentResources.NA)
              }`
            : selectedConnector
              ? `${intl.formatMessage(ExtendedAgentsGraphResources.connectorTypeLabel)}: ${
                    selectedConnector.type ?? intl.formatMessage(SreAgentResources.NA)
                }`
              : selectedTrigger
                ? `Trigger Type: ${selectedTrigger.type === 'incident' ? 'Incident' : 'Scheduled'}`
                : selectedSystemTool
                  ? `System Tool - Category: ${selectedSystemTool.category}`
                  : selectedAgent
                    ? intl.formatMessage(ExtendedAgentsGraphResources.filteredAgentLabel, { name: selectedAgent.name })
                    : intl.formatMessage(ExtendedAgentsGraphResources.noAgentSelected);

        const agentDetails =
            selectedAgent && !selectedTool && !selectedConnector && !selectedTrigger && !selectedSystemTool ? (
                <>
                    <div className={styles.summary}>
                        <Text size={400} weight="semibold">
                            {selectedAgent.name}
                        </Text>
                        <div className={styles.badgeRow}>
                            {selectedAgent.agentType && (
                                <Badge appearance="outline" size="small">
                                    {getAgentTypeLabel(selectedAgent.agentType, intl)}
                                </Badge>
                            )}
                            {memoryEnabled && (
                                <Badge appearance="outline" size="small">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.memoryEnabledBadge)}
                                </Badge>
                            )}
                            {selectedAgent.outputType && (
                                <Badge appearance="outline" size="small">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.outputTypeLabel)}: {selectedAgent.outputType}
                                </Badge>
                            )}
                        </div>
                        {memoryEnabled && documentCount !== null && (
                            <div style={{ marginTop: '8px', marginLeft: '8px' }}>
                                <Link
                                    onClick={() => navigate('/views/settings/dataKnowledgeSpace')}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '6px',
                                        cursor: 'pointer',
                                        fontSize: '12px',
                                        color: '#0078D4',
                                        textDecoration: 'underline',
                                    }}
                                >
                                    {documentCount > 0
                                        ? intl.formatMessage(ExtendedAgentsGraphResources.memoryKnowledgeBasePrompt, {
                                              count: documentCount,
                                          })
                                        : intl.formatMessage(ExtendedAgentsGraphResources.memoryNoDocuments)}
                                </Link>
                            </div>
                        )}
                        <div className={styles.badgeRow}>
                            <Badge appearance="tint" size="small">
                                {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, {
                                    count: agentToolNames.length,
                                })}
                            </Badge>
                            <Badge appearance="tint" size="small">
                                {intl.formatMessage(ExtendedAgentsGraphResources.systemToolsCountBadge, {
                                    count: agentSystemToolNames.length,
                                })}
                            </Badge>
                            <Badge appearance="tint" size="small">
                                {intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsCountBadge, {
                                    count: selectedAgent.mcpTools?.length ?? 0,
                                })}
                            </Badge>
                            <Badge appearance="tint" size="small">
                                {intl.formatMessage(ExtendedAgentsGraphResources.handoffCountBadge, {
                                    count: selectedAgent.handoffs?.length ?? 0,
                                })}
                            </Badge>
                            <Badge appearance="tint" size="small">
                                {intl.formatMessage(ExtendedAgentsGraphResources.agentAsToolCountBadge, {
                                    count: selectedAgent.agentsAsTools?.length ?? 0,
                                })}
                            </Badge>
                        </div>
                    </div>

                    {/* Instructions / Handoff Description */}
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.instructionsTitle)}</Text>
                        {selectedAgent.instructions && selectedAgent.instructions.trim().length > 0 ? (
                            <Text className={styles.instructions}>{selectedAgent.instructions}</Text>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noInstructions)}</Text>
                        )}
                        {selectedAgent.handoffDescription && (
                            <div className={styles.subSection}>
                                <Text className={styles.sectionTitle}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.handoffDescriptionTitle)}
                                </Text>
                                <Text className={styles.instructions}>{selectedAgent.handoffDescription}</Text>
                            </div>
                        )}
                    </div>

                    {/* Tools: simple, uncluttered list of names (validated against known tools) */}
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.toolsSectionTitle)}</Text>
                        {agentToolNames.length > 0 ? (
                            <div className={styles.list}>
                                {agentToolNames.map(name => (
                                    <div key={name} className={styles.listItem}>
                                        <Text>{name}</Text>
                                    </div>
                                ))}
                            </div>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noTools)}</Text>
                        )}
                    </div>

                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.systemToolsSectionTitle)}
                        </Text>
                        {agentSystemToolNames.length > 0 ? (
                            <div className={styles.list}>
                                {agentSystemToolNames.map(name => {
                                    const systemTool = systemToolMap.get(name);
                                    return (
                                        <div key={name} className={styles.listItem}>
                                            <Text weight="semibold">{name}</Text>
                                            {systemTool?.pluginName && (
                                                <Text size={200} className={styles.subtitle}>
                                                    {intl.formatMessage(ExtendedAgentsGraphResources.systemToolPluginLabel)}:{' '}
                                                    {systemTool.pluginName}
                                                </Text>
                                            )}
                                        </div>
                                    );
                                })}
                            </div>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noSystemTools)}</Text>
                        )}
                    </div>

                    {/* MCP Tools */}
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.mcpToolsSectionTitle)}</Text>
                        {selectedAgent.mcpTools && selectedAgent.mcpTools.length > 0 ? (
                            <div className={styles.list}>
                                {selectedAgent.mcpTools.map(name => (
                                    <div key={name} className={styles.listItem}>
                                        <Text>{name}</Text>
                                    </div>
                                ))}
                            </div>
                        ) : (
                            <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noMcpTools)}</Text>
                        )}
                    </div>

                    {/* Handoffs */}
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.handoffsSectionTitle)}</Text>
                        {renderStringList(selectedAgent.handoffs, intl.formatMessage(ExtendedAgentsGraphResources.noHandoffs))}
                    </div>
                </>
            ) : !selectedAgent ? (
                <Text className={styles.emptyState}>{intl.formatMessage(ExtendedAgentsGraphResources.noAgentSelected)}</Text>
            ) : null;

        // Simple info panel for system tools
        const systemToolDetails = selectedSystemTool ? (
            <>
                <div className={styles.summary}>
                    <Text size={400} weight="semibold">
                        {selectedSystemTool.name}
                    </Text>
                    <div className={styles.badgeRow}>
                        <Badge appearance="outline" size="small">
                            {selectedSystemTool.category}
                        </Badge>
                        {selectedSystemTool.resourceType && (
                            <Badge appearance="tint" size="small">
                                {selectedSystemTool.resourceType}
                            </Badge>
                        )}
                    </div>
                </div>

                {/* Description */}
                {selectedSystemTool.description && (
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.toolDescriptionLabel)}</Text>
                        <Text className={styles.subtitle}>{selectedSystemTool.description}</Text>
                    </div>
                )}

                {/* Plugin Name */}
                <div className={styles.section}>
                    <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.systemToolPluginLabel)}</Text>
                    <Text className={styles.subtitle}>{selectedSystemTool.pluginName}</Text>
                </div>

                {/* Connects To (for SearchMemory tool) */}
                {selectedSystemTool.name?.toLowerCase() === 'searchmemory' && (
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>{intl.formatMessage(ExtendedAgentsGraphResources.connectsTo)}</Text>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '8px', marginTop: '8px' }}>
                            <Link
                                appearance="subtle"
                                onClick={() => navigate('/views/settings/dataKnowledgeSpace')}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    cursor: 'pointer',
                                }}
                            >
                                Knowledge Base
                            </Link>
                            <Link
                                appearance="subtle"
                                onClick={() => navigate('/views/settings/data-connectors')}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '6px',
                                    cursor: 'pointer',
                                }}
                            >
                                Data Connectors
                            </Link>
                        </div>
                    </div>
                )}

                {/* Parameters */}
                {selectedSystemTool.parameters && selectedSystemTool.parameters.length > 0 && (
                    <div className={styles.section}>
                        <Text className={styles.sectionTitle}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.parametersSectionTitle)}
                        </Text>
                        <div className={styles.list}>
                            {selectedSystemTool.parameters.map((param, index) => (
                                <div key={index} className={styles.listItem}>
                                    <Text>{param}</Text>
                                </div>
                            ))}
                        </div>
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
                                <Text weight="semibold">{headerTitle}</Text>
                                {headerSubtitle && (
                                    <Text size={200} className={styles.subtitle}>
                                        {headerSubtitle}
                                    </Text>
                                )}
                            </div>
                            {(playgroundTarget ||
                                (headerEditContext && headerEditContext.type !== 'connector' && headerEditContext.type !== 'trigger')) && (
                                <div style={{ display: 'flex', gap: '4px' }}>
                                    {playgroundTarget && (
                                        <Button
                                            appearance="subtle"
                                            size="small"
                                            icon={<Beaker20Regular />}
                                            onClick={handleOpenPlaygroundClick}
                                            title={intl.formatMessage(PlaygroundResources.openPlaygroundButton)}
                                            aria-label={intl.formatMessage(PlaygroundResources.openPlaygroundButton)}
                                        />
                                    )}
                                    {headerEditContext &&
                                        headerEditContext.type !== 'connector' &&
                                        headerEditContext.type !== 'trigger' && (
                                            <>
                                                <Button
                                                    appearance="subtle"
                                                    size="small"
                                                    icon={<Edit20Regular />}
                                                    onClick={() => handleOpenYamlEditor(headerEditContext.entity, headerEditContext.type)}
                                                    title={intl.formatMessage(ExtendedAgentsGraphResources.yamlOpenButton)}
                                                />
                                                {headerEditContext.type === 'agent' && isAgentContext && selectedAgent && (
                                                    <Button
                                                        appearance="subtle"
                                                        size="small"
                                                        icon={<Delete20Regular />}
                                                        onClick={() => handleDeleteClick('agent', selectedAgent)}
                                                        disabled={isDeleting}
                                                        title={intl.formatMessage(SreAgentResources.deleteAgentTitle)}
                                                    />
                                                )}
                                                {headerEditContext.type === 'tool' && selectedTool && (
                                                    <Button
                                                        appearance="subtle"
                                                        size="small"
                                                        icon={<Delete20Regular />}
                                                        onClick={() => handleDeleteClick('tool', selectedTool)}
                                                        disabled={isDeleting}
                                                        title={intl.formatMessage(SreAgentResources.deleteToolTitle)}
                                                    />
                                                )}
                                            </>
                                        )}
                                </div>
                            )}
                        </div>

                        <div className={styles.content}>
                            {selectedTool && (
                                <div className={styles.section}>
                                    <Text className={styles.sectionTitle}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.toolDetailsTitle, { name: selectedTool.name })}
                                    </Text>
                                    {renderToolDetails(selectedTool)}
                                </div>
                            )}

                            {selectedConnector && (
                                <div className={styles.section}>
                                    <Text className={styles.sectionTitle}>
                                        {intl.formatMessage(ExtendedAgentsGraphResources.connectorDetailsTitle, {
                                            name: selectedConnector.name,
                                        })}
                                    </Text>
                                    {renderConnectorDetails(selectedConnector)}
                                </div>
                            )}

                            {selectedTrigger && (
                                <div className={styles.section}>
                                    <Text className={styles.sectionTitle}>Trigger Details: {selectedTrigger.name}</Text>
                                    {renderTriggerDetails(selectedTrigger)}
                                </div>
                            )}

                            {agentDetails}
                            {systemToolDetails}
                        </div>
                    </div>
                </div>

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
                                    : intl.formatMessage(SreAgentResources.deleteAgentTitle)}
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
