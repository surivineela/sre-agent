import { ToolbarButton, ToolbarButtonProps, Tooltip } from '@fluentui/react-components';
import { forwardRef, ReactNode } from 'react';

export interface PermissionedToolbarButtonProps extends Omit<ToolbarButtonProps, 'disabled'> {
    /** Whether the user has permission to perform the action */
    canPerform: boolean;
    /** Tooltip text shown when the user lacks permission */
    noPermissionTooltip: string;
    /** Additional disabled condition (e.g., in-flight operation). Applied only when canPerform is true. */
    disabledReason?: boolean;
    /** If true, hides the button entirely when permission is missing instead of showing disabled state */
    hideIfNoPermission?: boolean;
    children: ReactNode;
}

/**
 * Permission wrapper specialized for ToolbarButton to avoid typing issues with polymorphic 'as' prop on PermissionedButton.
 */
export const PermissionedToolbarButton = forwardRef<HTMLButtonElement, PermissionedToolbarButtonProps>(
    ({ canPerform, noPermissionTooltip, disabledReason = false, hideIfNoPermission = false, children, ...buttonProps }, ref) => {
        if (!canPerform) {
            if (hideIfNoPermission) return null;
            return (
                <Tooltip content={noPermissionTooltip} relationship="label">
                    <ToolbarButton {...(buttonProps as ToolbarButtonProps)} disabled ref={ref}>
                        {children}
                    </ToolbarButton>
                </Tooltip>
            );
        }

        return (
            <ToolbarButton {...(buttonProps as ToolbarButtonProps)} disabled={disabledReason} ref={ref}>
                {children}
            </ToolbarButton>
        );
    }
);

PermissionedToolbarButton.displayName = 'PermissionedToolbarButton';

export default PermissionedToolbarButton;
