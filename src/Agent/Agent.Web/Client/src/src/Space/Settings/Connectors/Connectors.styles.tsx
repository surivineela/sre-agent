import { makeStyles, tokens } from '@fluentui/react-components';

export const useConnectorsStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXL,
    },
    titleContainer: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    title: {
        margin: '0px',
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
    },
    toolbar: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        gap: tokens.spacingHorizontalM,
    },
    divider: {
        flexGrow: 0,
    },
    button: {
        fontWeight: tokens.fontWeightRegular,
        padding: 0,
        minWidth: '20px',
    },
    searchBox: {
        width: '230px',
        zIndex: 1,
    },
    noItemsContainer: {
        marginTop: '40px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalXXL,
    },
    noSearchResultsContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: tokens.spacingVerticalXXL,
        marginTop: tokens.spacingVerticalXL,
    },
    textContainer: {
        maxWidth: '600px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: tokens.spacingVerticalS,
    },

    primaryTitle: {
        fontSize: '18px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
        whiteSpace: 'nowrap',
    },
    secondaryTitle: {
        fontSize: '16px',
        fontWeight: '600',
        color: tokens.colorNeutralForeground1,
    },
    description: {
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
        lineHeight: '20px',
        whiteSpace: 'nowrap',
    },
    searchDescription: {
        fontSize: '14px',
        color: tokens.colorNeutralForeground2,
    },
    dataGrid: {
        marginTop: '12px',
    },
    emptyStateContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        minHeight: '400px',
    },
    shimmerContainer: {
        marginTop: '14px',
        marginBottom: '14px',
    },
    nameCellContainer: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        width: '100%',
    },
    nameText: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        marginRight: '12px',
    },
    nameMenuContainer: {
        flexShrink: 0,
    },
    connectorIcon: {
        width: '20px',
        height: '20px',
    },
    iconAndTextContainer: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    statusIcon: {
        color: tokens.colorPaletteGreenForeground1,
    },
    headerCell: {
        fontWeight: '500',
    },
});
