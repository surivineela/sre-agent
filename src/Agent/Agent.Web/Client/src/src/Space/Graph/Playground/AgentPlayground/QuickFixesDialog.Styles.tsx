import { makeStyles, shorthands, tokens } from '@fluentui/react-components';

export const useQuickFixesDialogStyles = makeStyles({
    dialogSurface: {
        display: 'flex',
        flexDirection: 'column',
        height: '90vh',
        maxHeight: 'unset',
        maxWidth: '90vw',
        padding: '0px 8px 0px 8px',
    },
    dialogContentRow: {
        display: 'flex',
        flexDirection: 'row',
        flex: '1 1 auto',
        minWidth: 0,
        gap: '8px',
        overflowY: 'hidden',
    },
    findingsListPanel: {
        display: 'flex',
        flexDirection: 'column',
        flex: '1 1 auto',
        gap: '16px',
        minHeight: 0,
        padding: '20px 0px 8px 0px',
    },
    findingsListPanelExpanded: {
        width: '30%',
    },
    findingsListPanelCollapsed: {
        width: '100%',
    },
    panelHeader: {
        display: 'flex',
        justifyContent: 'center',
        gap: '8px',
    },
    panelTitle: {
        margin: '0px',
    },
    findingsList: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        flex: '1 1 auto',
        overflow: 'auto',
        padding: '2px',
    },
    panelFooter: {
        flex: 'none',
        display: 'flex',
        marginTop: 'auto',
        padding: '0px',
        gap: '8px',
    },
    cancelButton: {
        marginLeft: 'auto',
    },
    listItemBorderRadius: {
        borderRadius: '8px',
    },
    findingContent: {
        display: 'flex',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    findingTitleTruncated: {
        whiteSpace: 'nowrap',
        overflowX: 'hidden',
        textOverflow: 'ellipsis',
    },
    expanderCell: {
        marginLeft: 'auto',
        height: '100%',
    },
    descriptionContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    messageBarTransparent: {
        whiteSpace: 'normal',
        backgroundColor: 'transparent',
        borderRadius: '8px',
    },
    diffPreviewPanelCollapsed: {
        display: 'flex',
        alignItems: 'flex-start',
        padding: '20px 0px 8px 0px',
        flex: 'none',
    },
    diffPreviewPanelExpanded: {
        display: 'flex',
        flexDirection: 'column',
        flex: '1 1 auto',
        gap: '16px',
        minHeight: 0,
        width: '70%',
        padding: '20px 0px 8px 0px',
    },
    diffPreviewHeader: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    diffPreviewTitle: {
        margin: 'auto',
    },
    flexNone: {
        flex: 'none',
    },
    listItemRoot: {
        position: 'relative',
        alignItems: 'center',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        gridTemplateAreas: `
      "finding expander"
      "description description"
    `,
        gridTemplateColumns: '1fr auto',
    },
    listItem: {
        display: 'grid',
        padding: '8px',
    },
    finding: { gridArea: 'finding', overflow: 'hidden' },
    expander: { gridArea: 'expander' },
    description: { gridArea: 'description' },

    watcherFindingItemSelected: {
        backgroundColor: tokens.colorBrandBackground2,
        ...shorthands.borderColor(tokens.colorBrandStroke1),
        boxShadow: `0 0 0 1px ${tokens.colorBrandStroke1}`,
    },
    watcherFindingTitle: {
        fontSize: tokens.fontSizeBase300,
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
    },
    watcherFindingRationale: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
    },
    watcherHint: {
        backgroundColor: tokens.colorNeutralBackground3,
        borderRadius: tokens.borderRadiusSmall,
        ...shorthands.padding(tokens.spacingVerticalXS, tokens.spacingHorizontalS),
        color: tokens.colorNeutralForeground2,
    },
});
