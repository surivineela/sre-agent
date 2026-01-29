import { EntityCardProps, EntityTitleSlots } from '@fluentui-copilot/react-copilot';
import { makeStyles, mergeClasses, Text, tokens } from '@fluentui/react-components';
import { cloneElement, FC, isValidElement, ReactElement, ReactNode } from 'react';

const useStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        padding: '5px 0px',
    },
    card: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '12px',
        padding: '12px',
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: '12px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        cursor: 'pointer',
        transitionProperty: 'background-color, border-color',
        transitionDuration: '0.15s',
        transitionTimingFunction: 'ease',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
            border: `1px solid ${tokens.colorNeutralStroke1Hover}`,
        },
        ':active': {
            backgroundColor: tokens.colorNeutralBackground3Pressed,
        },
    },
    iconContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        width: '48px',
        height: '48px',
        backgroundColor: tokens.colorNeutralBackground4,
        borderRadius: '8px',
        flexShrink: 0,
    },
    icon: {
        color: tokens.colorNeutralForeground2,
        fontSize: '24px',
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        flex: 1,
        minWidth: 0,
    },
    primaryText: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    secondaryText: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground3,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
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

    // Clone the icon element to apply our styles, or render as-is if not a valid element
    const renderIcon = (): ReactNode => {
        if (isValidElement(props.icon)) {
            return cloneElement(props.icon as ReactElement<{ className?: string }>, {
                className: mergeClasses((props.icon as ReactElement<{ className?: string }>).props?.className, styles.icon),
            });
        }
        return props.icon as ReactNode;
    };

    return (
        <div className={styles.root}>
            <div
                className={styles.card}
                role="button"
                tabIndex={0}
                onClick={e => {
                    e.stopPropagation();
                    props.onClick?.(e as unknown as React.MouseEvent<HTMLDivElement>);
                }}
                onKeyDown={e => {
                    if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault();
                        e.stopPropagation();
                        props.onClick?.(e as unknown as React.MouseEvent<HTMLDivElement>);
                    }
                }}
            >
                <div className={styles.iconContainer}>{renderIcon()}</div>
                <div className={styles.content}>
                    <Text className={styles.primaryText}>{props.primaryText}</Text>
                    <Text className={styles.secondaryText}>{props.secondaryText}</Text>
                </div>
            </div>
            {props.children}
        </div>
    );
};
