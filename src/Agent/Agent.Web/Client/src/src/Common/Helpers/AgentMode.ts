import { IntlShape } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';
import { AgentMode } from '../Contracts/Azure/SreAgent';

export const getAgentModeDisplayName = (mode: string, intl: IntlShape): string => {
    const lowercaseMode = mode?.toLowerCase() ?? '';
    switch (lowercaseMode) {
        case AgentMode.autonomous:
            return intl.formatMessage(SreAgentResources.autonomous);
        case AgentMode.review:
            return intl.formatMessage(SreAgentResources.review);
        case AgentMode.readonly:
            return intl.formatMessage(SreAgentResources.readonly);
        default:
            return '';
    }
};
