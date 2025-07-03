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

export const getAgentModeDescription = (mode: string, intl: IntlShape): string => {
    const lowercaseMode = mode?.toLowerCase() ?? '';
    switch (lowercaseMode) {
        case AgentMode.autonomous:
            return intl.formatMessage(SreAgentResources.autonomousDescription);
        case AgentMode.review:
            return intl.formatMessage(SreAgentResources.reviewDescription);
        case AgentMode.readonly:
            return intl.formatMessage(SreAgentResources.readonlyDescription);
        default:
            return intl.formatMessage(SreAgentResources.agentModeUnknownDescription);
    }
};
