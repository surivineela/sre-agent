import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useSkillFileEditorStyles = makeStyles({
    container: {
        display: 'flex',
        flexDirection: 'column',
        flexGrow: 1,
        minWidth: 0,
        ...shorthands.border('1px', 'solid', tokens.colorNeutralStroke1),
        ...shorthands.borderRadius('4px'),
        overflow: 'hidden',
    },
    header: {
        display: 'flex',
        alignItems: 'center',
        ...shorthands.padding('4px', '12px'),
        ...shorthands.borderBottom('1px', 'solid', tokens.colorNeutralStroke1),
        backgroundColor: tokens.colorNeutralBackground3,
        fontSize: '12px',
        fontWeight: tokens.fontWeightSemibold,
        minHeight: '32px',
    },
    editorContainer: {
        flexGrow: 1,
        overflow: 'hidden',
    },
    noFileSelected: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexGrow: 1,
        color: tokens.colorNeutralForeground3,
        fontSize: '14px',
    },
});
