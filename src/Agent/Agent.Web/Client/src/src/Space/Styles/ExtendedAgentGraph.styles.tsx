import { GriffelStyle, makeStyles, tokens } from '@fluentui/react-components';
import { ExtendedAgentNodeSize } from '../Contracts/ExtendedAgentGraph';

const containerCommonStyles: GriffelStyle = {
    flex: '1 1 auto',
    backgroundColor: tokens.colorNeutralBackground1,
    borderTopLeftRadius: tokens.borderRadiusXLarge,
    boxShadow: tokens.shadow4,
    display: 'flex',
    flexDirection: 'column',
    overflow: 'hidden',
    minHeight: 0,
};

const menuItemWithIcon: GriffelStyle = {
    gap: '12px',
    alignItems: 'center',
    '& .fui-MenuItem__icon': {
        height: 'unset',
        width: 'unset',
    },
};

export const useExtendedAgentGraphStyles = makeStyles({
    visualRoot: {
        display: 'flex',
        flexDirection: 'row',
        justifyContent: 'flex-start',
        alignItems: 'flex-start',
        width: '100%',
        height: '100%',
        minHeight: 0,
        overflow: 'hidden',
        position: 'relative',
    },
    reactFlow: {
        width: '100%',
        height: '100%',
        minHeight: 0,
        position: 'relative',
    },
    spinner: {
        position: 'fixed',
        left: '50%',
        top: '50%',
        transform: 'translate(-50%, -50%)',
    },
    container: {
        ...containerCommonStyles,
    },
    gridViewContainer: {
        ...containerCommonStyles,
        paddingBottom: '16px',
        paddingLeft: '16px',
    },
    rootContainer: {
        display: 'flex',
        flexDirection: 'column',
        height: 'calc(100vh - 45px - 16px)',
        paddingTop: '16px',
        borderTop: '1px solid rgba(204, 204, 204, 0.8)',
        backgroundColor: tokens.colorNeutralBackground3,
        paddingLeft: '15px',
        gap: '16px',
    },
    toolbarWrapper: {
        display: 'flex',
        alignItems: 'center',
        gap: '16px',
    },
    statusMessageContainer: {
        paddingRight: '16px',
        marginBottom: tokens.spacingVerticalS,
    },
    selectorOverlay: {
        position: 'absolute',
        top: 0,
        left: 0,
        right: 0,
        display: 'flex',
        justifyContent: 'flex-start',
        pointerEvents: 'none',
        boxSizing: 'border-box',
        zIndex: 10,
    },
    infoPanelContainer: {
        position: 'relative',
        marginLeft: '0',
        pointerEvents: 'auto',
        zIndex: 5,
        height: '100%',
        display: 'flex',
    },
    infoPanelFloating: {
        position: 'absolute',
        top: 0,
        left: 0,
        marginLeft: 0,
        cursor: 'default',
        zIndex: 15,
        pointerEvents: 'auto',
        height: '100%',
    },
    menuItemWithIcon: menuItemWithIcon,
    contextMenuItemWithIcon: {
        ...menuItemWithIcon,
        gap: '4px',
    },
    menuIconWrapper: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '36px',
        width: '36px',
        borderRadius: '8px',
    },
    menuItemContent: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
        letterSpacing: '0%',
        verticalAlign: 'middle',
    },
});

// Agent Node Styles
export const useExtendedAgentNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    cardWrapper: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '8px',
        position: 'relative',
    },
    agentCard: {
        width: `${ExtendedAgentNodeSize.agentWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.agentHeight}px`,
        borderRadius: '16px',
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
        boxShadow: tokens.shadow16,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
    },
    header: {},
    headerText: {},
    description: {},
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
    },
    iconWrapper: {
        width: '36px',
        height: '36px',
        borderRadius: '8px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    nameText: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    badgeRow: {
        display: 'flex',
        gap: '8px',
        flexWrap: 'wrap',
        alignItems: 'center',
    },
    emptyText: {
        color: tokens.colorNeutralForeground3,
    },
    chipRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
    },
    quickActionButton: {
        height: '24px',
        width: '24px',
        minWidth: 'unset',
        boxShadow: tokens.shadow8,
        zIndex: 2,
    },
    menuPopover: {
        boxShadow: tokens.shadow16,
        borderRadius: tokens.borderRadiusMedium,
    },
    badge: {
        width: 'fit-content',
        minWidth: '24px',
        padding: '0 4px',
        borderRadius: tokens.borderRadiusCircular,
        border: tokens.colorNeutralStroke2,
        color: tokens.colorNeutralForeground3,
        zIndex: 1,
    },
});

// Tool Node Styles
export const useToolNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    toolCard: {
        width: `${ExtendedAgentNodeSize.toolWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.toolHeight}px`,
        borderRadius: '16px',
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
        boxShadow: tokens.shadow16,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
    },
    header: {},
    headerText: {},
    description: {},
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
    },
    iconWrapper: {
        width: '36px',
        height: '36px',
        borderRadius: '8px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    nameText: {
        color: tokens.colorNeutralForeground1,
        fontWeight: tokens.fontWeightSemibold,
        overflow: 'hidden',
        textOverflow: 'ellipsis',
    },
    kustoToolCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    linkToolCard: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
});

// Trigger Node Styles
export const useTriggerNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    triggerCard: {
        width: `${ExtendedAgentNodeSize.triggerWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.triggerHeight}px`,
        borderRadius: '16px',
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
        boxShadow: tokens.shadow16,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
    },
    iconWrapper: {
        width: '36px',
        height: '36px',
        borderRadius: '8px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    nameText: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        wordBreak: 'break-word',
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        wordBreak: 'break-word',
    },
    badgeRow: {
        display: 'flex',
        gap: '8px',
    },
    badge: {
        width: 'fit-content',
        minWidth: '24px',
        padding: '0 4px',
        borderRadius: tokens.borderRadiusCircular,
        border: tokens.colorNeutralStroke2,
        color: tokens.colorNeutralForeground3,
        zIndex: 1,
    },
    mutedText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        fontStyle: 'italic',
    },
    footerRow: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
        marginTop: 'auto',
    },
});

// Connector Node Styles
export const useConnectorNodeStyles = makeStyles({
    handle: {
        opacity: 0,
        pointerEvents: 'none',
    },
    connectorCard: {
        width: `${ExtendedAgentNodeSize.connectorWidth}px`,
        minHeight: `${ExtendedAgentNodeSize.connectorHeight}px`,
        borderRadius: '16px',
        cursor: 'pointer',
        transition: 'box-shadow 0.2s ease-in-out',
        border: `1px solid ${tokens.colorNeutralStroke1}`,
        padding: '20px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        position: 'relative',
        boxShadow: tokens.shadow16,
        ':hover': {
            backgroundColor: tokens.colorNeutralBackground1Hover,
        },
    },
    cardHighlighted: {
        boxShadow: tokens.shadow8,
    },
    cardHovered: {
        boxShadow: tokens.shadow16,
    },
    cardSelected: {
        border: `2px solid ${tokens.colorBrandStroke1}`,
    },
    cardContent: {
        display: 'flex',
        flexDirection: 'column',
        gap: '12px',
        flex: 1,
    },
    titleRow: {
        display: 'flex',
        gap: '12px',
        alignItems: 'center',
    },
    iconWrapper: {
        width: '36px',
        height: '36px',
        borderRadius: '8px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        flexShrink: 0,
    },
    nameBlock: {
        flex: 1,
        minWidth: 0,
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
    },
    nameText: {
        fontWeight: tokens.fontWeightSemibold,
        color: tokens.colorNeutralForeground1,
        wordBreak: 'break-word',
        fontSize: tokens.fontSizeBase300,
        lineHeight: tokens.lineHeightBase300,
    },
    subtitleText: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase200,
        lineHeight: tokens.lineHeightBase200,
        wordBreak: 'break-word',
    },
    badgeRow: {
        display: 'flex',
        gap: '8px',
    },
    badge: {
        width: 'fit-content',
        minWidth: '24px',
        padding: '0 4px',
        borderRadius: tokens.borderRadiusCircular,
        border: tokens.colorNeutralStroke2,
        color: tokens.colorNeutralForeground3,
        zIndex: 1,
    },
});

// Edge Styles
export const useExtendedAgentEdgeStyles = makeStyles({
    highlightedEdge: {
        stroke: `${tokens.colorBrandForegroundLinkHover} !important`,
        strokeWidth: '2px !important',
    },
    usesToolEdge: {
        stroke: tokens.colorPaletteBlueForeground2,
    },
    systemToolEdge: {
        stroke: tokens.colorPaletteGoldForeground2,
    },
    connectorEdge: {
        stroke: tokens.colorPaletteGreenForeground2,
    },
    agentAsToolEdge: {
        stroke: tokens.colorPalettePurpleForeground2,
    },
    handoffEdge: {
        stroke: tokens.colorPaletteDarkOrangeForeground2,
    },
});

// Selector Panel Styles
export const useExtendedAgentSelectorStyles = makeStyles({
    overlayCard: {
        pointerEvents: 'auto',
        backgroundColor: tokens.colorNeutralBackground1,
        borderRadius: tokens.borderRadiusLarge,
        boxShadow: tokens.shadow16,
        padding: '16px',
        minWidth: 'min(720px, 100%)',
        maxWidth: 'min(960px, 100%)',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    root: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
    },
    inputsRow: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
        alignItems: 'flex-end',
    },
    field: {
        flex: 1,
        minWidth: '220px',
    },
    option: {
        display: 'flex',
        flexDirection: 'column',
        gap: '2px',
        alignItems: 'flex-start',
        textAlign: 'left',
    },
    optionText: {
        wordBreak: 'break-word',
    },
    optionSubtext: {
        color: tokens.colorNeutralForeground3,
        wordBreak: 'break-word',
    },
    searchBox: {
        width: '100%',
    },
    actionColumn: {
        display: 'flex',
        alignItems: 'flex-end',
        paddingBottom: tokens.spacingVerticalXXS,
    },
    statsRow: {
        display: 'flex',
        alignItems: 'center',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalM,
    },
    statsItem: {
        color: tokens.colorNeutralForeground2,
    },
    badgeGroup: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    emptyNotice: {
        color: tokens.colorNeutralForeground3,
        paddingInlineStart: tokens.spacingHorizontalXS,
    },
});

// Info Panel Styles
export const useExtendedAgentInfoStyles = makeStyles({
    root: {
        height: '100%',
        backgroundColor: tokens.colorNeutralBackground1,
        display: 'flex',
        flexDirection: 'row',
        overflow: 'hidden',
        boxSizing: 'border-box',
        minWidth: '280px',
        boxShadow: tokens.shadow4,
    },
    panel: {
        flex: 1,
        display: 'flex',
        flexDirection: 'column',
        overflow: 'hidden',
    },
    resizeHandle: {
        width: '12px',
        cursor: 'col-resize',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '0 2px',
        touchAction: 'none',
        backgroundColor: 'transparent',
        transition: 'background-color 0.2s ease',
    },
    resizeHandleHovered: {
        backgroundColor: tokens.colorNeutralBackground3,
    },
    resizeHandleGrip: {
        width: '2px',
        height: '60%',
        borderRadius: tokens.borderRadiusCircular,
        backgroundColor: 'transparent',
        transition: 'background-color 0.2s ease',
    },
    resizeHandleGripVisible: {
        backgroundColor: tokens.colorNeutralStroke2,
    },
    header: {
        padding: '16px',
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'flex-start',
        gap: tokens.spacingHorizontalM,
    },
    headerInfo: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        cursor: 'grab',
        userSelect: 'none',
    },
    tabList: {
        marginTop: tokens.spacingVerticalS,
    },
    content: {
        flex: 1,
        overflowY: 'auto',
        padding: '0 16px 16px 16px',
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
    },
    tabPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalM,
        flex: 1,
    },
    section: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalS,
        marginBottom: tokens.spacingVerticalL,
    },
    sectionTitle: {
        fontWeight: tokens.fontWeightSemibold,
        fontSize: tokens.fontSizeBase400,
        marginBottom: tokens.spacingVerticalXS,
    },
    subSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
    },
    handoffSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
        paddingTop: '10px',
        paddingBottom: '10px',
    },
    instructionsSection: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        marginTop: tokens.spacingVerticalS,
        paddingTop: '10px',
        paddingBottom: '10px',
    },
    subtitle: {
        color: tokens.colorNeutralForeground2,
    },
    actionButton: {
        alignSelf: 'flex-start',
        marginTop: tokens.spacingVerticalS,
    },
    instructions: {
        whiteSpace: 'pre-wrap',
        wordBreak: 'break-word',
        color: tokens.colorNeutralForeground3,
    },
    list: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    pillGroup: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalS,
    },
    pillTag: {
        fontWeight: tokens.fontWeightSemibold,
        backgroundColor: tokens.colorNeutralBackground1,
    },
    listItem: {
        padding: '8px 12px',
        backgroundColor: tokens.colorNeutralBackground2,
        borderRadius: tokens.borderRadiusSmall,
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
    },
    listItemHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    listItemBadges: {
        display: 'flex',
        gap: tokens.spacingHorizontalXS,
        flexWrap: 'wrap',
    },
    statusBadge: {
        fontWeight: tokens.fontWeightSemibold,
    },
    badgeRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        flexWrap: 'wrap',
    },
    neutralBadge: {
        color: tokens.colorNeutralForeground3,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
    },
    tableCellTruncate: {
        maxWidth: '0',
        overflow: 'hidden',
        whiteSpace: 'nowrap',
        position: 'relative',
        '&::after': {
            content: '""',
            position: 'absolute',
            top: '0',
            right: '0',
            width: '20px',
            height: '100%',
            background: `linear-gradient(to right, transparent, ${tokens.colorNeutralBackground1})`,
            pointerEvents: 'none',
        },
    },
    metadataRow: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        color: tokens.colorNeutralForeground2,
        wordBreak: 'break-word',
        minWidth: '0',
        width: '100%',
        '& > *:first-child': {
            minWidth: '150px',
            flexShrink: 0,
        },
        '& > *:last-child': {
            flex: '1 1 auto',
            minWidth: '0',
            paddingLeft: '50px',
        },
    },
    metadataKey: {
        color: tokens.colorNeutralForeground3,
        fontSize: tokens.fontSizeBase300,
    },
    emptyState: {
        color: tokens.colorNeutralForeground3,
    },
    summary: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    toolGrid: {
        display: 'grid',
        gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))',
        gap: tokens.spacingHorizontalS,
    },
    toolCard: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXXS,
        padding: '14px 16px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        backgroundColor: tokens.colorNeutralBackground2,
        boxShadow: tokens.shadow4,
        transition: 'transform 0.2s ease, box-shadow 0.2s ease',
        ':hover': {
            transform: 'translateY(-2px)',
            boxShadow: tokens.shadow16,
        },
    },
    toolCardHeader: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalS,
    },
    toolCardDescription: {
        color: tokens.colorNeutralForeground3,
    },
    toolCardMeta: {
        display: 'flex',
        flexWrap: 'wrap',
        gap: tokens.spacingHorizontalXS,
        marginTop: tokens.spacingVerticalXXS,
    },
    toolTag: {
        fontWeight: tokens.fontWeightSemibold,
    },
    accordion: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
    },
    accordionPanel: {
        display: 'flex',
        flexDirection: 'column',
        gap: tokens.spacingVerticalXS,
        paddingTop: tokens.spacingVerticalS,
    },
    yamlEditorContainer: {
        flex: 1,
        minHeight: '360px',
        borderRadius: tokens.borderRadiusMedium,
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        overflow: 'hidden',
    },
    yamlActions: {
        display: 'flex',
        justifyContent: 'space-between',
        alignItems: 'center',
        gap: tokens.spacingHorizontalM,
    },
    yamlButtons: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
    },
    yamlButton: {
        alignSelf: 'flex-start',
    },
    yamlDescription: {
        color: tokens.colorNeutralForeground3,
    },
    textArea: {
        width: '100%',
        minHeight: '200px',
        padding: '8px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusSmall,
        color: tokens.colorNeutralForeground3,
        fontFamily: 'inherit',
        fontSize: tokens.fontSizeBase300,
        resize: 'vertical',
        outline: 'none',
        boxSizing: 'border-box',
    },
    textAreaSmall: {
        width: '100%',
        minHeight: '100px',
        padding: '8px',
        border: `1px solid ${tokens.colorNeutralStroke2}`,
        borderRadius: tokens.borderRadiusSmall,
        color: tokens.colorNeutralForeground3,
        fontFamily: 'inherit',
        fontSize: tokens.fontSizeBase300,
        boxSizing: 'border-box',
        resize: 'vertical',
        outline: 'none',
    },
    subText: {
        color: tokens.colorNeutralForeground3,
    },
    flexRowCenter: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
    },
    flexRowCenter8: {
        display: 'flex',
        alignItems: 'center',
        gap: '8px',
    },
    flexRowCenter4: {
        display: 'flex',
        alignItems: 'center',
        gap: '4px',
    },
    flexRow12: {
        display: 'flex',
        alignItems: 'center',
        gap: '12px',
    },
    flexColumnGap4: {
        display: 'flex',
        flexDirection: 'column',
        gap: '4px',
        minWidth: 0,
    },
    flexColumnGap8: {
        display: 'flex',
        flexDirection: 'column',
        gap: '8px',
        marginTop: '8px',
    },
    flexShrinkNone: {
        flexShrink: 0,
    },
    paddingVertical10: {
        paddingTop: '10px',
        paddingBottom: '10px',
    },
    paddingBottom10: {
        paddingBottom: '10px',
    },
    marginBottom8: {
        marginBottom: '8px',
    },
    marginBottom10: {
        marginBottom: '10px',
    },
    marginTopLeft: {
        marginTop: '8px',
        marginLeft: '8px',
    },
    smallIcon: {
        width: '16px',
        height: '16px',
    },
    successIcon: {
        color: tokens.colorPaletteGreenForeground1,
    },
    errorIcon: {
        color: tokens.colorPaletteRedForeground1,
    },
    knowledgeBaseLink: {
        display: 'flex',
        alignItems: 'center',
        gap: '6px',
        cursor: 'pointer',
        fontSize: '12px',
        color: '#0078D4',
        textDecoration: 'underline',
    },
});
