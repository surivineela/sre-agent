import { Button, Combobox, Option, Spinner, Text } from '@fluentui/react-components';
import { AgentsRegular, ArrowClockwise20Regular } from '@fluentui/react-icons';
import { Node, useReactFlow } from '@xyflow/react';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { FilterProps } from '../../Common/Components/PillFilter/Contracts';
import { PillFilter } from '../../Common/Components/PillFilter/PillFilter';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent, ExtendedAgentGraphNode } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentSelectorStyles } from '../Styles/ExtendedAgentGraph.styles';
import { getNodesMatchingSearchQuery } from './ExtendedAgentGraphUtility';

type ExtendedAgentSelectorProps = {
    agents: ExtendedAgent[];
    selectedAgentName?: string;
    searchQuery: string;
    onAgentSelect: (agentName?: string) => void;
    onSearchQueryChange: (query: string) => void;
    onRefresh: () => void;
    setSelectedNode: React.Dispatch<React.SetStateAction<ExtendedAgentGraphNode | undefined>>;
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
        selectedAgentName,
        searchQuery,
        onAgentSelect,
        onSearchQueryChange,
        onRefresh,
        setSelectedNode,
        isLoading,
        nodes,
        showAgentPicker = true,
        noAgentsMessage,
    }: ExtendedAgentSelectorProps) => {
        const styles = useExtendedAgentSelectorStyles();
        const intl = useIntl();
        const { fitView } = useReactFlow();

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
                        icon: <AgentsRegular />,
                        sublabel: agentTypeText,
                        type: agent.agentType,
                    };
                }),
            [agents, intl]
        );

        const filteredNodes = useMemo(() => {
            const matchingNodes = getNodesMatchingSearchQuery(nodes, searchQuery);
            return matchingNodes;
        }, [nodes, searchQuery]);

        const shouldRenderAgentCombobox = useMemo(() => showAgentPicker && agents.length > 0, [showAgentPicker, agents.length]);

        const incidentTypeFilterProps: FilterProps = useMemo(
            () => ({
                label: intl.formatMessage(ExtendedAgentsGraphResources.subagent),
                disabled: isLoading,
                labelDelimiter: ':',
                filterType: 'combobox' as const,
                showValueAs: 'list',
                options: agentOptions,
                onApply: (keys: string[]) => onAgentSelect(keys.length > 0 ? keys[0] : undefined),
                selectedKeys: selectedAgentName ? [selectedAgentName] : [],
                multiSelect: false,
                addAllOption: false,
            }),
            [isLoading, agentOptions, intl, onAgentSelect, selectedAgentName]
        );

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
                            <PillFilter {...incidentTypeFilterProps} />
                        </div>
                    )}
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '4px', alignItems: 'flex-start' }}>
                        <Combobox
                            id="nodeSearchComboBox"
                            value={searchQuery}
                            placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchPlaceholder)}
                            onOptionSelect={(_event, data) => {
                                const targetNode = nodes.find(node => node.id === data.optionValue);
                                if (targetNode) {
                                    setSelectedNode(targetNode.data);
                                    requestAnimationFrame(() => {
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
                                onSearchQueryChange(inputValue);
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
                    <div className={styles.actionColumn}>
                        <Button appearance="secondary" icon={<ArrowClockwise20Regular />} onClick={onRefresh} disabled={isLoading}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.refreshGraphButton)}
                        </Button>
                    </div>
                </div>
            </div>
        );
    }
);

ExtendedAgentSelector.displayName = 'ExtendedAgentSelector';
