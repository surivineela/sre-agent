import { tokens } from '@fluentui/react-components';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { FilterProps } from '../../../Common/Components/PillFilter/Contracts';
import { PillFilter } from '../../../Common/Components/PillFilter/PillFilter';
import { ExtendedAgentsGraphResources } from '../../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedTool, PlaygroundEntity, SystemTool } from '../../Contracts/ExtendedAgentGraph';
import { EntityIcon, EntityIconType } from '../EntityIcon';
import { McpConnection } from '../ExtendedAgentCreationDialog/api/mcpConnectionsApi';

const iconShorthandStyle = { wrapperSize: 20, iconSize: 16, borderRadius: 6 };

type PlaygroundEntitySelectorProps = {
    agents: ExtendedAgent[];
    systemTools: SystemTool[];
    extendedTools: ExtendedTool[];
    mcpConnections: McpConnection[];
    selectedEntity?: PlaygroundEntity;
    onEntitySelect: (playgroundEntity?: PlaygroundEntity) => void;
    isLoading: boolean;
    showAgentPicker?: boolean;
};

export const PlaygroundEntitySelector = memo(
    ({ agents, systemTools, extendedTools, mcpConnections, selectedEntity, onEntitySelect, isLoading }: PlaygroundEntitySelectorProps) => {
        const intl = useIntl();

        const agentOptions: EntityOption[] = useMemo(() => {
            return agents
                .map(agent => {
                    const agentTypeText = agent.agentType
                        ? intl.formatMessage(
                              agent.agentType === 'Autonomous'
                                  ? ExtendedAgentsGraphResources.autonomous
                                  : agent.agentType === 'Orchestrator'
                                    ? ExtendedAgentsGraphResources.orchestrator
                                    : ExtendedAgentsGraphResources.activity
                          )
                        : undefined;

                    return {
                        key: getEntityOptionKey({ entityType: 'Agent', entity: agent }),
                        name: agent.name,
                        label: agent.name,
                        icon: <EntityIcon type={agent.name === 'meta_agent' ? 'metaAgent' : 'agent'} shorthandStyle={iconShorthandStyle} />,
                        sublabel: agentTypeText,
                        type: agent.agentType,
                        entityType: 'Agent' as const,
                        entity: agent,
                    };
                })
                .sort((a, b) => (a.name === 'meta_agent' ? -1 : a.label.localeCompare(b.label)));
        }, [agents, intl]);

        const systemToolOptions: EntityOption[] = useMemo(
            () =>
                (systemTools || [])
                    .map(systemTool => {
                        return {
                            key: getEntityOptionKey({ entityType: 'SystemTool', entity: systemTool }),
                            name: systemTool.name,
                            label: systemTool.name,
                            icon: <EntityIcon type="tool" shorthandStyle={iconShorthandStyle} />,
                            sublabel: intl.formatMessage(ExtendedAgentsGraphResources.builtInTool),
                            entityType: 'SystemTool' as const,
                            entity: systemTool,
                        };
                    })
                    .sort((a, b) => a.label.localeCompare(b.label)),
            [systemTools, intl]
        );

        const extendedToolOptions: EntityOption[] = useMemo(
            () =>
                (extendedTools || [])
                    .map(extendedTool => {
                        const sublabel =
                            extendedTool.type === 'KustoTool'
                                ? intl.formatMessage(ExtendedAgentsGraphResources.kustoToolCreateMenuLabel)
                                : extendedTool.type === 'PythonFunctionTool'
                                  ? intl.formatMessage(ExtendedAgentsGraphResources.pythonToolCreateMenuLabel)
                                  : intl.formatMessage(ExtendedAgentsGraphResources.customTool);
                        const entityType: EntityIconType = extendedTool.type === 'PythonFunctionTool' ? 'pythonTool' : 'toolWithGear';
                        return {
                            key: getEntityOptionKey({ entityType: 'ExtendedTool', entity: extendedTool }),
                            name: extendedTool.name,
                            label: extendedTool.name,
                            icon: <EntityIcon type={entityType} shorthandStyle={iconShorthandStyle} />,
                            sublabel,
                            type: extendedTool.type,
                            entityType: 'ExtendedTool' as const,
                            entity: extendedTool,
                        };
                    })
                    .sort((a, b) => {
                        const typeCompare = a.type.localeCompare(b.type);
                        if (typeCompare !== 0) {
                            return typeCompare;
                        }
                        return a.label.localeCompare(b.label);
                    })
                    // Filtering out PythonFunctionTool for now
                    .filter(tool => tool.type === 'KustoTool'),
            [extendedTools, intl]
        );

        const mcpToolOptions: EntityOption[] = useMemo(() => {
            const mcpTools: EntityOption[] = [];
            mcpConnections?.forEach(connection => {
                connection.tools?.forEach(tool => {
                    mcpTools.push({
                        key: getEntityOptionKey({ entityType: 'McpTool', entity: tool }),
                        name: tool.name,
                        label: tool.name,
                        icon: <EntityIcon type="windowWrenchRegular" shorthandStyle={iconShorthandStyle} />,
                        sublabel: intl.formatMessage(ExtendedAgentsGraphResources.mcpTool),
                        entityType: 'McpTool' as const,
                        entity: tool,
                    });
                });
            });
            return mcpTools.sort((a, b) => a.label.localeCompare(b.label));
        }, [mcpConnections, intl]);

        const entityFilterProps: FilterProps = useMemo(() => {
            const metaAgentOption = agentOptions.find(option => option.key === 'meta_agent');
            const subagentOptions = agentOptions.filter(option => option.key !== 'meta_agent');
            const allAgentOptions = metaAgentOption ? [metaAgentOption, ...subagentOptions] : subagentOptions;

            // hiding MCP tools for now
            // const combinedOptions = [...allAgentOptions, ...systemToolOptions, ...extendedToolOptions, ...mcpToolOptions];
            const combinedOptions = [...allAgentOptions, ...systemToolOptions, ...extendedToolOptions];

            return {
                label: getPillFilterLabel(selectedEntity, intl),
                disabled: isLoading,
                labelDelimiter: ':',
                filterType: 'combobox' as const,
                showValueAs: 'list',
                options: combinedOptions,
                onApply: (keys: string[]) => {
                    if (keys.length === 0) {
                        onEntitySelect(undefined);
                        return;
                    }
                    const selectedOption = combinedOptions.find(option => option.key === keys[0]);
                    if (selectedOption) {
                        onEntitySelect(selectedOption);
                    } else {
                        onEntitySelect(undefined);
                    }
                },
                selectedKeys: selectedEntity ? [getEntityOptionKey(selectedEntity)] : [],
                multiSelect: false,
                addAllOption: false,
            };
        }, [isLoading, agentOptions, systemToolOptions, extendedToolOptions, mcpToolOptions, intl, onEntitySelect, selectedEntity]);

        return (
            <div>
                <div
                    style={{
                        pointerEvents: 'auto',
                        paddingTop: '12px',
                        paddingBottom: '12px',
                        paddingLeft: '20px',
                        display: 'flex',
                        gap: '12px',
                        alignItems: 'flex-start',
                        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
                    }}
                >
                    <div style={{ zIndex: 10 }}>
                        <PillFilter {...entityFilterProps} blockOnDirtyContext={true} />
                    </div>
                </div>
            </div>
        );
    }
);

PlaygroundEntitySelector.displayName = 'PlaygroundEntitySelector';

type EntityOption = PlaygroundEntity & {
    key: string;
    name: string;
    label: string;
    icon: JSX.Element;
    sublabel?: string;
    type?: string;
};

const getEntityOptionKey = (playgroundEntity: PlaygroundEntity) => {
    return `${playgroundEntity.entityType}_${playgroundEntity.entity.name}`;
};

const getPillFilterLabel = (entityOption: PlaygroundEntity | undefined, intl: ReturnType<typeof useIntl>) => {
    if (!entityOption) {
        return intl.formatMessage(ExtendedAgentsGraphResources.subagentOrTool);
    }

    if (entityOption.entityType === 'Agent') {
        return intl.formatMessage(ExtendedAgentsGraphResources.subagent);
    }

    if (entityOption.entityType === 'SystemTool') {
        return intl.formatMessage(ExtendedAgentsGraphResources.builtInTool);
    }

    if (entityOption.entityType === 'ExtendedTool') {
        if (entityOption.entity.type === 'KustoTool') {
            return intl.formatMessage(ExtendedAgentsGraphResources.kustoToolCreateMenuLabel);
        }
        if (entityOption.entity.type === 'PythonFunctionTool') {
            return intl.formatMessage(ExtendedAgentsGraphResources.pythonToolCreateMenuLabel);
        }
        return intl.formatMessage(ExtendedAgentsGraphResources.customTool);
    }

    if (entityOption.entityType === 'McpTool') {
        return intl.formatMessage(ExtendedAgentsGraphResources.mcpTool);
    }
    return '';
};
