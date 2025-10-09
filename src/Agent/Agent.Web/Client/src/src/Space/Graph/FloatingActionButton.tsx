import { Button, makeStyles, tokens, Tooltip } from '@fluentui/react-components';
import { Add24Filled } from '@fluentui/react-icons';
import { FC } from 'react';

const useFABStyles = makeStyles({
    fab: {
        position: 'fixed',
        bottom: '32px',
        right: '32px',
        width: '56px',
        height: '56px',
        minWidth: '56px',
        borderRadius: '50%',
        boxShadow: tokens.shadow16,
        transition: 'all 0.3s cubic-bezier(0.4, 0, 0.2, 1)',
        zIndex: 1000,
        ':hover': {
            transform: 'scale(1.1) rotate(90deg)',
            boxShadow: tokens.shadow28,
        },
        ':active': {
            transform: 'scale(0.95) rotate(90deg)',
        },
    },
    icon: {
        fontSize: '24px',
    },
});

interface FloatingActionButtonProps {
    onClick: () => void;
    tooltip?: string;
}

export const FloatingActionButton: FC<FloatingActionButtonProps> = ({ onClick, tooltip = 'Create new entity' }) => {
    const styles = useFABStyles();

    return (
        <Tooltip content={tooltip} relationship="label" positioning="before">
            <Button
                appearance="primary"
                shape="circular"
                className={styles.fab}
                icon={<Add24Filled className={styles.icon} />}
                onClick={onClick}
            />
        </Tooltip>
    );
};
