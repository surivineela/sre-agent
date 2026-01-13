import { EntityCard, EntityCardProps, EntityTitle, EntityTitleSlots } from '@fluentui-copilot/react-copilot';
import { makeStyles } from '@fluentui/react-components';
import { FC } from 'react';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        padding: '5px 0px',
    },
    card: {
        ':hover': {
            cursor: 'pointer',
        },
    },
});

interface ISpecialMessageCardProps {
    icon: EntityTitleSlots['media'];
    primaryText: string;
    secondaryText: string;
    onClick?: EntityCardProps['onClick'];
    children?: React.ReactNode;
}

export const SpecialMessageCard: FC<ISpecialMessageCardProps> = props => {
    const styles = useStyles();

    return (
        <div className={styles.root}>
            <EntityCard
                orientation="horizontal"
                role="group"
                style={{ maxWidth: 'unset' }}
                entityTitle={<EntityTitle media={props.icon} primaryText={props.primaryText} secondaryText={props.secondaryText} />}
                onClick={e => {
                    e.stopPropagation();
                    props.onClick?.(e);
                }}
                className={styles.card}
            />
            {props.children}
        </div>
    );
};
