import { useCallback, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentManagementResources } from '../../../../Strings/SREAgentResources';
import { ExtendedAgent } from '../../../Contracts/ExtendedAgentGraph';
import { PillSetItem } from '../../Common/PillSet';

export interface UseHandoffAgentsReturn {
    handoffAgentOptions: string[];
    onSelectedAgentChange: (agentName: string, isSelected: boolean) => void;
    selectedAgentNames: string[];
    setSelectedAgentNames: (names: string[]) => void;
    dropdownDisplay: string;
    pillItems: PillSetItem[];
    clear: () => void;
}

export const useHandoffAgents = (
    selectedAgentNames: string[],
    setSelectedAgentNames: (names: string[]) => void,
    agents: ExtendedAgent[] | undefined,
    excludedAgentName: string | undefined,
    additionalHandoffAgents: string[] | undefined
): UseHandoffAgentsReturn => {
    const intl = useIntl();

    const onSelectedAgentChange = useCallback(
        (agentName: string, isSelected: boolean) => {
            if (isSelected) {
                setSelectedAgentNames([...selectedAgentNames, agentName]);
            } else {
                setSelectedAgentNames(selectedAgentNames.filter(name => name !== agentName));
            }
        },
        [selectedAgentNames]
    );

    const handoffAgentOptions = useMemo(() => {
        if (!agents) {
            return [];
        }
        const filteredAgents = agents.filter(agent => !excludedAgentName || excludedAgentName !== agent.name).map(agent => agent.name);
        if (additionalHandoffAgents && additionalHandoffAgents.length > 0) {
            const combinedAgents = Array.from(new Set([...filteredAgents, ...additionalHandoffAgents]));
            return combinedAgents;
        }
        return filteredAgents;
    }, [agents, excludedAgentName, additionalHandoffAgents]);

    const dropdownDisplay = useMemo(() => {
        if (selectedAgentNames.length === 0) {
            return '';
        }

        const selectedCount = selectedAgentNames.length;
        const totalCount = handoffAgentOptions.length;

        return intl.formatMessage(IncidentManagementResources.selectedOutOfTotal, { selectedCount, totalCount });
    }, [intl, selectedAgentNames, handoffAgentOptions]);

    const pillItems = useMemo(() => {
        return selectedAgentNames.map(name => ({ key: name, label: name }));
    }, [selectedAgentNames]);

    const clear = useCallback(() => {
        setSelectedAgentNames([]);
    }, [setSelectedAgentNames]);

    return {
        handoffAgentOptions,
        onSelectedAgentChange,
        selectedAgentNames,
        setSelectedAgentNames,
        dropdownDisplay,
        pillItems,
        clear,
    };
};
