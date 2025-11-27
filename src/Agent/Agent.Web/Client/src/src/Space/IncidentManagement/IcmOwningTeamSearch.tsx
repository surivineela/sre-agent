import { Checkbox, Combobox, Field, Option, OptionOnSelectData } from '@fluentui/react-components';
import debounce from 'lodash/debounce';
import { FC, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentTeamSearchResponse } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';
import { useIncidentFilterFields } from '../Hooks/useIncidentFilterFields';

export const IcmOwningTeamSearch: FC<IcmOwningTeamSearchProps> = ({
    defaultTeamId,
    onFieldTouched,
    onUpdateOwningTeam,
    orientation,
    fieldStyles,
    comboboxStyles,
    fieldClassName,
    comboboxClassName,
    disabled,
}) => {
    const intl = useIntl();
    const { searchIncidentTeams } = useIncidentFilterFields();

    const [owningTeamOptions, setOwningTeamOptions] = useState<IncidentTeamSearchResponse[]>(
        defaultTeamId ? [{ id: parseInt(defaultTeamId), name: defaultTeamId, description: '', teamPublicId: '' }] : []
    );
    const [icmTeamSearchOptions, setIcmTeamSearchOptions] = useState<{
        owningTeamAssignableOnly: boolean;
        owningTeamWithOnCallRotationsOnly: boolean;
        searchTerm: string;
    }>({
        owningTeamAssignableOnly: true,
        owningTeamWithOnCallRotationsOnly: true,
        searchTerm: '',
    });

    const debouncedSearch = useMemo(
        () =>
            debounce(async () => {
                const teams = await searchIncidentTeams(
                    icmTeamSearchOptions.searchTerm,
                    icmTeamSearchOptions.owningTeamAssignableOnly,
                    icmTeamSearchOptions.owningTeamWithOnCallRotationsOnly
                );
                setOwningTeamOptions(teams);
            }, 500),
        [icmTeamSearchOptions, searchIncidentTeams]
    );

    useEffect(() => {
        if (icmTeamSearchOptions.searchTerm.trim()) {
            debouncedSearch();
        }
        return () => debouncedSearch.cancel();
    }, [icmTeamSearchOptions.searchTerm, debouncedSearch]);

    const onOptionSelect = (data: OptionOnSelectData) => {
        const selectedTeam = owningTeamOptions.find(team => `${team.id}` === data.optionValue);
        if (selectedTeam) {
            onUpdateOwningTeam(selectedTeam);
        }
    };

    return (
        <Field
            label={intl.formatMessage(IncidentManagementResources.owningTeam)}
            required
            orientation={orientation}
            style={fieldStyles}
            className={fieldClassName}
        >
            <Combobox
                onChange={ev =>
                    setIcmTeamSearchOptions(prev => ({
                        ...prev,
                        searchTerm: ev.target.value,
                    }))
                }
                style={comboboxStyles}
                className={comboboxClassName}
                onOptionSelect={(_, data) => {
                    onOptionSelect(data);
                }}
                placeholder={intl.formatMessage(IncidentManagementResources.owningIcmTeamPlaceholder)}
                onBlur={() => onFieldTouched()}
                defaultValue={defaultTeamId}
                disabled={disabled}
            >
                {owningTeamOptions.map(team => (
                    <Option key={team.id} value={`${team.id}`}>
                        {team.tenant ? `${team.tenant.name} / ${team.name}` : team.name}
                    </Option>
                ))}
            </Combobox>

            <Checkbox
                label={intl.formatMessage(IncidentManagementResources.incidentTeamSearchAssignableOnly)}
                checked={icmTeamSearchOptions.owningTeamAssignableOnly}
                onChange={(_, data) =>
                    setIcmTeamSearchOptions(prev => ({
                        ...prev,
                        owningTeamAssignableOnly: data.checked === true,
                    }))
                }
                required={false}
            />
            <Checkbox
                label={intl.formatMessage(IncidentManagementResources.incidentTeamSearchWithOncallRotation)}
                checked={icmTeamSearchOptions.owningTeamWithOnCallRotationsOnly}
                onChange={(_, data) =>
                    setIcmTeamSearchOptions(prev => ({
                        ...prev,
                        owningTeamWithOnCallRotationsOnly: data.checked === true,
                    }))
                }
                required={false}
            />
        </Field>
    );
};

interface IcmOwningTeamSearchProps {
    defaultTeamId?: string;
    onFieldTouched: () => void;
    onUpdateOwningTeam: (team: IncidentTeamSearchResponse) => void;
    orientation?: 'horizontal' | 'vertical' | undefined;
    fieldStyles?: React.CSSProperties;
    fieldClassName?: string;
    comboboxStyles?: React.CSSProperties;
    comboboxClassName?: string;
    disabled?: boolean;
}
