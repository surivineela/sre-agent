import { MutableRefObject, useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { getAlertConfig, updateAlertConfig } from "../Services/Request";
import MonacoEditor, { Monaco } from '@monaco-editor/react';
import { CommandBar, Panel, PanelType, PrimaryButton, ICommandBarItemProps, Stack, Text, mergeStyles, IPanel, MessageBar, MessageBarType } from "@fluentui/react";
import { useBoolean } from '@fluentui/react-hooks';
import { useSearchParams } from "react-router-dom";
import AlertEditorChat from "./AlertEditorChat";
import InstructionGeneration from "./InsturctionGeneration";
import DeployAgent from "./DeployAgent";
import { ICMAlertConfig, monacoJsonSchema } from "../Models/ICMAlertConfig";
import { useMutation } from "@tanstack/react-query";


export interface AlertEditorProps {
    alertConfig?: any;
    icmTeamId?: string;
    alertId?: string;
    isChangeUnsaved?: MutableRefObject<boolean>;
}

type Action =
    | { type: 'SET_ALERT_EDITOR_CHAT' }
    | { type: 'SET_INSTRUCTION_GENERATION' };

enum SelectContent {
    AlertEditorChat = "AlertEditorChat",
    InstructionGeneration = "InstructionGeneration",
}


//Edit existing alert, create a new custom alert, create a new alert from existing alert
const AlertEditor = (props: AlertEditorProps) => {
    const [searchParams] = useSearchParams();
    const [isOpen, { setTrue: openPanel, setFalse: dismissPanel }] = useBoolean(false);
    const defaultAlertConfig = props.alertConfig ? { ...props.alertConfig } : {};
    const [alertConfig, setAlertConfig] = useState<any>(defaultAlertConfig);
    const scrollRef = useRef<HTMLDivElement>(null);
    const panelRef = useRef<IPanel>(null);
    const alertConfigRef = useRef<any>(alertConfig);

    const reducer = (state: { selectedContent: SelectContent, panelHeader: string }, action: Action) => {
        switch (action.type) {
            case 'SET_ALERT_EDITOR_CHAT':
                openPanel();
                return { ...state, selectedContent: SelectContent.AlertEditorChat, panelHeader: "Test with your incident" };
            case 'SET_INSTRUCTION_GENERATION':
                openPanel();
                return { ...state, selectedContent: SelectContent.InstructionGeneration, panelHeader: "Generate Instructions" };
            default:
                return { ...state };
        }
    };
    const [contextState, dispatch] = useReducer(reducer, { selectedContent: null, panelHeader: "" });

    const {
        isPending: isSavingAlertConfigLoading,
        mutateAsync: updateAlertConfigAsync,
        isError: isSavingAlertConfigError,
        isSuccess: isSavingAlertConfigSuccess,
    } = useMutation({
        mutationFn: async (alertConfig: ICMAlertConfig) => {
            return await updateAlertConfig(alertConfig.teamId, alertConfig.alertingId, alertConfig)
        },
        mutationKey: ["updateAlertConfig"],
    })



    useEffect(() => {
        alertConfigRef.current = { ...alertConfig };
        return () => {
            alertConfigRef.current = null;
            dismissPanel();
        }
    }, [alertConfig]);

    useEffect(() => {
        (async () => {
            if (!props.icmTeamId || !props.alertId) return;
            const res = await getAlertConfig(props.icmTeamId, props.alertId);
            setAlertConfig(res);
        })();
    }, [props.icmTeamId, props.alertId]);


    const sanitizedAlertConfigContent = useMemo(
        () => {
            if (!alertConfig) return "";
            const sanitizedConfig = { ...alertConfig };
            delete sanitizedConfig["id"];
            return JSON.stringify(sanitizedConfig, null, 4);
        }, [alertConfig]);

    let commandBarItems: ICommandBarItemProps[] = [
        {
            key: 'Export(JSON)',
            text: 'Export(JSON)',
            iconProps: { iconName: 'Download' },
            onClick: () => {
                downloadAsJson();
            },
        },
        {
            key: 'Generate Instructions',
            text: 'Generate Instructions',
            iconProps: { iconName: 'Robot' },
            onClick: () => {
                if (!alertConfig.alertingId) return;
                dispatch({ type: 'SET_INSTRUCTION_GENERATION' });
            },
            disabled: searchParams.get("generateInstructionsEnabled") !== "true",
        }
    ];


    const handleEditorDidMount = (editor: any, monaco: Monaco) => {
        // Configure JSON validation
        monaco.languages.json.jsonDefaults.setDiagnosticsOptions({
            validate: true,
            schemas: [{
                uri: "", // This is just an identifier
                fileMatch: ["*"],
                schema: monacoJsonSchema
            }]
        });
    };




    const onAlertConfigChange = (newValue: string | undefined) => {
        if (!newValue) return;
        try {
            const parsedConfig = JSON.parse(newValue);
            setAlertConfig(parsedConfig);
            props.isChangeUnsaved.current = true;
        } catch (error) {
        }
    }

    const downloadAsJson = () => {
        var fileName = `${alertConfig.alertingId}.json`;

        const jsonString = JSON.stringify(alertConfig, null, 2); // Pretty print with 2-space indent
        const blob = new Blob([jsonString], { type: 'application/json' });
        const url = window.URL.createObjectURL(blob);

        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = fileName;
        anchor.click();

        // Clean up the URL object
        window.URL.revokeObjectURL(url);
    }

    const openPanelForChat = () => {
        dispatch({ type: 'SET_ALERT_EDITOR_CHAT' })
    }

    const updateAlertConfigWithInstruction = useCallback((instruction: string) => {
        setAlertConfig((prevConfig: any) => {
            return { ...prevConfig, incidentProcessingGuide: instruction };
        });
    }, []);

    const buttonStyles = mergeStyles({
        maxWidth: "300px",
    });

    const titleAndSubtitle = useMemo(() => {
        // If props.alertEditorProps.alertConfig is not null, then we are creating a new alert, otherwise is editing existing alertConfig from API,
        let title = "";
        let subtitle = "";
        let currentAlertConfig: any = {};
        if (props.alertConfig) {
            title = `Creating alert `;
            currentAlertConfig = { ...props.alertConfig };
        } else {
            title = `Editing alert `;
            currentAlertConfig = { ...alertConfig };
        }
        if (currentAlertConfig?.incidentTitle) {
            title = title + `for : ${currentAlertConfig.incidentTitle}`;
        }
        if (currentAlertConfig?.alertingId) {
            subtitle = `Alert Id: ${currentAlertConfig.alertingId}`;
        }
        return { title, subtitle };

    }, [alertConfig, props.alertConfig]);

    const onSaveAlertConfig = async () => {
        if (!alertConfig.teamId || !alertConfig.alertingId) return;
        await updateAlertConfigAsync(alertConfig);
        props.isChangeUnsaved.current = true;
    }

    return (
        <>
            <Stack tokens={{ childrenGap: 10 }} styles={{ root: { marginTop: "20px" } }}>
                <Text variant="large">{titleAndSubtitle.title}</Text>
                {/* <Text variant="medium">{titleAndSubtitle.subtitle}</Text> */}
                <CommandBar items={commandBarItems} styles={{ root: { paddingLeft: "0px" } }} />
                {isSavingAlertConfigError && <MessageBar messageBarType={MessageBarType.error}>Sorry, an error occurred while saving alert config, please retry</MessageBar>}
                {isSavingAlertConfigSuccess && <MessageBar messageBarType={MessageBarType.success}>Alert config saved successfully</MessageBar>}
                <MonacoEditor language="json" value={sanitizedAlertConfigContent} height="75vh" theme="vs-dark" options={{
                    automaticLayout: true,
                    formatOnType: true,
                    formatOnPaste: true,
                    fontSize: 15,
                }}
                    onMount={handleEditorDidMount}
                    onChange={(value, ev) => onAlertConfigChange(value)} />
                <Stack horizontal tokens={{ childrenGap: 20 }} horizontalAlign="start">
                    {/* <DeployAgent /> */}
                    <PrimaryButton text="Test with your incident" onClick={(e) => openPanelForChat()} className={buttonStyles} />
                    <PrimaryButton text={isSavingAlertConfigLoading ? "Saving" : "Save Alert Config"} disabled={isSavingAlertConfigLoading} onClick={onSaveAlertConfig} className={buttonStyles} />
                </Stack>
            </Stack>
            <Panel isOpen={isOpen}
                onDismiss={dismissPanel}
                closeButtonAriaLabel="Close"
                headerText={contextState.panelHeader}
                isBlocking={false}
                type={PanelType.medium}
                isFooterAtBottom={true}
                onRenderFooter={() => <div ref={scrollRef}></div>}
                componentRef={panelRef}>
                {contextState.selectedContent === SelectContent.AlertEditorChat &&
                    <AlertEditorChat
                        alertConfig={alertConfigRef.current}
                        panelRef={panelRef}
                        scrollRef={scrollRef}
                    />
                }
                {contextState.selectedContent === SelectContent.InstructionGeneration &&
                    <InstructionGeneration
                        alertId={alertConfig.alertId}
                        teamId={alertConfig.teamId}
                        incidentTitleContains={alertConfig.incidentTitle}
                        incidentTitle={alertConfig.incidentTitle}
                        onGeneratedInstruction={updateAlertConfigWithInstruction} />
                }
            </Panel>
        </>
    );
}

export default AlertEditor;