import { Combobox, Option, Spinner, Text } from '@fluentui/react-components';
import { Node, useReactFlow } from '@xyflow/react';
import { memo, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { FilterProps } from '../../Common/Components/PillFilter/Contracts';
import { PillFilter } from '../../Common/Components/PillFilter/PillFilter';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedAgentAnchorEntity, ExtendedAgentGraphNode, ExtendedTrigger } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentSelectorStyles } from '../Styles/ExtendedAgentGraph.styles';
import { EntityIcon } from './EntityIcon';
import { getNodesMatchingSearchQuery } from './ExtendedAgentGraphUtility';

const iconShorthandStyle = { wrapperSize: 20, iconSize: 16, borderRadius: 6 };

type ExtendedAgentSelectorProps = {
    agents: ExtendedAgent[];
    triggers: ExtendedTrigger[];
    selectedEntity?: ExtendedAgentAnchorEntity;
    onEntitySelect: (anchorEntity?: ExtendedAgentAnchorEntity) => void;
    expandInfoPanel: () => void;
    setSelectedNodeId: React.Dispatch<React.SetStateAction<string | undefined>>;
    isLoading: boolean;
    nodes: Node<ExtendedAgentGraphNode>[];
    nodeCount: number;
    edgeCount: number;
    showAgentPicker?: boolean;
    noAgentsMessage?: string;
};

export const ExtendedAgentSelector = memo(
    ({
        agents,
        triggers,
        selectedEntity,
        onEntitySelect,
        expandInfoPanel,
        setSelectedNodeId,
        isLoading,
        nodes,
        showAgentPicker = true,
        noAgentsMessage,
    }: ExtendedAgentSelectorProps) => {
        const styles = useExtendedAgentSelectorStyles();
        const intl = useIntl();
        const { fitView } = useReactFlow();
        const [searchQuery, setSearchQuery] = useState('');

        const agentOptions = useMemo(
            () =>
                agents.map(agent => {
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
                        key: agent.name,
                        label: agent.name,
                        icon: <EntityIcon type={agent.name === 'meta_agent' ? 'metaAgent' : 'agent'} shorthandStyle={iconShorthandStyle} />,
                        sublabel: agentTypeText,
                        type: agent.agentType,
                        entityType: 'Agent' as const,
                    };
                }),
            [agents, intl]
        );

        const incidentTriggerOptions = useMemo(
            () =>
                triggers
                    .filter(trigger => trigger.type === 'incident')
                    .map(trigger => {
                        return {
                            key: trigger.name,
                            label: trigger.name,
                            icon: <EntityIcon type="incidentTrigger" shorthandStyle={iconShorthandStyle} />,
                            sublabel: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeIncident),
                            type: trigger.type,
                            entityType: 'Trigger' as const,
                        };
                    }),
            [triggers, intl]
        );

        const scheduledTriggerOptions = useMemo(
            () =>
                triggers
                    .filter(trigger => trigger.type === 'scheduled')
                    .map(trigger => {
                        return {
                            key: trigger.name,
                            label: trigger.name,
                            icon: <EntityIcon type="scheduledTask" shorthandStyle={iconShorthandStyle} />,
                            sublabel: intl.formatMessage(ExtendedAgentsGraphResources.triggerBadgeScheduled),
                            type: trigger.type,
                            entityType: 'Trigger' as const,
                        };
                    }),
            [triggers, intl]
        );

        const filteredNodes = useMemo(() => {
            const matchingNodes = getNodesMatchingSearchQuery(nodes, searchQuery);
            return matchingNodes;
        }, [nodes, searchQuery]);

        const shouldRenderAgentCombobox = useMemo(() => showAgentPicker && agents.length > 0, [showAgentPicker, agents.length]);

        const entityFilterProps: FilterProps = useMemo(() => {
            const metaAgentOption = agentOptions.find(option => option.key === 'meta_agent');
            const subagentOptions = agentOptions.filter(option => option.key !== 'meta_agent');
            const allAgentOptions = metaAgentOption ? [metaAgentOption, ...subagentOptions] : subagentOptions;
            return {
                label: intl.formatMessage(
                    selectedEntity?.entityType === 'Agent' ? ExtendedAgentsGraphResources.subagent : ExtendedAgentsGraphResources.trigger
                ),
                disabled: isLoading,
                labelDelimiter: ':',
                filterType: 'combobox' as const,
                showValueAs: 'list',
                options: [...allAgentOptions, ...incidentTriggerOptions, ...scheduledTriggerOptions],
                onApply: (keys: string[]) => {
                    if (keys.length === 0) {
                        onEntitySelect(undefined);
                        return;
                    }
                    const selectedOption = [...allAgentOptions, ...incidentTriggerOptions, ...scheduledTriggerOptions].find(
                        option => option.key === keys[0]
                    );
                    if (selectedOption) {
                        onEntitySelect({ entityType: selectedOption.entityType, entityName: selectedOption.key });
                    } else {
                        onEntitySelect(undefined);
                    }
                },
                selectedKeys: selectedEntity ? [selectedEntity.entityName] : [],
                multiSelect: false,
                addAllOption: false,
            };
        }, [isLoading, agentOptions, incidentTriggerOptions, scheduledTriggerOptions, intl, onEntitySelect, selectedEntity]);

        return (
            <div>
                <div
                    style={{
                        pointerEvents: 'auto',
                        marginTop: '12px',
                        marginLeft: '20px',
                        display: 'flex',
                        gap: '12px',
                        alignItems: 'flex-start',
                    }}
                >
                    {shouldRenderAgentCombobox && (
                        <div style={{ zIndex: 10 }}>
                            <PillFilter {...entityFilterProps} />
                        </div>
                    )}
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', alignItems: 'flex-start' }}>
                        <Combobox
                            id="nodeSearchComboBox"
                            value={searchQuery}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchPlaceholder)}
                            onOptionSelect={(_event, data) => {
                                setSearchQuery('');
                                const targetNode = nodes.find(node => node.id === data.optionValue);
                                if (targetNode) {
                                    setSelectedNodeId(targetNode.id);
                                    requestAnimationFrame(() => {
                                        expandInfoPanel();
                                        fitView({
                                            nodes: [{ id: targetNode.id }],
                                            duration: 600,
                                            padding: 0.1,
                                        });
                                    });
                                }
                            }}
                            disabled={isLoading || !shouldRenderAgentCombobox}
                            className={styles.searchBox}
                            onInput={event => {
                                const inputValue = (event.target as any).value as string;
                                setSearchQuery(inputValue);
                            }}
                            positioning={{
                                position: 'below',
                                align: 'start',
                            }}
                            listbox={{ style: { borderRadius: '16px' } }}
                        >
                            {isLoading ? (
                                <Spinner size="small" />
                            ) : !filteredNodes?.length ? (
                                <div style={{ margin: '2px 0px', paddingLeft: '10px' }}>
                                    {intl.formatMessage(ExtendedAgentsGraphResources.noNodesFound)}
                                </div>
                            ) : (
                                <div
                                    style={{
                                        maxHeight: '400px',
                                        overflowY: 'auto',
                                        overflowX: 'auto',
                                        borderRadius: '16px',
                                        padding: '8px 0',
                                    }}
                                >
                                    {filteredNodes?.map(node => (
                                        <Option key={node.id} value={node.id} text={node.data.name} checkIcon={null} style={{ margin: 2 }}>
                                            <span style={{ overflow: 'hidden', textOverflow: 'ellipsis' }}>{node.data.name}</span>
                                        </Option>
                                    ))}
                                </div>
                            )}
                        </Combobox>
                        {!shouldRenderAgentCombobox && noAgentsMessage && (
                            <Text size={200} className={styles.emptyNotice} role="status">
                                {noAgentsMessage}
                            </Text>
                        )}
                    </div>
                </div>
            </div>
        );
    }
);

ExtendedAgentSelector.displayName = 'ExtendedAgentSelector';
