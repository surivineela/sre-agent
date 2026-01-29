import { Dropdown, Field, Option, Tag, TagGroup, Text, tokens } from '@fluentui/react-components';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources } from '../../../../Strings/SREAgentResources';

export interface MultiAgentSelectorProps {
    /** Currently selected agent IDs */
    selectedAgents: string[];
    /** Callback when selection changes */
    onAgentsChange: (agents: string[]) => void;
    /** List of available agent names/IDs */
    availableAgents: string[];
    /** Whether the selector is disabled */
    disabled?: boolean;
    /** Whether field is required */
    required?: boolean;
}

/**
 * Multi-select dropdown component for selecting one or more handling agents.
 * Phase 2 feature for parallel multi-agent incident processing.
 */
export const MultiAgentSelector: FC<MultiAgentSelectorProps> = ({
    selectedAgents,
    onAgentsChange,
    availableAgents,
    disabled = false,
    required = false,
}) => {
    const intl = useIntl();

    const handleOptionSelect = useCallback(
        (_: unknown, data: { selectedOptions: string[] }) => {
            onAgentsChange(data.selectedOptions);
        },
        [onAgentsChange]
    );

    const handleRemoveAgent = useCallback(
        (agentToRemove: string) => {
            // Prevent removing the last agent
            if (selectedAgents.length > 1) {
                onAgentsChange(selectedAgents.filter(a => a !== agentToRemove));
            }
        },
        [selectedAgents, onAgentsChange]
    );

    const displayValue = useMemo(() => {
        if (selectedAgents.length === 0) {
            return '';
        }
        if (selectedAgents.length === 1) {
            return selectedAgents[0];
        }
        return `${selectedAgents.length} agents selected`;
    }, [selectedAgents]);

    const showMinAgentWarning = selectedAgents.length === 0 && required;

    return (
        <Field
            label={intl.formatMessage(IncidentManagementResources.multiAgentLabel)}
            required={required}
            validationMessage={showMinAgentWarning ? intl.formatMessage(IncidentManagementResources.minAgentWarning) : undefined}
            validationState={showMinAgentWarning ? 'error' : undefined}
        >
            <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginBottom: 8 }}>
                {intl.formatMessage(IncidentManagementResources.multiAgentDescription)}
            </Text>

            <Dropdown
                multiselect
                selectedOptions={selectedAgents}
                value={displayValue}
                onOptionSelect={handleOptionSelect}
                placeholder={intl.formatMessage(IncidentManagementResources.multiAgentPlaceholder)}
                disabled={disabled}
                style={{ minWidth: 250 }}
            >
                {availableAgents.map(agent => (
                    <Option key={agent} value={agent}>
                        {agent}
                    </Option>
                ))}
            </Dropdown>

            {selectedAgents.length > 0 && (
                <div style={{ marginTop: 8 }}>
                    <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginBottom: 4, display: 'block' }}>
                        {intl.formatMessage(IncidentManagementResources.selectedAgents)}
                    </Text>
                    <TagGroup
                        onDismiss={(_e, data) => handleRemoveAgent(data.value)}
                        aria-label={intl.formatMessage(IncidentManagementResources.selectedAgentsAriaLabel)}
                    >
                        {selectedAgents.map(agent => (
                            <Tag
                                key={agent}
                                value={agent}
                                dismissible={selectedAgents.length > 1 && !disabled}
                                dismissIcon={<Dismiss12Regular />}
                                appearance="brand"
                            >
                                {agent}
                            </Tag>
                        ))}
                    </TagGroup>
                </div>
            )}
        </Field>
    );
};
