import { makeStyles, tokens } from '@fluentui/react-components';

export const useKnowledgeBaseStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        gap: '16px',
        width: '100%',
    },
    header: {
        fontSize: '18px',
        fontWeight: 600,
    },
    description: {
        marginBottom: '16px',
    },
    buttonsContainer: {
        marginBottom: '16px',
    },
    pillsContainer: {
        display: 'flex',
        flexDirection: 'row',
        gap: '5px',
    },
    searchBox: {
        width: '230px',
        marginLeft: '16px',
        zIndex: 1,
    },
    toolbarButton: {
        paddingLeft: '0px',
        minWidth: '20px',
    },
    toolbarRefresh: {
        minWidth: '20px',
        marginLeft: 'auto',
    },
    toolbarDivider: {
        padding: '0px',
    },
    filesSelectedText: {
        marginBottom: '8px',
    },
    detailsListContainer: {
        width: '100%',
        overflow: 'visible',
    },
    detailsList: {
        paddingTop: '0px',
        marginTop: '0px',
    },
    noFilesContainer: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        justifyContent: 'center',
        textAlign: 'center',
        gap: '20px',
        paddingTop: '0px',
    },
    dialogSurface: {
        maxWidth: '800px',
        minWidth: '500px',
        padding: '0px',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
    },
    dialogBody: {
        padding: '24px',
    },
    dropZone: {
        borderRadius: '8px',
        padding: '24px 16px',
        textAlign: 'center',
        minHeight: '150px',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground2,
    },
    dropZoneIdle: {
        border: `2px dashed ${tokens.colorNeutralStroke2}`,
        backgroundColor: 'transparent',
    },
    dropZoneDragOver: {
        border: `2px dashed ${tokens.colorBrandStroke1}`,
        backgroundColor: tokens.colorNeutralBackground2,
    },
    emptyDropZone: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
        gap: '12px',
    },
    selectedFilesContainer: {
        width: '100%',
        textAlign: 'left',
        paddingTop: '25px',
        paddingBottom: '25px',
    },
    selectedFilesTitle: {
        marginBottom: '8px',
        fontWeight: 500,
        display: 'block',
    },
    fileList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '6px',
        marginBottom: '12px',
    },
    fileItem: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        padding: '6px 8px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    fileName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
        maxWidth: '250px',
    },
    fileActions: {
        display: 'flex',
        gap: '8px',
        justifyContent: 'center',
    },
    hiddenFileInput: {
        display: 'none',
    },
    dialogFooter: {
        borderTop: '1px solid ' + tokens.colorNeutralStroke1Pressed,
        width: '100%',
        padding: '16px',
        justifyContent: 'flex-end',
        gap: '8px',
    },
    nameCell: {
        width: '200px',
        minWidth: '200px',
    },
    nameCellContent: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        paddingRight: '12px',
    },
    fileTableHeaderCell35: {
        width: '35%',
    },
    fileTableHeaderCell25: {
        width: '25%',
    },
    fileTableHeaderCell30: {
        width: '30%',
    },
    fileTableHeaderCell10: {
        width: '10%',
    },
    fileTableScrollContainer: {
        maxHeight: '250px',
        overflowY: 'auto',
    },
    fileIconCell: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    fileIcon: {
        fontSize: '16px',
        color: tokens.colorNeutralForeground2,
    },
    classificationDropdown: {
        minWidth: '180px',
    },
    editDialogSurface: {
        padding: '0px',
        width: '550px',
    },
    editDialogTitle: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
    },
    editDialogTitleContent: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    editDialogSubtitle: {
        fontSize: '14px',
        fontWeight: 'normal',
        color: tokens.colorNeutralForeground2,
    },
    editDialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '32px',
        paddingTop: '32px',
        paddingBottom: '32px',
    },
    editDialogField: {
        width: '100%',
    },
    uploadInfoContainer: {
        display: 'flex',
        justifyContent: 'space-between',
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    uploadInfoText: {
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
    },
    browseLinkText: {
        color: tokens.colorBrandForeground1,
        textDecoration: 'underline',
        cursor: 'pointer',
    },
    folderIcon: {
        height: '50px',
        width: '50px',
    },
    checkboxCell: {
        width: '20px',
        minWidth: '20px',
    },
    sizeCell: {
        width: '150px',
        minWidth: '150px',
    },
    classificationCell: {
        width: '150px',
        minWidth: '150px',
    },
    dateCell: {
        width: '200px',
        minWidth: '200px',
    },
    menuContainer: {
        marginRight: '20px',
    },
    dismissButton: {
        minWidth: 'auto',
    },
    editDialogButton: {
        minWidth: '40px',
    },
});
