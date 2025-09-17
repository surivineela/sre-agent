import { Tooltip, tokens } from '@fluentui/react-components';
import { FC, ReactNode } from 'react';

export interface PermissionedActionLinkProps {
    /** Whether the user has permission to perform the action */
    canPerform: boolean;
    /** Tooltip text shown when user lacks permission */
    noPermissionTooltip: string;
    /** Hide link entirely instead of showing disabled+tooltip */
    hideIfNoPermission?: boolean;
    /** Additional disabled condition (busy, invalid state, etc.) */
    disabledReason?: boolean;
    /** Children (link content) */
    children: ReactNode;
    onClick?: React.MouseEventHandler<HTMLSpanElement | HTMLAnchorElement>;
    /** Optional className for styling */
    className?: string;
}

/**
 * Permission-aware Link. Renders a disabled-looking span wrapped in a tooltip when permission is missing.
 * When disabled via disabledReason, keeps normal appearance but prevents onClick.
 */
const linkStyleBase: React.CSSProperties = {
    color: tokens.colorBrandForegroundLink,
    cursor: 'pointer',
    textDecoration: 'underline',
};

const disabledLinkStyle: React.CSSProperties = {
    color: tokens.colorNeutralForegroundDisabled,
    cursor: 'not-allowed',
    textDecoration: 'none',
};

const PermissionedActionLink: FC<PermissionedActionLinkProps> = ({
    canPerform,
    noPermissionTooltip,
    hideIfNoPermission = false,
    disabledReason = false,
    onClick,
    children,
    className,
}) => {
    if (!canPerform) {
        if (hideIfNoPermission) return null;
        return (
            <Tooltip relationship="label" content={noPermissionTooltip}>
                <span style={disabledLinkStyle} aria-disabled="true" className={className} role="link">
                    {children}
                </span>
            </Tooltip>
        );
    }

    if (disabledReason) {
        return (
            <span style={disabledLinkStyle} aria-disabled="true" className={className} role="link">
                {children}
            </span>
        );
    }

    return (
        <span
            role="link"
            tabIndex={0}
            className={className}
            style={linkStyleBase}
            onClick={onClick as any}
            onKeyDown={e => {
                if (e.key === 'Enter' || e.key === ' ') {
                    e.preventDefault();
                    (onClick as any)?.(e as any);
                }
            }}
        >
            {children}
        </span>
    );
};

export default PermissionedActionLink;
