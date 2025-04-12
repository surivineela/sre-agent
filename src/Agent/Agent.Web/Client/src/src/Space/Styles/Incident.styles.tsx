import {
    tokens,
    makeStyles,
  } from '@fluentui/react-components';  
  
export const useIncidentStatusBarStyles = makeStyles({
    container: {
        display: 'flex',
        alignItems: 'center',
        padding: '8px 12px',
        paddingLeft: 0,
        paddingTop: 0,
        gap: '10px',
        borderBottom: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    statusGroup: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    error: { color: tokens.colorPaletteRedForeground1 },
    warning: { color: '#DB7500' },
    success: { color: tokens.colorPaletteLightGreenForeground1 },
    dropdown: { backgroundColor: tokens.colorNeutralBackground3, border: 'none', boxShadow: 'none', maxWidth: 'fit-content', minWidth: '30px' },
});