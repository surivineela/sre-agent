import { makeStyles, tokens } from '@fluentui/react-components';

export const useDataKnowledgeSpaceStyles = makeStyles({
    settingsContainer: {
        margin: '-32px -32px 0 -32px',
    },
    outerContainer: {
        padding: '32px',
    },
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        width: '100%',
    },
    tabsContainer: {
        padding: '0px 32px',
    },
    containerDivider: {
        borderTop: `1px solid ${tokens.colorNeutralStroke1Pressed}`,
        padding: '16px 32px 0 32px',
    },
    dangerButton: {
        backgroundColor: tokens.colorPaletteRedBackground3,
    },
    header: {
        fontSize: '18px',
        fontWeight: 600,
    },
    toolbar: {
        display: 'flex',
        justifyContent: 'start',
        gap: '12px',
        padding: '5px',
        paddingLeft: '8px',
    },
    button: {
        fontWeight: 400,
        padding: 0,
        minWidth: '20px',
    },
    searchBox: {
        width: '230px',
        marginLeft: '16px',
        zIndex: 1,
    },
    toolbarRefresh: {
        minWidth: '20px',
        marginLeft: 'auto',
    },
});

export const useAddEditConnectorsStyles = makeStyles({
    dialogSurface: {
        padding: '0px',
        maxWidth: '95vw',
        width: '1000px',
        height: '600px',
    },
    dialogBody: {
        padding: '24px',
    },
    searchBoxContainer: {
        marginBottom: '16px',
    },
    searchBox: {
        width: '350px',
    },
    outerContainer: {
        padding: '32px',
    },
    cardContainer: {
        padding: '10px 0',
        height: '380px',
        overflow: 'auto',
    },
    cardGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(3, 1fr)',
        gap: '16px',
        padding: '3px',
    },
    image: {
        width: '32px',
        height: '32px',
    },
    serviceDescription: {
        color: '#666',
        display: 'block',
    },
    serviceMoreInfoText: {
        marginTop: '8px',
    },
    formSeparator: {
        borderTop: `1px solid ${tokens.colorNeutralStroke1Pressed}`,
        width: '100%',
    },
    dialogActionsContainer: {
        justifyContent: 'flex-end',
        padding: '16px 24px',
    },
    connectorHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
    },
    connectorIcon: {
        width: '32px',
        height: '32px',
    },
    connectorTypeText: {
        fontSize: '12px',
        color: '#666',
        fontWeight: 'normal',
    },
    form: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        paddingBottom: '32px',
        paddingTop: '16px',
    },
    identityLink: {
        marginTop: '8px',
        display: 'block',
        fontSize: '14px',
    },
    dialogActionsSpaceBetween: {
        justifyContent: 'space-between',
        width: '100%',
    },
    buttonGroup: {
        display: 'flex',
        gap: '8px',
    },
});

export const useEmptyStateStyles = makeStyles({
    noItemsContainer: {
        marginTop: '40px',
    },
    noSearchResultsContainer: {
        marginTop: '20px',
    },
    emptyStateContent: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '24px',
    },
    textContainer: {
        maxWidth: '600px',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        textAlign: 'center',
        gap: '8px',
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
});

export const useDeleteConfirmationDialogStyles = makeStyles({
    dialogTitle: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    dismissButton: {
        minWidth: 'auto',
    },
    itemsContainer: {
        marginTop: '16px',
    },
    itemRow: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
        marginBottom: '8px',
    },
    itemIcon: {
        height: '16px',
        width: '16px',
    },
});

export const useDataConnectorsStyles = makeStyles({
    dataGrid: {
        marginTop: '12px',
    },
    emptyStateContainer: {
        marginTop: '20px',
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
        minWidth: '200px',
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
    connectorTypeContainer: {
        marginTop: '14px',
        marginBottom: '14px',
    },
    connectorTypeName: {
        fontSize: '14px',
    },
    connectorTypeService: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    statusContainer: {
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
