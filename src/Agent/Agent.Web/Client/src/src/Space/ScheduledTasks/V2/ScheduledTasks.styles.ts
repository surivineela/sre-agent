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
    menuItems: {
        display: 'flex',
        gap: '6px',
        alignItems: 'center',
    },
});
