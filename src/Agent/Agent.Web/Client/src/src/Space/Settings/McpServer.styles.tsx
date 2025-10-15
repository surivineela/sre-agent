import { makeStyles, tokens } from '@fluentui/react-components';

export const useMcpServerStyles = makeStyles({
    headerContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginBottom: tokens.spacingVerticalXL,
    },
    headerTitle: {
        fontFamily: 'Segoe UI',
        fontWeight: 600,
        fontSize: '18px',
        leadingTrim: 'none',
        lineHeight: '24px',
        letterSpacing: '0%',
        margin: '0px',
    },
    headerDescription: {
        fontFamily: 'Segoe UI',
        fontWeight: 400,
        fontStyle: 'normal',
        fontSize: '14px',
        leadingTrim: 'none',
        lineHeight: '20px',
        letterSpacing: '0%',
    },
    toolbar: {
        padding: '0px',
        marginBottom: tokens.spacingVerticalXL,
    },
    toolbarButton: {
        fontFamily: 'Segoe UI',
        fontWeight: 400,
        fontStyle: 'normal',
        fontSize: '12px',
        leadingTrim: 'none',
        lineHeight: '16px',
        letterSpacing: '0%',
        verticalAlign: 'middle',
    },
    dataGridHeader: {
        fontFamily: 'Segoe UI',
        fontWeight: 400,
        fontSize: '14px',
        leadingTrim: 'none',
        lineHeight: '20px',
        letterSpacing: '0%',
        verticalAlign: 'middle',
    },
    emptyStateContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '20px',
        margin: '66px',
    },
    emptyStateTextContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '8px',
    },
});
