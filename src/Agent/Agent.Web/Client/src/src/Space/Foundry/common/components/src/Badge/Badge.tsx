import type { BadgeProps as FluentBadgeProps } from '@fluentui/react-components';
import { Badge as FluentBadge, mergeClasses } from '@fluentui/react-components';
import { forwardRef } from 'react';
import { useBadgeStyles } from './Badge.Styles';

export type BadgeAppearance = 'filled' | 'tint' | 'outline' | 'ghost';
export type BadgeColor = 'danger' | 'warning' | 'success' | 'informative' | 'important';
export type BadgeSize = 'small' | 'medium' | 'large' | 'extra-large';

export type IBadgeProps = Omit<FluentBadgeProps, 'color' | 'size' | 'appearance' | 'shape'> & {
    appearance?: BadgeAppearance;
    color?: BadgeColor;
    size?: BadgeSize;
};

export const Badge = forwardRef<HTMLDivElement, IBadgeProps>(
    ({ appearance = 'filled', className, color = 'informative', size = 'medium', ...rest }, ref) => {
        const colorMap: Record<BadgeColor, 'severe' | 'success' | 'danger' | 'informative' | 'important'> = {
            warning: 'severe',
            success: 'success',
            danger: 'danger',
            informative: 'informative',
            important: 'important',
        };

        const styles = useBadgeStyles();

        return (
            <FluentBadge
                ref={ref}
                appearance={appearance}
                className={mergeClasses(className, styles.badge)}
                color={colorMap[color]}
                shape="rounded"
                size={size}
                {...rest}
            />
        );
    }
);
Badge.displayName = 'Badge';
