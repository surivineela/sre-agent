import { makeStyles, tokens } from '@fluentui/react-components';

export const useResizeHandleStyles = makeStyles({
    resizeHandle: {
        position: 'absolute',
        top: 0,
        right: `calc(-1 * ${tokens.spacingHorizontalL})`,
        width: tokens.spacingHorizontalL,
        height: '100%',
        borderLeft: `${tokens.strokeWidthThickest} solid transparent`,
        transition: 'background-color 0.1s ease',
        '&:hover': {
            cursor: 'col-resize',
            borderLeftColor: tokens.colorBrandBackground,
        },
        '&.dragging': {
            cursor: 'col-resize',
            borderLeftColor: tokens.colorBrandBackground,
        },

        '&.disabled': {
            display: 'none',
        },
    },
});
