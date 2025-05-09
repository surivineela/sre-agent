import { DefaultButton, merge, mergeStyles, PrimaryButton, Separator, Stack, Text } from "@fluentui/react";
import { useState } from "react";
import AzureAlertsOverview from "./AzureAlertsOverview";
import { AlertEditorProps } from "./AlertEditor";
import { IcmTeamInfo } from "../Models/Response";
import { generateCustomAlertConfig } from "../Services/AlertUtilities";


enum EditViewType {
    Overview,
    AzureAlerting,
    CustomAlerting,
}

const EditOverview = (props: { icmTeamInfo: IcmTeamInfo, onGetAlertConfig: (params: AlertEditorProps) => void }) => {
    const [currentViewType, setCurrentViewType] = useState(EditViewType.Overview);

    const navigateToCustomAlerting = () => {
        setCurrentViewType(EditViewType.CustomAlerting);
        const customAlertConfig = generateCustomAlertConfig(props.icmTeamInfo);
        const editorProps: AlertEditorProps = {
            alertConfig: customAlertConfig
        };
        props.onGetAlertConfig(editorProps);
    }

    const verticalLineStyle = mergeStyles({
        height: "200px"
    });

    const overview = (
        <Stack verticalFill horizontalAlign="center" verticalAlign="center">
            <Stack tokens={{ childrenGap: 20 }} horizontalAlign="center">
                <Text variant="xxLarge">Select Alert Type</Text>
                <Stack horizontal tokens={{ childrenGap: 20 }} enableScopedSelectors horizontalAlign="space-evenly">
                    <DefaultButton onClick={(e) => setCurrentViewType(EditViewType.AzureAlerting)}>
                        <Stack tokens={{ childrenGap: 10 }} >
                            <Text block variant="xLarge">Azure Alerting</Text>
                            <Text block variant="mediumPlus">Use an existing Azure alert to create a handler configuration</Text>
                        </Stack>
                    </DefaultButton>
                    <Stack.Item className={verticalLineStyle}>
                        <Separator vertical alignContent="center">OR</Separator>
                    </Stack.Item>
                    <DefaultButton onClick={(e) => { navigateToCustomAlerting() }}>
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