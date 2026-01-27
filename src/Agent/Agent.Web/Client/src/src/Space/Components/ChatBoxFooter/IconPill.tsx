import { mergeClasses, Tooltip } from '@fluentui/react-components';
import { Dismiss12Regular } from '@fluentui/react-icons';
import { FC, memo, useState } from 'react';
import { useIconPillStyles } from './styles';

export interface IconPillProps {
    icon: React.ReactNode;
    ariaLabel: string;
    onDismiss?: () => void;
    disabled?: boolean;
    tooltip?: string;
}

export const IconPill: FC<IconPillProps> = memo(({ icon, ariaLabel, onDismiss, disabled, tooltip }) => {
    const [isHovered, setIsHovered] = useState(false);
    const styles = useIconPillStyles();

    const pillContent = (
        <div
            className={styles.iconPill}
            role="button"
            aria-label={ariaLabel}
            onMouseEnter={() => setIsHovered(true)}
            onMouseLeave={() => setIsHovered(false)}
        >
            <span className={styles.iconPillIcon}>{icon}</span>
            {onDismiss && (
                <span
                    className={mergeClasses(styles.iconPillDismiss, isHovered && styles.iconPillDismissVisible)}
                    onClick={e => {
                        e.stopPropagation();
                        if (!disabled) {
                            onDismiss();
                        }
                    }}
                    style={disabled ? { cursor: 'not-allowed', opacity: 0.5 } : undefined}
                    role="button"
                    aria-label={`Dismiss ${ariaLabel}`}
                >
                    <Dismiss12Regular />
                </span>
            )}
        </div>
    );

    if (tooltip) {
        return (
            <Tooltip content={tooltip} relationship="label">
                {pillContent}
            </Tooltip>
        );
    }

    return pillContent;
});
