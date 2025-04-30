import { IButtonStyles } from '@fluentui/react/lib/Button';
import { IDialogContentStyleProps, IDialogContentStyles, IDialogFooterStyleProps, IDialogFooterStyles } from '@fluentui/react/lib/Dialog';
import { IModalStyleProps, IModalStyles } from '@fluentui/react/lib/Modal';
import { getTheme } from '@fluentui/react/lib/Styling';
import { IStyleFunctionOrObject } from '@fluentui/react/lib/Utilities';

export const modalStyles: IStyleFunctionOrObject<IModalStyleProps, IModalStyles> = {
    main: {
        position: 'absolute',
        top: '0px',
        minWidth: '100% !important',
        boxShadow: '0px 0px 5px 0px rgba(0, 0, 0, 0.4)',
    },
};

export const modalContentStyles: IStyleFunctionOrObject<IDialogContentStyleProps, IDialogContentStyles> = {
    inner: { paddingBottom: '0px' },
};

export const modalFooterStyles: IStyleFunctionOrObject<IDialogFooterStyleProps, IDialogFooterStyles> = {
    actionsRight: {
        paddingTop: '10px',
        marginTop: '30px',
        marginBottom: '26px',
        justifyContent: 'flex-start',
        textAlign: 'left',
    },
};

export const deleteAppSpaceFooterStyles: IStyleFunctionOrObject<IDialogFooterStyleProps, IDialogFooterStyles> = {
    actionsRight: {
        paddingTop: '10px',
        marginTop: '30px',
        justifyContent: 'flex-start',
        textAlign: 'left',
    },
};

export const buttonStyles: IButtonStyles = {
    root: {
        borderColor: getTheme().semanticColors.buttonBackgroundCheckedHovered,
        borderStyle: 'solid',
        borderWidth: '1px',
        marginLeft: '0px',
    },
};

export const leftButtonDivStyles = {
    marginRight: '8px',
    display: 'inline-block',
};

export const buttonDivStyles = {
    marginRight: '8px',
    display: 'inline-block',
};
