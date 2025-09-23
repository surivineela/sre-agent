import { makeStyles, tokens } from '@fluentui/react-components';

export const useDialogStyles = makeStyles({
    dialogSurface: {
        '& .fui-DialogSurface__backdrop': {
            backgroundColor: tokens.colorBackgroundOverlay,
        },
    },
});
