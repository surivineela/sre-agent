import { EntityCard, EntityTitle } from '@fluentui-copilot/react-copilot';
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

export const SelectableCard: FC<SelectableCardProps> = ({ onSelect, icon, title, disabled = false }) => {
    const styles = useSelectableCardStyles();

    const handleClick = useCallback(() => {
        if (!disabled) {
            onSelect();
        }
    }, [onSelect, disabled]);

    return (
        <EntityCard
            onClick={handleClick}
            className={styles.card}
            entityTitle={<EntityTitle media={icon ? <div className={styles.iconContainer}>{icon}</div> : undefined} primaryText={title} />}
        />
    );
};
