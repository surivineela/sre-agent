import { DefaultButton, merge, mergeStyles, PrimaryButton, Separator, Stack, Text } from "@fluentui/react";
import { useState } from "react";
import AzureAlertsOverview from "./AzureAlertsOverview";
import { AlertEditorProps } from "./AlertEditor";
import { IcmTeamInfo } from "../Models/Response";
import { generateCustomAlertConfig } from "../Services/AlertUtilities";
import { ContentStyleSets } from "../Styles/Content.Styles";


enum EditViewType {
    Overview,
    AzureAlerting,
}

const EditOverview = (props: { icmTeamInfo: IcmTeamInfo, onGetAlertConfig: (params: AlertEditorProps) => void }) => {
    const [currentViewType, setCurrentViewType] = useState(EditViewType.Overview);

    const navigateToCustomAlerting = () => {
        const customAlertConfig = generateCustomAlertConfig(props.icmTeamInfo);
        const editorProps: AlertEditorProps = {
            alertConfig: customAlertConfig
        };
        props.onGetAlertConfig(editorProps);
    }

    const verticalLineStyle = mergeStyles({
        height: "200px"
    });

    const buttonStyle = mergeStyles({
        border: "0px",
    });

    const overview = (
        <Stack verticalFill horizontalAlign="center" verticalAlign="start">
            <Stack tokens={{ childrenGap: 20 }} horizontalAlign="center" className={ContentStyleSets.container}>
                <Text variant="xxLarge" className={mergeStyles({ paddingBottom: "50px" })}>Select Alert Type</Text>
                <Stack horizontal tokens={{ childrenGap: 20 }} enableScopedSelectors horizontalAlign="space-evenly">
                    <DefaultButton className={buttonStyle} onClick={(e) => setCurrentViewType(EditViewType.AzureAlerting)}>
                        <Stack tokens={{ childrenGap: 10 }} >
                            <Text block variant="xLarge">Azure Alerting</Text>
                            <Text block variant="mediumPlus">Use an existing Azure alert to create a handler configuration</Text>
                        </Stack>
                    </DefaultButton>
                    <Stack.Item className={verticalLineStyle}>
                        <Separator vertical alignContent="center">OR</Separator>
                    </Stack.Item>
                    <DefaultButton className={buttonStyle} onClick={(e) => { navigateToCustomAlerting() }}>
                        <Stack tokens={{ childrenGap: 10 }} >
                            <Text block variant="xLarge">Custom Alerting</Text>
                            <Text block variant="mediumPlus">Create a custom alert handler configuration from scratch</Text>
                        </Stack>
                    </DefaultButton>
                </Stack>
            </Stack>
        </Stack>
    );

    return (
        <>
            {currentViewType === EditViewType.Overview && overview}
            {currentViewType === EditViewType.AzureAlerting && <AzureAlertsOverview {...props} />}
        </>
    );
}

export default EditOverview;