import { MutableRefObject, useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { createAlertConfig, getAlertConfig, updateAlertConfig } from "../Services/Request";
import MonacoEditor, { Monaco } from '@monaco-editor/react';
import { CommandBar, Panel, PanelType, ICommandBarItemProps, Stack, Text, mergeStyles, MessageBar, MessageBarType, Spinner, SpinnerSize } from "@fluentui/react";
import { useBoolean } from '@fluentui/react-hooks';
import AlertEditorChat from "./AlertEditorChat";
import InstructionGeneration from "./InsturctionGeneration";
import DeployAgent from "./DeployAgent";
import { ICMAlertConfig, monacoJsonSchema } from "../Models/ICMAlertConfig";
import { useMutation, useQuery } from "@tanstack/react-query";
import { PanelStyles } from "../Styles/Content.Styles";
import { useSharedUrlParams } from "../Context/UrlParamsProvider";


export interface AlertEditorProps {
    alertConfig?: any;
    icmTeamId?: string;
    alertId?: string;
    isChangeUnsaved?: MutableRefObject<boolean>;
}

type Action =
    | { type: 'SET_ALERT_EDITOR_CHAT' }
    | { type: 'SET_INSTRUCTION_GENERATION' }
    | { type: 'SET_DEPLOY_AGENT' };

enum SelectContent {
    AlertEditorChat = "AlertEditorChat",
    InstructionGeneration = "InstructionGeneration",
    DeployAgent = "DeployAgent",
}

enum AlertEditorMode {
    Edit = "Edit",
    Create = "Create",
}


//Edit existing alert, create a new custom alert, create a new alert from existing alert
const AlertEditor = (props: AlertEditorProps) => {
    const [isOpen, { setTrue: openPanel, setFalse: dismissPanel }] = useBoolean(false);
    const [defaultAlertConfig, setDefaultAlertConfig] = useState<ICMAlertConfig>(props.alertConfig ? { ...props.alertConfig } : {});
    const [editorMode] = useState<AlertEditorMode>(props.alertConfig ? AlertEditorMode.Create : AlertEditorMode.Edit);
    const scrollRef = useRef<HTMLDivElement>(null);
    const alertConfigRef = useRef<ICMAlertConfig>(props.alertConfig ? { ...props.alertConfig } : {});
    const urlParams = useSharedUrlParams();
    const mode = urlParams.mode; const reducer = (state: { selectedContent: SelectContent, panelHeader: string }, action: Action) => {
        switch (action.type) {
            case 'SET_ALERT_EDITOR_CHAT':
                openPanel();
                return { ...state, selectedContent: SelectContent.AlertEditorChat, panelHeader: "Test with your incident" };
            case 'SET_INSTRUCTION_GENERATION':
                openPanel();
                return { ...state, selectedContent: SelectContent.InstructionGeneration, panelHeader: "Generate Instructions" };
            case 'SET_DEPLOY_AGENT':
                openPanel();
                return { ...state, selectedContent: SelectContent.DeployAgent, panelHeader: "Deploy Agent" };
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
        reset: resetSavingAlertConfigStatus,
    } = useMutation({
        mutationFn: async (props: { alertConfig: ICMAlertConfig, editorMode: AlertEditorMode }) => {
            if (props.editorMode === AlertEditorMode.Create) {
                return await createAlertConfig(props.alertConfig);
            } else {
                return await updateAlertConfig(props.alertConfig.teamId, props.alertConfig.alertingId, props.alertConfig);
            }
        },
        mutationKey: ["updateAlertConfig"],
        gcTime: 0,
    });

    const {
        isLoading: isAlertConfigLoading,
        isError: isAlertConfigLoadingError,
    } = useQuery({
        queryKey: ["getAlertConfig", props.icmTeamId, props.alertId],
        queryFn: async () => {
            const data = await getAlertConfig(props.icmTeamId, props.alertId);
            setDefaultAlertConfig(data);
            return data;
        },
        enabled: !!props.icmTeamId && !!props.alertId,
        gcTime: 0,
    });

    //Reset success message after 3 seconds
    useEffect(() => {
        if (isSavingAlertConfigSuccess) {
            const timer = setTimeout(() => {
                resetSavingAlertConfigStatus();
            }, 3000);
            return () => {
                clearTimeout(timer);
            }
        }
    }, [isSavingAlertConfigSuccess, resetSavingAlertConfigStatus]);




    useEffect(() => {
        return () => {
            alertConfigRef.current = null;
            dismissPanel();
        }
    }, [props]);

    useEffect(() => {
        alertConfigRef.current = { ...defaultAlertConfig };
    }, [defaultAlertConfig]);

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
                // if (!alertConfig.alertingId) return;
                dispatch({ type: 'SET_INSTRUCTION_GENERATION' });
            }
        },
        {
            key: 'Test with Incident',
            text: 'Test with Incident',
            iconProps: { iconName: 'TestBeaker' },
            onClick: () => {
                openPanelForChat();
            }
        },
        {
            key: 'Deploy to Agent',
            text: 'Deploy to Agent',
            iconProps: { iconName: 'CloudUpload' },
            onClick: () => {
                if (mode === "playground") {
                    onDeployAgent();
                } else {
                    onSaveAlertConfig();
                }
            },
            disabled: isSavingAlertConfigLoading || isAlertConfigLoading || isAlertConfigLoadingError
        }
    ];




    const onAlertConfigChange = useCallback((newValue: ICMAlertConfig | null) => {
        if (!newValue) return;
        props.isChangeUnsaved.current = true;
        alertConfigRef.current = { ...newValue };
    }, []);

    const downloadAsJson = () => {
        var fileName = `${alertConfigRef.current.alertingId}.json`;

        const jsonString = JSON.stringify(alertConfigRef.current, null, 4); // Pretty print with 2-space indent
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

    const updateAlertConfigWithInstruction = useCallback((instructions: string[]) => {
        const newAlertConfig = { ...alertConfigRef.current, incidentProcessingGuide: instructions };
        setDefaultAlertConfig(newAlertConfig);
    }, []);

    const title = useMemo(() => {
        let title = "";
        let currentAlertConfig: any = {};
        if (props.alertConfig) {
            // For crating alert no need to have title
            return "";
        } else {
            title = `Editing alert `;
            currentAlertConfig = { ...defaultAlertConfig };
        }
        if (currentAlertConfig?.incidentTitle) {
            title = title + `for : ${currentAlertConfig.incidentTitle}`;
        }
        return title

    }, [defaultAlertConfig, props.alertConfig]);

    const onSaveAlertConfig = async () => {
        if (!alertConfigRef.current.teamId || !alertConfigRef.current.alertingId) return;
        await updateAlertConfigAsync({
            alertConfig: alertConfigRef.current,
            editorMode: editorMode
        });
        props.isChangeUnsaved.current = false;
    }    

    const alertEditorStyles = mergeStyles({
        marginTop: "20px",
        height: "100%",
        width: "80%",
        minWidth: "600px"
    })

    const onDeployAgent = async () => {
        dispatch({ type: 'SET_DEPLOY_AGENT' });
    }

    return (
        <>
            <Stack horizontalAlign="center" verticalFill>
                <Stack tokens={{ childrenGap: 10 }} className={alertEditorStyles} >
                    {defaultAlertConfig?.alertingId && !isAlertConfigLoading ?
                        <>
                            <Text variant="large">{title}</Text>
                            <CommandBar items={commandBarItems} styles={{ root: { paddingLeft: "0px" } }} />
                            {isSavingAlertConfigError && <MessageBar messageBarType={MessageBarType.error}>Sorry, an error occurred while saving alert config, please retry</MessageBar>}
                            {isSavingAlertConfigSuccess && <MessageBar messageBarType={MessageBarType.success}>Alert config saved successfully</MessageBar>}
                            <MonacoAlertEditor defaultConfig={defaultAlertConfig} onChange={onAlertConfigChange} />
                        </> : <Spinner label="Loading alert config..." size={SpinnerSize.large} />
                    }

                </Stack>
            </Stack>
            <Panel isOpen={isOpen}
                onDismiss={dismissPanel}
                closeButtonAriaLabel="Close"
                headerText={contextState.panelHeader}
                isBlocking={false}
                type={PanelType.medium}
                isFooterAtBottom={true}
                onRenderFooter={() => <div ref={scrollRef}></div>}>
                <div className={PanelStyles.container}>
                    {contextState.selectedContent === SelectContent.AlertEditorChat &&
                        <AlertEditorChat
                            alertConfigRef={alertConfigRef}
                            scrollRef={scrollRef}
                        />
                    }
                    {contextState.selectedContent === SelectContent.InstructionGeneration &&
                        <InstructionGeneration
                            alertConfigRef={alertConfigRef}
                            onGeneratedInstruction={updateAlertConfigWithInstruction} />
                    }
                    {contextState.selectedContent === SelectContent.DeployAgent &&
                        <DeployAgent
                            teamId={alertConfigRef.current.teamId}
                        />
                    }
                </div>
            </Panel>
        </>
    );
}

const MonacoAlertEditor = (props: { defaultConfig: any, onChange: (object: any | null) => void }) => {
    const [disPlayValue, setDisplayValue] = useState<string>("");

    useEffect(() => {
        if (!props.defaultConfig) return;
        const config = { ...props.defaultConfig };
        if (config.id) {
            delete config["id"];
        }
        setDisplayValue(JSON.stringify(config, null, 4));
    }, [props.defaultConfig]);

    const handleEditorDidMount = (_editor: any, monaco: Monaco) => {
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

    const onValueChange = (value: string | undefined) => {
        setDisplayValue(value);
        try {
            const parsedConfig = JSON.parse(value || "");
            if (props.defaultConfig?.id) {
                parsedConfig.id = props.defaultConfig.id;
            }
            props.onChange(parsedConfig);
        } catch (error) {

        }
    }


    return (
        <MonacoEditor language="json" height="100%" theme="vs-dark" options={{
            automaticLayout: true,
            formatOnType: true,
            formatOnPaste: true,
            fontSize: 15
        }}
            onMount={handleEditorDidMount}
            value={disPlayValue}
            onChange={(value, _ev) => onValueChange(value)} />
    );
}

export default AlertEditor;