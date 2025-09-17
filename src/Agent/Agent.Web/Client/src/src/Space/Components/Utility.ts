import { IntlShape } from 'react-intl';
import { SreAgentResources } from '../../Strings/SREAgentResources';

export const getRiskLevel = (command: string, intl: IntlShape): string => {
    const cmd = command.toLowerCase();

    if (cmd.includes('delete') || cmd.includes('remove') || cmd.includes('purge')) {
        return intl.formatMessage(SreAgentResources.highRisk);
    }

    if (cmd.includes('create') || cmd.includes('update') || cmd.includes('set') || cmd.includes('scale') || cmd.includes('restart')) {
        return intl.formatMessage(SreAgentResources.mediumRisk);
    }

    if (cmd.includes('start') || cmd.includes('stop') || cmd.includes('enable') || cmd.includes('disable')) {
        return intl.formatMessage(SreAgentResources.lowRisk);
    }

    // Read-only operations
    if (cmd.includes('list') || cmd.includes('show') || cmd.includes('get') || cmd.includes('describe')) {
        return intl.formatMessage(SreAgentResources.safe);
    }

    return intl.formatMessage(SreAgentResources.mediumRisk);
};

/** Ex: 7/15/25, 1:00:00 PM */
export const formatTimestampShort = (value: string | number | Date): string => {
    const dt = new Date(value);
    const locale = typeof navigator !== 'undefined' && navigator.language ? navigator.language : 'en-US';

    return new Intl.DateTimeFormat(locale, {
        year: '2-digit',
        month: 'numeric',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit',
        second: '2-digit',
        hour12: true,
    }).format(dt);
};
