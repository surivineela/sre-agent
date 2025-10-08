import { Button, ButtonProps, Tooltip } from '@fluentui/react-components';
import { FC, ReactNode } from 'react';

export interface PermissionedButtonProps extends Omit<ButtonProps, 'disabled'> {
    /** Whether the user has permission to perform the action */
    canPerform: boolean;
    /** Tooltip text shown when the user lacks permission */
    noPermissionTooltip: string;
    /** Tooltip text shown when the user HAS permission (optional) */
    allowedTooltip?: string;
    /** Additional disabled condition (e.g., in-flight operation). Applied only when canPerform is true. */
    disabledReason?: boolean;
    /** If true, hides the button entirely when permission is missing instead of showing disabled state */
    hideIfNoPermission?: boolean;
    children?: ReactNode;
}

/**
 * Reusable button that encapsulates permission-based disabling + tooltip.
 * If the user lacks permission, we render a disabled button wrapped in a Tooltip with an explanatory message.
 */
export const PermissionedButton: FC<PermissionedButtonProps> = ({
    canPerform,
    noPermissionTooltip,
    allowedTooltip,
    disabledReason = false,
    hideIfNoPermission = false,
    children,
    ...buttonProps
}) => {
    if (!canPerform) {
        if (hideIfNoPermission) return null;
        return (
            <Tooltip content={noPermissionTooltip} relationship="label">
                <Button {...(buttonProps as ButtonProps)} disabled>
                    {children}
                </Button>
            </Tooltip>
        );
    }

    const button = (
        <Button {...(buttonProps as ButtonProps)} disabled={disabledReason}>
            {children}
        </Button>
    );

    return allowedTooltip ? (
        <Tooltip content={allowedTooltip} relationship="label">
            {button}
        </Tooltip>
    ) : (
        button
    );
};

export default PermissionedButton;
