import { makeStyles, tokens } from '@fluentui/react-components';

export const useScheduledTasksStyles = makeStyles({
    root: {
        display: 'flex',
        flexDirection: 'column',
        margin: '16px 20px 5px 20px',
        borderRadius: tokens.borderRadiusXLarge,
        boxShadow: tokens.shadow4,
        backgroundColor: tokens.colorNeutralBackground1,
        height: 'calc(100vh - 67px)',
        overflow: 'hidden',
        position: 'relative',
        flex: 1,
    },
    content: {
        display: 'flex',
        flexDirection: 'column',
        overflowY: 'auto',
        position: 'relative',
        flex: 1,
    },
    padding: {
        padding: '16px',
        height: 'calc(100% - 32px)',
    },
    title: { display: 'flex', flexDirection: 'column', gap: '8px', marginBottom: '16px' },
    toolbar: {
        display: 'flex',
        justifyContent: 'space-between',
        gap: '5px',
        padding: '20px',
        paddingLeft: '0px',
    },
    toolbarButtons: {
        display: 'flex',
        gap: '12px',
    },
    toolbarButton: {
        fontWeight: 'normal',
        padding: '2px 8px',
    },
    filters: {
        display: 'flex',
        gap: '10px',
        alignItems: 'center',
    },
    menuItems: {
        display: 'flex',
        gap: '6px',
        alignItems: 'center',
    },
    taskForm: {
        display: 'flex',
        gap: '24px',
        padding: '12px 0',
        justifyContent: 'space-around',
    },
    taskFormDivider: {
        backgroundColor: '#D1D1D1',
        width: '1px',
        height: '328px',
    },
    taskFormLeft: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingHorizontalXL,
        maxWidth: '460px',
    },
    taskFormRight: {
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'stretch',
        gap: '24px',
    },
    taskFormTimeFields: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(84px, 1fr))',
        gap: '12px',
        maxWidth: '460px',
        '& .fui-Dropdown': {
            minWidth: '0 !important',
            width: '100%',
        },
    },
    taskFormDateFields: {
        display: 'grid',
        gridTemplateColumns: '1fr 1fr',
        gap: '12px',
    },
    fieldActionGroup: {
        display: 'inline-flex',
        alignItems: 'center',
    },
    fieldLabelRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        width: '100%',
        gap: tokens.spacingHorizontalM,
    },
    fieldRequiredStar: {
        color: tokens.colorPaletteRedForeground1,
        fontWeight: tokens.fontWeightRegular,
        lineHeight: 1,
    },
    promptImprovementButton: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        alignItems: 'center',
        fontSize: tokens.fontSizeBase200,
    },
});
