import { MutableRefObject, useCallback, useEffect, useMemo, useReducer, useRef, useState } from "react";
import { getAlertConfig, updateAlertConfig } from "../Services/Request";
import MonacoEditor, { Monaco } from '@monaco-editor/react';
import { CommandBar, Panel, PanelType, PrimaryButton, ICommandBarItemProps, Stack, Text, mergeStyles, IPanel, MessageBar, MessageBarType, Spinner, SpinnerSize } from "@fluentui/react";
import { useBoolean } from '@fluentui/react-hooks';
import { useSearchParams } from "react-router-dom";
import AlertEditorChat from "./AlertEditorChat";
import InstructionGeneration from "./InsturctionGeneration";
import DeployAgent from "./DeployAgent";
import { ICMAlertConfig, monacoJsonSchema } from "../Models/ICMAlertConfig";
import { useMutation, useQuery } from "@tanstack/react-query";


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
    const [alertConfig, setAlertConfig] = useState<ICMAlertConfig>(props.alertConfig ? { ...props.alertConfig } : {});
    const [defaultAlertConfig, setDefaultAlertConfig] = useState<ICMAlertConfig>(props.alertConfig ? { ...props.alertConfig } : {});
    const scrollRef = useRef<HTMLDivElement>(null);
    const panelRef = useRef<IPanel>(null);
    const alertConfigRef = useRef<ICMAlertConfig>(alertConfig);

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
        reset: resetSavingAlertConfigStatus,
    } = useMutation({
        mutationFn: async (alertConfig: ICMAlertConfig) => {
            return await updateAlertConfig(alertConfig.teamId, alertConfig.alertingId, alertConfig);
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
            setAlertConfig(data);
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
        alertConfigRef.current = { ...alertConfig };
        return () => {
            alertConfigRef.current = null;
            dismissPanel();
        }
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




    const onAlertConfigChange = useCallback((newValue: ICMAlertConfig | null) => {
        if (!newValue) return;
        props.isChangeUnsaved.current = true;
        setAlertConfig(newValue);
    }, [setAlertConfig]);

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
    }, [setAlertConfig]);

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
        props.isChangeUnsaved.current = false;
    }

    return (
        <>
            <Stack tokens={{ childrenGap: 10 }} styles={{ root: { marginTop: "20px" } }}>
                {defaultAlertConfig?.alertingId && !isAlertConfigLoading ?
                    <>
                        <Text variant="large">{titleAndSubtitle.title}</Text>
                        <CommandBar items={commandBarItems} styles={{ root: { paddingLeft: "0px" } }} />
                        {isSavingAlertConfigError && <MessageBar messageBarType={MessageBarType.error}>Sorry, an error occurred while saving alert config, please retry</MessageBar>}
                        {isSavingAlertConfigSuccess && <MessageBar messageBarType={MessageBarType.success}>Alert config saved successfully</MessageBar>}
                        <MonacoAlertEditor defaultConfig={defaultAlertConfig} onChange={onAlertConfigChange} />
                        <Stack horizontal tokens={{ childrenGap: 20 }} horizontalAlign="start">
                            {/* <DeployAgent /> */}
                            <PrimaryButton text="Test with your incident" onClick={(e) => openPanelForChat()} className={buttonStyles} />
                            <PrimaryButton text={isSavingAlertConfigLoading ? "Saving" : "Save Alert Config"} disabled={isSavingAlertConfigLoading || isAlertConfigLoading || isAlertConfigLoadingError} onClick={onSaveAlertConfig} className={buttonStyles} />
                        </Stack>
                    </> : <Spinner label="Loading alert config..." size={SpinnerSize.large} />
                }

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
                        alertId={alertConfig.alertingId}
                        teamId={alertConfig.teamId}
                        incidentTitleContains={alertConfig.incidentTitle}
                        incidentTitle={alertConfig.incidentTitle}
                        onGeneratedInstruction={updateAlertConfigWithInstruction} />
                }
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
        <MonacoEditor language="json" height="75vh" theme="vs-dark" options={{
            automaticLayout: true,
            formatOnType: true,
            formatOnPaste: true,
            fontSize: 15
        }}
            onMount={handleEditorDidMount}
            value={disPlayValue}
            onChange={(value, ev) => onValueChange(value)} />
    );
}

export default AlertEditor;