import { PrimaryNavItemValues, SecondaryNavItemValues } from './Contracts/SreAgentSpace';

export const getPathName = (leadingForwardSlash: boolean, root: PrimaryNavItemValues, paths?: string[]) => {
    const childrenPath = paths && paths.length > 0 ? `${paths.join('/')}` : '';

    return `${leadingForwardSlash ? '/' : ''}views/${root}${childrenPath ? `/${childrenPath}` : ''}`;
};

export const getNavItemIdFromPathName = (pathname: string): string => {
    return pathname.startsWith('views/')
        ? pathname.substring('views/'.length)
        : pathname.startsWith('/views/')
          ? pathname.substring('/views/'.length)
          : pathname;
};

export const getCategoryNavItemIdFromPathName = (pathname: string): string => {
    const navItemId = getNavItemIdFromPathName(pathname);
    const navItemParts = navItemId.split('/');
    return navItemParts.length > 0 ? navItemParts[0] : '';
};

export const constructNavItemId = (
    primaryNavItemValue: PrimaryNavItemValues,
    secondaryNavItemValue: SecondaryNavItemValues | undefined,
    threadId: string | undefined,
    grandChildKey?: string
): string => {
    const parsePart = (input?: string) => (input ? `${input.startsWith('/') ? '' : '/'}${input}` : '');
    return `${primaryNavItemValue}${parsePart(secondaryNavItemValue)}${parsePart(threadId)}${parsePart(grandChildKey)}`;
};
