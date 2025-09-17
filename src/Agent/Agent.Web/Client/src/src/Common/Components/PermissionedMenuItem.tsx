import { MenuItem, MenuItemProps, Tooltip } from '@fluentui/react-components';
import { FC, ReactElement } from 'react';

export interface PermissionedMenuItemProps extends Omit<MenuItemProps, 'disabled'> {
    canPerform: boolean;
    noPermissionTooltip: string | ReactElement;
    disabledReason?: boolean; // additional disabled condition when user has permission
    hideIfNoPermission?: boolean; // optionally hide instead of showing disabled
    children: ReactElement | string;
}

/**
 * Wraps a Fluent MenuItem with permission gating + tooltip logic.
 * If user lacks permission and hideIfNoPermission is false, renders a disabled MenuItem inside a Tooltip.
 */
const PermissionedMenuItem: FC<PermissionedMenuItemProps> = ({
    canPerform,
    noPermissionTooltip,
    disabledReason = false,
    hideIfNoPermission = false,
    children,
    ...menuItemProps
}) => {
    if (!canPerform) {
        if (hideIfNoPermission) return null;
        return (
            <Tooltip content={noPermissionTooltip || ''} relationship="label" withArrow>
                <MenuItem {...(menuItemProps as MenuItemProps)} disabled>
                    {children}
                </MenuItem>
            </Tooltip>
        );
    }

    return (
        <MenuItem {...(menuItemProps as MenuItemProps)} disabled={disabledReason}>
            {children}
        </MenuItem>
    );
};

export default PermissionedMenuItem;
