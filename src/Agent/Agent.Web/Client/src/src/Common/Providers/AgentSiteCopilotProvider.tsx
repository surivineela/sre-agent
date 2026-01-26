import { CopilotProvider, CopilotTheme, ChatInputState } from "@fluentui-copilot/react-copilot"
import { makeStyles, mergeClasses, webDarkTheme, webLightTheme, } from "@fluentui/react-components"
import { ReactNode } from "react";
import { ThemeMode } from "../AzPortalProxy/Models/ITheme";

const useChatInputCustomStyles = makeStyles({
    inputWrapper: {
        gridTemplateRows: 'auto 1fr auto'
    },
});

const useChatInputStyles = (state: unknown) => {
    const styles = useChatInputCustomStyles();
    const chatInputState = state as ChatInputState;

    chatInputState.inputWrapper.className = mergeClasses(chatInputState.inputWrapper.className, styles.inputWrapper);
};

export const AgentSiteCopilotProvider = (props: { themeMode?: ThemeMode, children: ReactNode }) => {
    return (
        <CopilotProvider
            {...CopilotTheme}
            mode={'canvas'}
            theme={props.themeMode === ThemeMode.Dark ? webDarkTheme : webLightTheme}
            customStyleHooks={{
                useChatInputStyles
            }}
        >
            {props.children}
        </CopilotProvider>
    )
}