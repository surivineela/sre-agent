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
        width: '330px',
        zIndex: 1,
    },
    toolbarButton: {
        paddingLeft: '0px',
        minWidth: '20px',
    },
    toolbarDivider: {
        padding: '0px',
    },
    filesSelectedText: {
        marginBottom: '8px',
    },
    detailsListContainer: {
        width: '99%',
        overflow: 'hidden',
    },
    detailsList: {
        paddingTop: '0px',
        marginTop: '-16px',
    },
    noFilesContainer: {
        padding: '20px',
        textAlign: 'center',
    },
    dialogSurface: {
        maxWidth: '500px',
        minWidth: '400px',
    },
    dialogContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
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
    },
    selectedFilesTitle: {
        marginBottom: '8px',
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
});
