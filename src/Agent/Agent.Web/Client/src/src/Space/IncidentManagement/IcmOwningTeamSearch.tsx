import { Checkbox, Combobox, Field, Option, OptionOnSelectData } from '@fluentui/react-components';
import debounce from 'lodash/debounce';
import { FC, useContext, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { useAzPortalContext } from '../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../Common/Clients/IncidentHandlerClient';
import { IncidentTeamSearchResponse } from '../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementResources } from '../../Strings/SREAgentResources';

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

    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const { log } = useAzPortalContext();

    const incidentHandlerClient = useMemo(() => IncidentHandlerClient.getInstance(sreAgentEndpoint, log), [sreAgentEndpoint, log]);

    const [owningTeamOptions, setOwningTeamOptions] = useState<IncidentTeamSearchResponse[]>([]);

    const [searchValue, setSearchValue] = useState<string>(defaultTeamId ? `${defaultTeamId}` : '');

    const [icmTeamSearchOptions, setIcmTeamSearchOptions] = useState<{
        owningTeamAssignableOnly: boolean;
        owningTeamWithOnCallRotationsOnly: boolean;
        searchTerm: string;
    }>({
        owningTeamAssignableOnly: true,
        owningTeamWithOnCallRotationsOnly: true,
        searchTerm: '',
    });

    useEffect(() => {
        // Load default team if defaultTeamId is provided
        const loadDefaultTeam = async () => {
            if (defaultTeamId) {
                const res = await incidentHandlerClient.getIcmTeamById(defaultTeamId);
                if (res.isSuccessful && res.content) {
                    setOwningTeamOptions([res.content]);
                    setSearchValue(getDisplayName(res.content));
                }
            }
        };
        loadDefaultTeam();
    }, [defaultTeamId, incidentHandlerClient]);

    const debouncedSearch = useMemo(
        () =>
            debounce(async () => {
                const res = await incidentHandlerClient.searchIncidentTeams(
                    icmTeamSearchOptions.searchTerm,
                    icmTeamSearchOptions.owningTeamAssignableOnly,
                    icmTeamSearchOptions.owningTeamWithOnCallRotationsOnly
                );
                if (res.isSuccessful && res.content) {
                    setOwningTeamOptions(res.content);
                }
            }, 500),
        [icmTeamSearchOptions, incidentHandlerClient]
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
        setSearchValue(selectedTeam ? getDisplayName(selectedTeam) : '');
    };

    const getDisplayName = (team: IncidentTeamSearchResponse) => {
        return team.tenant ? `${team.tenant.name} / ${team.name}` : team.name;
    };

    const onSearchChange = (ev: React.ChangeEvent<HTMLInputElement>) => {
        setIcmTeamSearchOptions(prev => ({
            ...prev,
            searchTerm: ev.target.value,
        }));
        setSearchValue(ev.target.value);
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
                onChange={ev => onSearchChange(ev)}
                style={comboboxStyles}
                className={comboboxClassName}
                onOptionSelect={(_, data) => {
                    onOptionSelect(data);
                }}
                placeholder={intl.formatMessage(IncidentManagementResources.owningIcmTeamPlaceholder)}
                onBlur={() => onFieldTouched()}
                value={searchValue}
                disabled={disabled}
            >
                {owningTeamOptions.map(team => (
                    <Option key={team.id} value={`${team.id}`}>
                        {getDisplayName(team)}
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
