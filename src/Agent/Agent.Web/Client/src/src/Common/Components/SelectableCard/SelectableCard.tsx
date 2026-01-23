import { Card, mergeClasses, Text } from '@fluentui/react-components';
import { FC, ReactNode, useCallback } from 'react';
import { useSelectableCardStyles } from './SelectableCard.styles';

export interface SelectableCardProps {
    onSelect: () => void;
    /** Typically an img or Fluent icon */
    icon?: ReactNode;
    title: string;
    className?: string;
    disabled?: boolean;
}

export const SelectableCard: FC<SelectableCardProps> = ({ onSelect, icon, title, className, disabled = false }) => {
    const styles = useSelectableCardStyles();

    const handleClick = useCallback(() => {
        if (!disabled) {
            onSelect();
        }
    }, [onSelect, disabled]);

    return (
        <Card className={mergeClasses(styles.card, disabled && styles.cardDisabled, className)} onClick={handleClick}>
            {icon && <div className={styles.iconContainer}>{icon}</div>}
            <Text className={styles.title}>{title}</Text>
        </Card>
    );
};
