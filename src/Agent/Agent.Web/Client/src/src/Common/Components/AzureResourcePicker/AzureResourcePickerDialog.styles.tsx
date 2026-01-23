import { makeStyles, tokens } from '@fluentui/react-components';

export const useAzureResourcePickerDialogStyles = makeStyles({
    dialogSurface: {
        width: '875px',
        maxWidth: '90vw',
        maxHeight: '80vh',
        height: '650px',
        display: 'flex',
        flexDirection: 'column',
    },
    dialogBody: {
        display: 'flex',
        flexDirection: 'column',
        height: '100%',
        gap: '0',
    },
    dialogTitle: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
    },
    dialogContent: {
        flex: 1,
        overflow: 'hidden',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    headerRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
    toggleSection: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    infoMessageBar: {
        marginBottom: tokens.spacingVerticalS,
    },
    gridContainer: {
        flex: 1,
        overflowY: 'auto',
        overflowX: 'auto',
        minHeight: 0,
    },
    dataGrid: {
        width: '100%',
        tableLayout: 'auto',
    },
    dataGridHeader: {
        fontWeight: 600,
        position: 'sticky',
        top: 0,
        backgroundColor: tokens.colorNeutralBackground1,
        zIndex: 1,
    },
    disabledSectionHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        paddingTop: tokens.spacingVerticalS,
        paddingBottom: tokens.spacingVerticalS,
        paddingLeft: tokens.spacingHorizontalM,
        paddingRight: tokens.spacingHorizontalM,
        backgroundColor: tokens.colorNeutralBackground3,
        color: tokens.colorNeutralForeground2,
        fontSize: tokens.fontSizeBase200,
        marginTop: tokens.spacingVerticalM,
        borderRadius: tokens.borderRadiusMedium,
    },
    disabledRow: {
        opacity: 0.5,
        cursor: 'not-allowed',
    },
    nameCell: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    externalLinkIcon: {
        color: tokens.colorNeutralForeground3,
        cursor: 'pointer',
        flexShrink: 0,
        '&:hover': {
            color: tokens.colorBrandForeground1,
        },
    },
    recommendedIcon: {
        color: tokens.colorBrandForeground1,
        flexShrink: 0,
        width: '16px',
        height: '16px',
    },
    infoIcon: {
        cursor: 'pointer',
        color: tokens.colorNeutralForeground3,
    },
    selectedCountText: {
        fontSize: tokens.fontSizeBase200,
        color: tokens.colorNeutralForeground2,
        paddingTop: tokens.spacingVerticalS,
    },
    dialogActions: {
        paddingTop: tokens.spacingVerticalM,
        justifyContent: 'flex-end',
    },
});
