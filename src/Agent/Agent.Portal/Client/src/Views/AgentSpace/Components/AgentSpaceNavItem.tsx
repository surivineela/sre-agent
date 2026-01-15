import { Button, makeStyles, mergeClasses, tokens, Tooltip } from '@fluentui/react-components';
import { Open16Regular } from '@fluentui/react-icons';
import { FC, JSX } from 'react';

const useStyles = makeStyles({
    navItem: {
        justifyContent: 'flex-start',
        width: '100%',
        borderRadius: tokens.borderRadiusMedium,
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        minHeight: '36px',
        minWidth: 0,
        border: '1px solid transparent',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground2,
            border: `1px solid ${tokens.colorNeutralStroke2}`,
        },
    },
    navItemSelected: {
        backgroundColor: tokens.colorNeutralBackground1,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1,
        },
    },
    navItemCollapsed: {
        justifyContent: 'center',
        paddingLeft: tokens.spacingHorizontalS,
        paddingRight: tokens.spacingHorizontalS,
        width: 'auto',
    },
    labelContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalXS,
        marginLeft: tokens.spacingHorizontalS,
        whiteSpace: 'nowrap',
        overflow: 'hidden',
    },
});

interface AgentSpaceNavItemProps {
    icon: JSX.Element;
    label: string;
    isSelected: boolean;
    isNavOpen: boolean;
    onClick: () => void;
    isExternal?: boolean;
}

export const AgentSpaceNavItem: FC<AgentSpaceNavItemProps> = ({ icon, label, isSelected, isNavOpen, onClick, isExternal = false }) => {
    const styles = useStyles();

    const button = (
        <Button
            appearance="subtle"
            className={mergeClasses(styles.navItem, isSelected && styles.navItemSelected, !isNavOpen && styles.navItemCollapsed)}
            icon={icon}
            onClick={onClick}
            aria-current={isSelected ? 'page' : undefined}
        >
            {isNavOpen && (
                <span className={styles.labelContainer}>
                    {label}
                    {isExternal && <Open16Regular />}
                </span>
            )}
        </Button>
    );

    if (!isNavOpen) {
        return (
            <Tooltip content={label} relationship="label" positioning="after">
                {button}
            </Tooltip>
        );
    }

    return button;
};
