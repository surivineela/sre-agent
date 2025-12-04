import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useSkillFileBrowserStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke1),
        ...shorthands.borderRadius('4px'),
        overflow: 'hidden',
        flexGrow: 1,
        minHeight: 0,
    },
    fileListPanel: {
        display: 'flex',
        flexDirection: 'column',
        backgroundColor: tokens.colorNeutralBackground2,
        flexGrow: 1,
        minHeight: 0,
    },
    currentPathBar: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        ...shorthands.padding('4px', '8px', '4px', '12px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke1),
        backgroundColor: tokens.colorNeutralBackground3,
        fontSize: '12px',
        color: tokens.colorNeutralForeground2,
        minHeight: '32px',
    },
    pathText: {
        fontFamily: 'Consolas, Monaco, "Courier New", monospace',
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    fileList: {
        display: 'flex',
        flexDirection: 'column',
        overflowY: 'auto',
        flexGrow: 1,
    },
    fileRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        ...shorthands.padding('6px', '12px'),
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    fileRowSelected: {
        backgroundColor: tokens.colorNeutralBackground3Selected,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Selected,
        },
    },
    folderRow: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'space-between',
        ...shorthands.padding('6px', '12px'),
        cursor: 'pointer',
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    backRow: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.padding('6px', '12px'),
        cursor: 'pointer',
        color: tokens.colorNeutralForeground2,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground3Hover,
        },
    },
    fileNameCell: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.gap('8px'),
        overflow: 'hidden',
        flexGrow: 1,
    },
    fileName: {
        overflow: 'hidden',
        textOverflow: 'ellipsis',
        whiteSpace: 'nowrap',
    },
    deleteButton: {
        opacity: 0,
        '.fileRow:hover &, .folderRow:hover &': {
            opacity: 1,
        },
    },
    deleteButtonVisible: {
        opacity: 1,
    },
    dropZoneContainer: {
        ...shorthands.padding('8px'),
        ...shorthands.borderTop('1px', 'solid', tokens.colorNeutralStroke1),
    },
    dropZone: {
        ...shorthands.border('2px', 'dashed', tokens.colorNeutralStroke1),
        ...shorthands.borderRadius('4px'),
        ...shorthands.padding('12px', '8px'),
        textAlign: 'center',
        backgroundColor: tokens.colorNeutralBackground1,
        cursor: 'pointer',
        transitionProperty: 'border-color, background-color',
        transitionDuration: '200ms',
        fontSize: '12px',
        ':hover': {
            ...shorthands.borderColor(tokens.colorBrandStroke1),
            backgroundColor: tokens.colorNeutralBackground3,
        },
    },
    dropZoneDragOver: {
        ...shorthands.borderColor(tokens.colorBrandStroke1),
        backgroundColor: tokens.colorBrandBackground2,
    },
    hiddenFileInput: {
        display: 'none',
    },
    defaultFileBadge: {
        marginLeft: '8px',
        fontSize: '10px',
        color: tokens.colorNeutralForeground3,
    },
    newFolderPopover: {
        ...shorthands.padding('8px'),
    },
    newFolderForm: {
        display: 'flex',
        flexDirection: 'row',
        ...shorthands.gap('8px'),
        alignItems: 'center',
    },
    pathBarActions: {
        display: 'flex',
        flexDirection: 'row',
        ...shorthands.gap('4px'),
        alignItems: 'center',
    },
});
