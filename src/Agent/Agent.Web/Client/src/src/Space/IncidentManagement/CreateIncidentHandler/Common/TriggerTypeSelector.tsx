import { Checkbox, Field, Text, tokens } from '@fluentui/react-components';
import { Warning16Regular } from '@fluentui/react-icons';
import { FC, useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentTriggerEvent } from '../../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementResources } from '../../../../Strings/SREAgentResources';

export interface TriggerTypeSelectorProps {
    selectedTriggers: IncidentTriggerEvent[];
    onTriggersChange: (triggers: IncidentTriggerEvent[]) => void;
    owningTeamId?: string;
    disabled?: boolean;
}

interface TriggerOption {
    value: IncidentTriggerEvent;
    labelKey: keyof typeof IncidentManagementResources;
    descriptionKey: keyof typeof IncidentManagementResources;
    requiresOwningTeam?: boolean;
}

const TRIGGER_OPTIONS: TriggerOption[] = [
    {
        value: IncidentTriggerEvent.IncidentCreatedOrTransferred,
        labelKey: 'triggerIncidentCreatedOrTransferred',
        descriptionKey: 'triggerIncidentCreatedOrTransferredDescription',
    },
    {
        value: IncidentTriggerEvent.DiscussionEntry,
        labelKey: 'triggerDiscussionEntry',
        descriptionKey: 'triggerDiscussionEntryDescription',
        requiresOwningTeam: true,
    },
    {
        value: IncidentTriggerEvent.IncidentMitigated,
        labelKey: 'triggerIncidentMitigated',
        descriptionKey: 'triggerIncidentMitigatedDescription',
    },
    {
        value: IncidentTriggerEvent.IncidentReactivated,
        labelKey: 'triggerIncidentReactivated',
        descriptionKey: 'triggerIncidentReactivatedDescription',
    },
    {
        value: IncidentTriggerEvent.IncidentResolved,
        labelKey: 'triggerIncidentResolved',
        descriptionKey: 'triggerIncidentResolvedDescription',
    },
];

export const TriggerTypeSelector: FC<TriggerTypeSelectorProps> = ({
    selectedTriggers,
    onTriggersChange,
    owningTeamId,
    disabled = false,
}) => {
    const intl = useIntl();

    const handleTriggerChange = useCallback(
        (trigger: IncidentTriggerEvent, checked: boolean) => {
            if (checked) {
                onTriggersChange([...selectedTriggers, trigger]);
            } else {
                // Prevent removing the last trigger
                if (selectedTriggers.length > 1) {
                    onTriggersChange(selectedTriggers.filter(t => t !== trigger));
                }
            }
        },
        [selectedTriggers, onTriggersChange]
    );

    const isDiscussionEntryDisabled = useMemo(() => {
        return !owningTeamId;
    }, [owningTeamId]);

    const showDiscussionEntryWarning = useMemo(() => {
        return selectedTriggers.includes(IncidentTriggerEvent.DiscussionEntry) && !owningTeamId;
    }, [selectedTriggers, owningTeamId]);

    return (
        <Field label={intl.formatMessage(IncidentManagementResources.triggerEvents)}>
            <Text size={200} style={{ color: tokens.colorNeutralForeground3, marginBottom: 8 }}>
                {intl.formatMessage(IncidentManagementResources.triggerEventsDescription)}
            </Text>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 12, marginTop: 8 }}>
                {TRIGGER_OPTIONS.map(option => {
                    const isSelected = selectedTriggers.includes(option.value);
                    const isOnlySelected = selectedTriggers.length === 1 && isSelected;
                    const isOptionDisabled =
                        disabled || isOnlySelected || (option.requiresOwningTeam && isDiscussionEntryDisabled && !isSelected);

                    return (
                        <div key={option.value} style={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
                            <Checkbox
                                checked={isSelected}
                                onChange={(_, data) => handleTriggerChange(option.value, data.checked === true)}
                                disabled={isOptionDisabled}
                                label={
                                    <span style={{ fontWeight: 500 }}>
                                        {intl.formatMessage(IncidentManagementResources[option.labelKey])}
                                    </span>
                                }
                            />
                            <Text
                                size={200}
                                style={{
                                    color: tokens.colorNeutralForeground3,
                                    marginLeft: 28,
                                }}
                            >
                                {intl.formatMessage(IncidentManagementResources[option.descriptionKey])}
                            </Text>
                            {option.requiresOwningTeam && isDiscussionEntryDisabled && !isSelected && (
                                <div
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: 4,
                                        marginLeft: 28,
                                        marginTop: 4,
                                    }}
                                >
                                    <Warning16Regular style={{ color: tokens.colorPaletteYellowForeground2 }} />
                                    <Text size={200} style={{ color: tokens.colorPaletteYellowForeground2 }}>
                                        {intl.formatMessage(IncidentManagementResources.discussionEntryRequiresOwningTeam)}
                                    </Text>
                                </div>
                            )}
                        </div>
                    );
                })}
                {showDiscussionEntryWarning && (
                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: 8,
                            padding: '8px 12px',
                            backgroundColor: tokens.colorPaletteYellowBackground1,
                            borderRadius: 4,
                        }}
                    >
                        <Warning16Regular style={{ color: tokens.colorPaletteYellowForeground2 }} />
                        <Text size={200} style={{ color: tokens.colorPaletteYellowForeground2 }}>
                            {intl.formatMessage(IncidentManagementResources.discussionEntryRequiresOwningTeam)}
                        </Text>
                    </div>
                )}
            </div>
        </Field>
    );
};
