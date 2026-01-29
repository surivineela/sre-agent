import { MessageBar, MessageBarBody, MessageBarTitle, Text } from '@fluentui/react-components';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources } from '../../../../Strings/SREAgentResources';

export interface FilterConflictInfo {
    filterName: string;
    filterId: string;
    overlappingTriggers: string[];
}

export interface FilterConflictWarningProps {
    conflicts: FilterConflictInfo[];
}

export const FilterConflictWarning: FC<FilterConflictWarningProps> = ({ conflicts }) => {
    const intl = useIntl();

    if (conflicts.length === 0) {
        return null;
    }

    return (
        <MessageBar intent="warning" style={{ marginTop: 8, marginBottom: 8 }}>
            <MessageBarBody>
                <MessageBarTitle>{intl.formatMessage(IncidentManagementResources.filterConflictWarningTitle)}</MessageBarTitle>
                <Text size={200}>{intl.formatMessage(IncidentManagementResources.filterConflictWarningDescription)}</Text>
                <div style={{ marginTop: 8 }}>
                    <Text size={200} weight="semibold">
                        {intl.formatMessage(IncidentManagementResources.conflictingFilters)}
                    </Text>
                    <ul style={{ margin: '4px 0', paddingLeft: 20 }}>
                        {conflicts.map(conflict => (
                            <li key={conflict.filterId}>
                                <Text size={200}>
                                    {conflict.filterName} ({conflict.overlappingTriggers.join(', ')})
                                </Text>
                            </li>
                        ))}
                    </ul>
                </div>
            </MessageBarBody>
        </MessageBar>
    );
};
