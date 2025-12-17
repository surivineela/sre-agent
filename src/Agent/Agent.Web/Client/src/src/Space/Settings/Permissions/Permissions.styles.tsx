import { makeStyles, tokens } from '@fluentui/react-components';

export const usePermissionsStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
    },
    header: {
        marginBottom: tokens.spacingVerticalS,
        fontSize: '18px',
    },
    headerDescription: {
        color: tokens.colorNeutralForeground3,
        marginBottom: tokens.spacingVerticalL,
    },
    toolbar: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        gap: tokens.spacingHorizontalM,
        marginBottom: tokens.spacingVerticalM,
    },
    dataGridWrapper: {
        marginTop: tokens.spacingVerticalM,
        overflowX: 'auto',
    },
    dataGrid: {},
    headerCell: {
        fontWeight: tokens.fontWeightSemibold,
    },
    emptyStateContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '400px',
    },
    emptyStateContent: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalL,
        textAlign: 'center',
    },
    emptyStateTitle: {
        fontSize: tokens.fontSizeBase400,
        fontWeight: tokens.fontWeightSemibold,
    },
    emptyStateDescription: {
        color: tokens.colorNeutralForeground3,
    },
    emptyStateIcon: {
        fontSize: '48px',
        color: tokens.colorBrandForeground1,
    },
    shimmerContainer: {
        marginTop: tokens.spacingVerticalM,
        marginBottom: tokens.spacingVerticalM,
    },
    toolbarButton: {
        paddingLeft: '0px',
        minWidth: '20px',
    },
    toolbarDivider: {
        padding: '0px',
    },
});

export const useAddPermissionDialogStyles = makeStyles({
    dialogSurface: {
        maxWidth: '500px',
        minWidth: '400px',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalL,
    },
    dialogActions: {
        paddingTop: tokens.spacingVerticalL,
    },
});
