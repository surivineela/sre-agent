import { Link, LinkProps, Tooltip } from '@fluentui/react-components';
import { FC } from 'react';

export interface PermissionedActionLinkProps {
    /** Whether the user has permission to perform the action */
    canPerform: boolean;
    /** Tooltip text shown when user lacks permission */
    noPermissionTooltip: string;
    /** Hide link entirely instead of showing disabled+tooltip */
    hideIfNoPermission?: boolean;
    /** Additional disabled condition (busy, invalid state, etc.) */
    disabledReason?: boolean;
}

const PermissionedActionLink: FC<PermissionedActionLinkProps & LinkProps> = ({
    canPerform,
    noPermissionTooltip,
    hideIfNoPermission = false,
    disabledReason = false,
    children,
    ...restProps
}) => {
    if (!canPerform) {
        if (hideIfNoPermission) return null;
    }

    const toolTip = canPerform ? '' : noPermissionTooltip;

    const link = (
        <Link disabled={disabledReason || !canPerform} {...restProps}>
            {children}
        </Link>
    );

    return toolTip ? (
        <Tooltip relationship="label" content={toolTip}>
            {link}
        </Tooltip>
    ) : (
        link
    );
};

export default PermissionedActionLink;
