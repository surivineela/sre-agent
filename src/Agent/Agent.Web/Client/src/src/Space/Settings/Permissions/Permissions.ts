import { IntlShape } from 'react-intl';
import { AgentPermissionsResources } from '../../../Strings/SREAgentResources';

export enum CrossTenantRoles {
    StandardUser = 'StandardUser',
    Reader = 'Reader',
    Author = 'Author',
}

export const getRoleDisplayName = (role: string, intl: IntlShape): string => {
    const roleDisplayMap: Record<string, string> = {
        [CrossTenantRoles.StandardUser]: intl.formatMessage(AgentPermissionsResources.roleStandardUser),
        [CrossTenantRoles.Reader]: intl.formatMessage(AgentPermissionsResources.roleReader),
        [CrossTenantRoles.Author]: intl.formatMessage(AgentPermissionsResources.roleAuthor),
    };
    return roleDisplayMap[role] ?? role;
};
