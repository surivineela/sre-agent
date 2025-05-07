import { ApiVersions } from '../ApiVersions';
import MakeArmCall from './ArmClient';

export interface PermissionsCheckResponse {
    value: [
        {
            actions: string[];
            notActions: string[];
            dataActions: string[];
            notDataActions: string[];
        },
    ];
}

export enum PermissionAction {
    read = './read',
    write = './write',
    stopSiteAction = './stop/action',
    startSiteAction = './start/action',
}

interface PermissionsAsRegExp {
    actions: RegExp[];
    notActions: RegExp[];
}

export interface Permissions {
    /**
     * The actions that are permitted.
     */
    readonly actions: ReadonlyArray<string>;

    /**
     * The actions that are explicitly not permitted.
     */
    readonly notActions: ReadonlyArray<string>;
}

const isAllowed = (requestedAction: string, permission: PermissionsAsRegExp): boolean => {
    const actionAllowed = permission.actions.some(action => {
        return action.test(requestedAction);
    });
    const actionDenied = permission.notActions.some(notAction => {
        return notAction.test(requestedAction);
    });

    return actionAllowed && !actionDenied;
};

const wildCardEscapeSequence = '\\*';

// Escape reserved regex characters so that they are not interpreted by regex evaluation.
/*
 * 1. All allowed character escapes are taken into account: \*, \t, \n, \r, \\, \'
 *    a. \0 is explicitly not supported
 * 2. All non-escaped wildcards match 0 or more characters of anything
 * 3. The entire wildcard pattern is matched from beginning to end, and no more (e.g., a*d matches add but not adding or bad).
 * 4. The pattern matching should be case insensitive.
 */
const escapeRegExp = (regex: string): string => {
    return regex
        .replace(/([.*+?^=!:${}()|[\]/\\])/g, '\\$1')
        .replace('\\/\\*\\/', '\\/?\\*\\/') // first make any / before a wildcard \\* and a slash / optional but leave the wildcard escaped.
        .replace(wildCardEscapeSequence, '.*') // the previous command escaped legitimate wildcards - replace them with Regex wildcards
        .replace('\x00', wildCardEscapeSequence) // replace sentinels with truly escaped wildcards
        .replace('\\t', '\t') // tabs
        .replace('\\n', '\n') // newlines
        .replace('\\r', '\r') // carriage returns
        .replace('\\\\', '\\') // backslashes
        .replace("\\'", "'"); // single quotes
};

const permissionsToRegExp = (permissions: Permissions): PermissionsAsRegExp => {
    const actions = permissions.actions.map((val: any) => {
        return actionToRegExp(val);
    });
    const notActions = permissions.notActions.map((val: any) => {
        return actionToRegExp(val);
    });

    return {
        actions: actions,
        notActions: notActions,
    };
};

const actionToRegExp = (wildCardPattern: string): RegExp => {
    wildCardPattern = wildCardPattern.replace(wildCardEscapeSequence, '\x00'); // sentinel for escaped wildcards
    const regex = escapeRegExp(wildCardPattern);
    if (wildCardPattern.endsWith('/*')) {
        // If it ends with /* then we have to match X/* or X
        const regex2 = escapeRegExp(wildCardPattern.substring(0, wildCardPattern.length - 2));
        return new RegExp('^((' + regex + ')|(' + regex2 + '))$', 'i'); // perform case insensitive compares
    } else {
        return new RegExp('^' + regex + '$', 'i'); // perform case insensitive compares
    }
};

export function getResourceType(resourceId: string): string | null {
    const parts = resourceId.split('/').filter(Boolean);

    const providerIndex = parts.indexOf('providers');
    if (providerIndex === -1 || providerIndex + 2 >= parts.length) {
        return null; // Invalid format
    }

    const namespace = parts[providerIndex + 1];
    const type = parts[providerIndex + 2];

    return `${namespace}/${type}`;
}

export class PermissionClient {
    private static _instance = new PermissionClient();

    public static getInstance() {
        return PermissionClient._instance;
    }

    /**
     * https://msazure.visualstudio.com/DefaultCollection/One/_git/AzureUX-PortalFx?path=/src/SDK/Website/TypeScript/MsPortalImpl/Services/PermissionsHelpers.ts
     * Evaluates the set of requested actions against the set of active permissions for an entity.
     * @param requestedActions The requested actions.
     * @param permissionSet The set of active permissions.
     */
    public canPerformActions(
        requestedActions: ReadonlyArray<string>,
        permissionSet: ReadonlyArray<Permissions>,
        resourceId: string
    ): boolean {
        if (!requestedActions || !permissionSet || permissionSet.length === 0) {
            // If there are no requested actions or no available actions the caller has no permissions
            return false;
        }

        // Convert available actions to regexes
        const permissionSetRegexes = permissionSet.map(permissionsToRegExp);

        const resourceType = getResourceType(resourceId) ?? '';

        // Every requested action must be allowed by the permission set
        const result = requestedActions.every(item => {
            if (item.length > 1 && item.charAt(0) === '.' && item.charAt(1) === '/') {
                // Special case: turn leading ./ to {resourceType}/ for formatting.
                item = resourceType + item.substring(1);
            }

            return permissionSetRegexes.some(availableRegex => {
                return isAllowed(item, availableRegex);
            });
        });

        return result;
    }

    /**
     * Only use this call if you want to handle the raw content results.
     * Can run the result of this call through {@link canPerformActions}.
     *
     * Otherwise use {@link hasPermission}
     */
    public getPermissions(resourceID: string, apiVersion = ApiVersions.PermissionsApiVersion) {
        return MakeArmCall<PermissionsCheckResponse>({
            commandName: 'checkPermissions',
            method: 'GET',
            url: `${resourceID}/providers/Microsoft.Authorization/permissions?api-version=${apiVersion}`,
        });
    }

    public hasPermission(resourceId: string, actions: string[]) {
        return this.getPermissions(resourceId).then(response => {
            return this.canPerformActions(actions, response.data?.value, resourceId);
        });
    }
}
