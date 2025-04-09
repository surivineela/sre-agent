import { getTheme, IStyle, mergeStyleSets } from '@fluentui/react/lib/Styling';
import { useTheme } from '@fluentui/react/lib/Theme';
import { IButtonStyles } from '@fluentui/react/lib/Button';
import { CSSProperties } from 'react';
import { makeStyles, tokens } from '@fluentui/react-components';

export const activitiesStylesRoot: CSSProperties = {
  display: 'flex',
  justifyContent: 'flex-start',
  alignItems: 'flex-start',
  overflow: 'hidden',
  borderTop: '1px solid rgba(204,204,204,.8)',
  backgroundColor: tokens.colorNeutralBackground3,
};

export const ThreadContentStyles = mergeStyleSets({
  root: {
    flex: '1 1 auto',
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'flex-start',
    alignItems: 'stretch',
    paddingLeft: '20px',
    overflowY: 'hidden',
    fontSize: '16px',
    lineHeight: '22px',
  },
  title: {
    fontWeight: 600,
    lineHeight: '22px',
    paddingBottom: '-10px',
    fontSize: '15px',
  },
});

export const useChatBoxStyles = makeStyles({
  userBubble: {
    backgroundColor: tokens.colorBrandBackground2, 
    color: tokens.colorNeutralForeground1,
    borderRadius: tokens.borderRadiusXLarge,
    padding: "8px 12px",
    maxWidth: "fit-content", 
    display: "inline-block", 
  },
});

export const ChatBoxStyles = mergeStyleSets({
  chatBox: {
   padding: '0px',
   paddingRight: '10px',
  },
  root: {
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'space-between',
    alignItems: 'stretch',
    height: 'calc(100vh - 114.023px)',
    padding: '20px',
    fontSize: '16px',
    backgroundColor: tokens.colorNeutralForegroundInverted,
    borderRadius: tokens.borderRadiusXLarge,
  },
  chatContainer: {
    overflowX: 'hidden',
    overflowY: 'auto',
    paddingTop: '10px',
    paddingRight: '5px',
    borderRadius: tokens.borderRadiusLarge,
  },
  chat: {
    margin: 'auto',
  },
  userMessage: {
    alignSelf: 'flex-end',
    wordBreak: 'normal',
    overflowWrap: 'anywhere',
    whiteSpace: 'normal',
  },
  agentMessage: {
    '.fai-CopilotMessage__content': {
      width: '90%',
    },
  },
});

export const ActionsStyles = mergeStyleSets({
  root: {
    height: 'calc(100vh - 114.023px)',
    overflowX: 'hidden',
    overflowY: 'auto',
  },
  status: {
    display: 'flex',
    justifyContent: 'flex-start',
    alignItems: 'center',
    gap: '8px',
  },
  statusIcon: {
    width: '16px',
    height: '16px',
    paddingTop: '2px',
    paddingRight: '2px',
  },
});

export const useChatInputStyles = () => {
  const colors = getTheme().semanticColors;

  return {
    root: {
      flex: '0 0 auto',
      marginTop: '20px',
    },
    textFieldContainer: {
      borderColor: colors.buttonBackgroundCheckedHovered,
      borderStyle: 'solid',
      borderRadius: 8,
      backgroundColor: colors.inputBackground,
      maxWidth: '1000px',
      margin: 'auto',
    },
    textField: {
      fieldGroup: {
        backgroundColor: colors.inputBackground,
      },
      field: {
        maxHeight: '60px',
        minHeight: '25px',
        overflowX: 'hidden',
        overflowY: 'auto',
        backgroundColor: colors.inputBackground,
        '::placeholder, :-ms-input-placeholder, ::-ms-input-placeholder': {
          color: colors.inputPlaceholderText,
          opacity: 1, // Firefox adds a lower opacity to the placeholder, so we use opacity: 1 to fix this.,
        },
      },
    },
    footer: {
      display: 'flex',
      justifyContent: 'flex-end',
    },
  };
};

export const useThreadMenuStyle = () => {
  const root: IStyle = {
    flex: '0 0 220px',
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'flex-start',
    alignItems: 'stretch',
    gap: '10px',
    backgroundColor: tokens.colorNeutralBackground3,
    paddingTop: '42px',
    paddingLeft: '20px',
  };

  const threadList: IStyle = {
    maxWidth: '500px',
    height: 'calc(100vh - 150px)',
    overflowX: 'hidden',
    overflowY: 'auto',
  };

  const searchBox: IStyle = {
    margin: '0px 10px',
    borderRadius: tokens.borderRadiusLarge,
    width: '280px',
  };

  const threadItem: IStyle = {
    padding: '10px 0px',
    paddingLeft: '10px',
    cursor: 'pointer',
  };

  const activeThreadItem: IStyle = {
    borderLeftStyle: 'solid',
    borderLeftWidth: '2px',
    borderLeftHeight: '4px',
    borderLeftColor: tokens.colorNeutralForeground2BrandSelected,
    boxSizing: 'border-box',
    backgroundColor: tokens.colorNeutralBackground3Selected,
    borderRadius: tokens.borderRadiusLarge, 
  };

  return mergeStyleSets({
    root,
    threadList,
    searchBox,
    threadItem,
    activeThreadItem,
  });
};

export const useCommandButtonStyles = (): IButtonStyles => {
  const colors = useTheme().semanticColors;

  return {
    root: {
      backgroundColor: colors.buttonBackgroundHovered,
    },
    rootHovered: {
      backgroundColor: colors.buttonBackgroundChecked,
    },
    rootChecked: {
      backgroundColor: colors.buttonBackgroundChecked,
    },
  };
};


export const useThreadActionsStyle = () => {
  const root: IStyle = {
    flex: '0 0 220px',
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'flex-start',
    alignItems: 'stretch',
    gap: '10px',
    backgroundColor: tokens.colorNeutralBackground3,
    paddingTop: '42px',
    paddingLeft: '5px',
    paddingRight: '10px',
  };

  const actionsList: IStyle = {
    maxWidth: '500px',
    height: 'calc(100vh - 150px)',
    overflowX: 'hidden',
    overflowY: 'auto',
  };

  const searchBox: IStyle = {
    margin: '0px 10px',
    borderRadius: tokens.borderRadiusLarge,
    width: '100%', 
    marginLeft: '-2px',
    minWidth: '275px',
  };

  return mergeStyleSets({
    root,
    actionsList,
    searchBox,
  });
};

export const useThreadActionsStyles = makeStyles({
  root: {
    flex: '0 0 220px',
    display: 'flex',
    flexDirection: 'column',
    justifyContent: 'flex-start',
    alignItems: 'stretch',
    gap: '10px',
    backgroundColor: tokens.colorNeutralBackground3,
    paddingTop: '42px',
    paddingLeft: '5px',
    paddingRight: '10px',
  },
  actionsList: {
    maxWidth: '500px',
    height: 'calc(100vh - 150px)',
    overflowX: 'hidden',
    overflowY: 'auto',
    display: 'flex',
    flexDirection: 'column',
    gap: '10px',
  },
  searchBox: {
    margin: '0px 10px',
    borderRadius: tokens.borderRadiusLarge,
    width: '100%', 
    marginLeft: '-2px',
  },
  title: {
    lineHeight: '22px',
    marginLeft: '5px',
    paddingBottom: '-10px',
  },
  card: {
    minWidth: '275px',
  },
  cardHeader: {
    fontWeight: '550px',
  },
  pendingIcon: {
    backgroundColor:tokens.colorPaletteBlueBorderActive,
    borderRadius: tokens.borderRadiusCircular,
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    width: "16px",
    height: "16px",
  },
  completedIcon: {
    backgroundColor: tokens.colorPaletteGreenBorderActive, 
    borderRadius: tokens.borderRadiusCircular,
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    width: '16px',
  },
  errorIcon: {
    backgroundColor: tokens.colorPaletteRedBackground3, 
    borderRadius: tokens.borderRadiusCircular,
    display: "flex",
    justifyContent: "center",
    alignItems: "center",
    width: '16px',
  },
  iconStatusRow: {
    display: "flex",
    alignItems: "center",
    gap: "4px", 
  }
});
