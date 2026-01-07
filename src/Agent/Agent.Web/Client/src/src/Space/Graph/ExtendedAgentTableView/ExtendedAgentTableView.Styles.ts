import { makeStyles, tokens } from '@fluentui/react-components';

export const useListViewStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        paddingTop: '16px',
        paddingBottom: '16px',
        paddingLeft: '21px',
    },
    descriptionText: {
        padding: '24px 0px 20px 0px',
    },
    cardsContainer: {
        display: 'flex',
        gap: '20px',
        marginBottom: '24px',
    },
    cardHeader: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        gap: '20px',
    },
    cardTitle: {
        width: '170px',
    },
    cardCount: {
        display: 'flex',
    },
    entityTable: {
        paddingTop: '4px',
        paddingRight: '20px',
        paddingBottom: '20px',
        overflowY: 'auto',
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: '5px',
        paddingBottom: '20px',
    },
    searchAndToolbar: {
        display: 'flex',
        gap: '5px',
    },
    searchBox: {
        minWidth: '220px',
        width: 'max-content',
    },
    searchBoxAndFilters: {
        display: 'flex',
        gap: '10px',
        alignItems: 'center',
    },
    toolbarButtons: {
        padding: '0px',
    },
    toolbarButton: {
        fontWeight: 'normal',
    },
    tableHeader: {
        fontWeight: '600',
    },
    emptyState: {
        padding: '40px',
        textAlign: 'center',
        color: tokens.colorNeutralForeground3,
    },
    errorBar: {
        marginBottom: '8px',
    },
    dangerButton: {
        backgroundColor: tokens.colorPaletteRedBackground3,
        color: tokens.colorNeutralForegroundOnBrand,
        ':hover': {
            backgroundColor: tokens.colorPaletteRedBackground2,
        },
        ':active': {
            backgroundColor: tokens.colorPaletteRedBackground1,
        },
    },
    cardContent: {
        width: '50px',
        display: 'flex',
        justifyContent: 'flex-end',
    },
    tableCellContent: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        width: '100%',
    },
    tableCellActionsWrapper: {
        display: 'flex',
        gap: '8px',
    },
    transparentButton: {
        padding: 0,
        minWidth: 'auto',
        justifyContent: 'flex-start',
    },
    clickableText: {
        color: tokens.colorBrandForeground1,
        cursor: 'pointer',
    },
    containerWrapper: {
        display: 'flex',
        height: '100%',
        width: '100%',
        overflowX: 'auto',
        position: 'relative',
    },
    containerFlex: {
        flex: '1',
    },
    clickableCard: {
        cursor: 'pointer',
    },
    flexRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    flexRowSmall: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    flexRowMedium: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    greenIcon: {
        color: tokens.colorPaletteGreenForeground1,
    },
    redIcon: {
        color: tokens.colorPaletteRedForeground1,
    },
    minWidthTable: {
        minWidth: '800px',
    },
    infoPanelAbsolute: {
        position: 'absolute',
        right: 0,
        top: 0,
        height: '100%',
        zIndex: 1000,
    },
    lastUpdated: {
        display: 'flex',
        gap: '6px',
        alignItems: 'center',
    },
});
