import {
    Badge,
    Button,
    Combobox,
    Field,
    InputOnChangeData,
    Option,
    SearchBox,
    SearchBoxChangeEvent,
    Text,
} from '@fluentui/react-components';
import { ArrowClockwise20Regular } from '@fluentui/react-icons';
import { memo, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../Strings/SREAgentResources';
import { ExtendedAgent } from '../Contracts/ExtendedAgentGraph';
import { useExtendedAgentSelectorStyles } from '../Styles/ExtendedAgentGraph.styles';

type ExtendedAgentSelectorProps = {
    agents: ExtendedAgent[];
    selectedAgentName?: string;
    selectedAgent?: ExtendedAgent;
    searchQuery: string;
    onAgentSelect: (agentName?: string) => void;
    onSearchQueryChange: (query: string) => void;
    onRefresh: () => void;
    isLoading: boolean;
    nodeCount: number;
    edgeCount: number;
    showAgentPicker?: boolean;
    noAgentsMessage?: string;
};

export const ExtendedAgentSelector = memo(
    ({
        agents,
        selectedAgentName,
        selectedAgent,
        searchQuery,
        onAgentSelect,
        onSearchQueryChange,
        onRefresh,
        isLoading,
        nodeCount,
        edgeCount,
        showAgentPicker = true,
        noAgentsMessage,
    }: ExtendedAgentSelectorProps) => {
        const styles = useExtendedAgentSelectorStyles();
        const intl = useIntl();

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
                        description: agentTypeText,
                        type: agent.agentType,
                    };
                }),
            [agents, intl]
        );

        const toolCount = selectedAgent?.tools?.length ?? 0;
        const handoffCount = selectedAgent?.handoffs?.length ?? 0;
        const agentAsToolCount = selectedAgent?.agentsAsTools?.length ?? 0;
        const shouldRenderAgentCombobox = showAgentPicker && agents.length > 0;

        return (
            <div className={styles.overlayCard}>
                <div className={styles.root}>
                    <div className={styles.inputsRow}>
                        {shouldRenderAgentCombobox && (
                            <Field label={intl.formatMessage(ExtendedAgentsGraphResources.agentSelectorLabel)} className={styles.field}>
                                <Combobox
                                    value={selectedAgentName ?? ''}
                                    placeholder={intl.formatMessage(ExtendedAgentsGraphResources.agentSelectorPlaceholder)}
                                    selectedOptions={selectedAgentName ? [selectedAgentName] : []}
                                    onOptionSelect={(_, data) => onAgentSelect(data.optionValue ?? undefined)}
                                    disabled={isLoading}
                                    appearance="outline"
                                >
                                    {agentOptions.map(option => (
                                        <Option
                                            key={option.key}
                                            value={option.key}
                                            text={option.label}
                                            checkIcon={null}
                                            className={styles.option}
                                        >
                                            <span className={styles.optionText}>{option.label}</span>
                                            {option.description && <span className={styles.optionSubtext}>{option.description}</span>}
                                        </Option>
                                    ))}
                                </Combobox>
                            </Field>
                        )}

                        <Field label={intl.formatMessage(ExtendedAgentsGraphResources.searchLabel)} className={styles.field}>
                            <SearchBox
                                value={searchQuery}
                                placeholder={intl.formatMessage(ExtendedAgentsGraphResources.searchPlaceholder)}
                                onChange={(_event: SearchBoxChangeEvent, data: InputOnChangeData) => onSearchQueryChange(data.value ?? '')}
                                disabled={isLoading}
                                className={styles.searchBox}
                            />
                        </Field>

                        <div className={styles.actionColumn}>
                            <Button appearance="secondary" icon={<ArrowClockwise20Regular />} onClick={onRefresh} disabled={isLoading}>
                                {intl.formatMessage(ExtendedAgentsGraphResources.refreshGraphButton)}
                            </Button>
                        </div>
                    </div>

                    {!shouldRenderAgentCombobox && noAgentsMessage && (
                        <Text size={200} className={styles.emptyNotice} role="status">
                            {noAgentsMessage}
                        </Text>
                    )}

                    <div className={styles.statsRow}>
                        <Text size={200} className={styles.statsItem}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.graphNodeCount, { count: nodeCount })}
                        </Text>
                        <Text size={200} className={styles.statsItem}>
                            {intl.formatMessage(ExtendedAgentsGraphResources.graphEdgeCount, { count: edgeCount })}
                        </Text>
                        {selectedAgent && (
                            <div className={styles.badgeGroup}>
                                <Badge appearance="tint" size="small">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.toolsCountBadge, { count: toolCount })}
                                </Badge>
                                <Badge appearance="tint" size="small">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.handoffCountBadge, { count: handoffCount })}
                                </Badge>
                                <Badge appearance="tint" size="small">
                                    {intl.formatMessage(ExtendedAgentsGraphResources.agentAsToolCountBadge, { count: agentAsToolCount })}
                                </Badge>
                            </div>
                        )}
                    </div>
                </div>
            </div>
        );
    }
);

ExtendedAgentSelector.displayName = 'ExtendedAgentSelector';
