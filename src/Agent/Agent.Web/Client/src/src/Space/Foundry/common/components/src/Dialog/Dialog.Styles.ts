import { makeStyles, tokens } from '@fluentui/react-components';

export const useDialogStyles = makeStyles({
    dialogSurface: {
        width: '100%',
        maxWidth: '100%',
        padding: '0px',
        border: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralStroke3}`,
        backgroundColor: tokens.colorNeutralBackground2,
    },
    dialogSurfaceNano: {
        width: '24%',
        maxWidth: '24%',
        '@media (width < 1312px)': {
            width: '28%',
            maxWidth: '28%',
        },
        '@media (width < 1056px)': {
            width: '36%',
            maxWidth: '36%',
        },
        '@media (width < 672px)': {
            width: '90%',
            maxWidth: '90%',
        },
    },

    dialogSurfaceExtraSmall: {
        width: '30%',
        maxWidth: '30%',
        '@media (width < 1312px)': {
            width: '35%',
            maxWidth: '35%',
        },
        '@media (width < 1056px)': {
            width: '48%',
            maxWidth: '48%',
        },
        '@media (width < 672px)': {
            width: '95%',
            maxWidth: '95%',
        },
    },

    dialogSurfaceSmall: {
        width: '36%',
        maxWidth: '36%',
        '@media (width < 1312px)': {
            width: '42%',
            maxWidth: '42%',
        },
        '@media (width < 1056px)': {
            width: '60%',
            maxWidth: '60%',
        },
        '@media (width < 672px)': {
            width: '100%',
            maxWidth: '100%',
        },
    },

    dialogSurfaceMedium: {
        width: '48%',
        maxWidth: '48%',
        '@media (width < 1312px)': {
            width: '60%',
            maxWidth: '60%',
        },
        '@media (width < 1056px)': {
            width: '84%',
            maxWidth: '84%',
        },
        '@media (width < 672px)': {
            width: '100%',
            maxWidth: '100%',
        },
    },

    dialogSurfaceLarge: {
        width: '85%',
        maxWidth: '1400px',
        '@media (width < 1312px)': {
            width: '90%',
            maxWidth: '90%',
        },
        '@media (width < 1056px)': {
            width: '96%',
            maxWidth: '96%',
        },
        '@media (width < 672px)': {
            width: '100%',
            maxWidth: '100%',
        },
    },
    dialogTitle: {},
    dialogContent: {
        color: tokens.colorNeutralForeground2,
        overflowY: 'hidden',
        height: '100%',
        display: 'flex',
        flexDirection: 'column',
        boxSizing: 'unset',
        padding: '0px',
        margin: '0px',
    },
    divider: {
        gridArea: '2 / 1 / 2 / 4',
        alignSelf: 'end',
        width: `calc(100% + ${tokens.spacingVerticalXXXL} * 2)`,
        marginRight: `calc(-1 * ${tokens.spacingVerticalXXXL})`,
        marginLeft: `calc(-1 * ${tokens.spacingVerticalXXXL})`,
        borderBottom: `${tokens.strokeWidthThin} solid ${tokens.colorNeutralBackground4}`,
    },

    dialogActions: {
        flexWrap: 'wrap',
        justifyContent: 'end',
        marginTop: tokens.spacingHorizontalS,
    },

    titleWithActions: {
        display: 'flex',
        gap: tokens.spacingHorizontalS,
        alignItems: 'center',
        width: '100%',
        paddingRight: tokens.spacingHorizontalXXL,
        paddingLeft: tokens.spacingHorizontalXXL,
    },
    titleDivider: {
        minHeight: '32px',
    },
    closeButtonAndDivider: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: tokens.spacingHorizontalL,
        marginLeft: 'auto',
    },
});
