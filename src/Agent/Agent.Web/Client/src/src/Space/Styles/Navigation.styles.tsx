import { makeStyles, tokens } from '@fluentui/react-components';

// Shared list item styles for consistent hover and selection across all navigation lists
export const useSharedListItemStyles = makeStyles({
    listItem: {
        cursor: 'pointer',
        borderRadius: '16px',
        border: '1px solid transparent', // Transparent border to maintain consistent sizing
        ':hover': {
            backgroundColor: `${tokens.colorNeutralBackground2} !important`,
            border: `1px solid ${tokens.colorNeutralStroke2}`,
        },
    },
    listItemSelected: {
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        borderRadius: '16px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
});

// Shared navigation item base styles (to be used with spread operator)
export const sharedNavItemStyles = {
    backgroundColor: 'transparent',
    cursor: 'pointer',
    borderRadius: '16px',
    border: '1px solid transparent',
    paddingLeft: '18px', // 8px original + 10px for consistent alignment
    gap: '8px',
    alignItems: 'center',
    outline: 'none', // Remove focus outline
    '&[aria-current="page"]': {
        backgroundColor: `${tokens.colorNeutralBackground1} !important`,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        outline: 'none',
    },
    '&:hover': {
        backgroundColor: `${tokens.colorNeutralBackground2} !important`,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    '&:focus': {
        outline: 'none !important',
    },
    '&:focus-visible': {
        outline: 'none !important',
    },
    '&:disabled': {
        backgroundColor: 'transparent',
        color: tokens.colorNeutralForegroundDisabled,
        cursor: 'not-allowed',
        border: '1px solid transparent',
    },
    '&:disabled:hover': {
        backgroundColor: 'transparent',
        border: '1px solid transparent',
    },
    '::after': {
        marginInlineStart: '-8px',
    },
} as const;

// Shared NavDrawer component styles
export const useSharedNavDrawerStyles = makeStyles({
    drawer: {
        height: 'calc(100% - 16px)',
        paddingTop: '16px',
        paddingLeft: '16px',
        backgroundColor: 'transparent',
        maxWidth: '240px', // Default width for Settings
    },
    drawerIncidentManagement: {
        height: 'calc(100% - 16px)',
        paddingTop: '16px',
        paddingLeft: '16px',
        backgroundColor: 'transparent',
        maxWidth: '215px', // Specific width for IncidentManagement
    },
    drawerCollapsed: {
        height: 'calc(100% - 16px)',
        paddingTop: '16px',
        paddingLeft: '16px',
        backgroundColor: 'transparent',
        width: '48px',
    },
    drawerHeader: {
        backgroundColor: 'transparent',
        padding: `0px 0px ${tokens.spacingVerticalXXS} 0px !important`,
    },
    drawerBody: {
        backgroundColor: 'transparent',
        padding: '0px',
    },
    headerButton: {
        paddingLeft: '8px',
        paddingRight: '8px',
        height: '40px',
        maxWidth: '40px',
    },
    item: {
        ...sharedNavItemStyles,
    },
    itemCollapsed: {
        ...sharedNavItemStyles,
        paddingLeft: '8px', // Reduced padding for collapsed state
        justifyContent: 'center',
        '&[aria-current="page"]': {
            backgroundColor: `${tokens.colorNeutralBackground1} !important`,
            border: `1px solid ${tokens.colorNeutralStroke2}`,
            outline: 'none',
        },
        '::after': {
            display: 'none !important', // Remove the selection indicator line
        },
    },
    itemIcon: {
        height: '16px',
        width: '16px',
        margin: 'auto',
        '&:hover': {
            backgroundColor: 'transparent',
        },
    },
    itemText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        flex: 1,
    },
});
