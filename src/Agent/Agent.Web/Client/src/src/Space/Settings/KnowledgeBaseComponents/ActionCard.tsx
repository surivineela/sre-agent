import { Card, Text, mergeClasses } from '@fluentui/react-components';
import { FC, ReactNode } from 'react';
import { useActionCardStyles } from '../Styles/KnowledgeSettings.styles';

interface ActionCardProps {
    icon: ReactNode;
    label: string;
    onClick: () => void;
    disabled?: boolean;
}

export const ActionCard: FC<ActionCardProps> = ({ icon, label, onClick, disabled = false }) => {
    const styles = useActionCardStyles();

    return (
        <Card
            className={mergeClasses(styles.card, disabled && styles.cardDisabled)}
            onClick={disabled ? undefined : onClick}
            role="button"
            tabIndex={disabled ? -1 : 0}
            onKeyDown={e => {
                if (!disabled && (e.key === 'Enter' || e.key === ' ')) {
                    e.preventDefault();
                    onClick();
                }
            }}
            aria-disabled={disabled}
        >
            <div className={styles.cardContent}>
                <div className={styles.iconContainer}>{icon}</div>
                <Text className={styles.label}>{label}</Text>
            </div>
        </Card>
    );
};
