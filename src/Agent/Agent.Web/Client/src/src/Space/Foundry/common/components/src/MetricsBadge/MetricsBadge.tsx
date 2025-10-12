import { mergeClasses } from '@fluentui/react-components';
import { forwardRef } from 'react';
import { Badge, IBadgeProps } from '../Badge/Badge';
import { useMetricsBadgeStyles } from './MetricsBadge.Styles';

export interface IMetricsBadgeProps extends Pick<IBadgeProps, 'className' | 'icon'> {
    label: string;
}

export const MetricsBadge = forwardRef<HTMLDivElement, IMetricsBadgeProps>(({ label, className, icon }, ref) => {
    const styles = useMetricsBadgeStyles();
    return (
        <Badge ref={ref} className={mergeClasses(styles.badge, className)} icon={icon}>
            {label}
        </Badge>
    );
});
MetricsBadge.displayName = 'MetricsBadge';
