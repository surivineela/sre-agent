import { mergeStyles, Separator, Stack } from "@fluentui/react";
import { memo, useCallback, useMemo, useReducer, useRef, useState } from "react";
import Landing from "./Landing";
import AlertEditor, { AlertEditorProps } from "./AlertEditor";
import EditOverview from "./EditOverview";
import SideNav from "./SideNav";
import { IcmTeamInfo } from "../Models/Response";
import { Step, StepLabel, Stepper } from "@mui/material";

enum Page {
    SelectedTeam = "SelectedTeam",
    AzureOverview = "AzureAlerting",
    AlertEditor = "AlertEditor",
}

interface MainContentProps {
    selectedTeam?: IcmTeamInfo;
    alertEditor?: AlertEditorProps;
}

type Action =
    | { type: 'SWITCH_TO_SELECT_TEAM' }
    | { type: 'SWITCH_TO_ALERT_OVERVIEW' }
    | { type: 'SWITCH_TO_ALERT_EDITING' }
    | { type: 'SET_ICM_TEAM', payload: IcmTeamInfo }
    | { type: 'SET_ALERT_CONFIG', payload: AlertEditorProps }




const MainContent = () => {
    const isEditorChangeUnsaved = useRef<boolean>(false);

    const isContinueNavigate = () => {
        if (isEditorChangeUnsaved.current) {
            const isContinue = window.confirm("You have unsaved changes. Do you want to continue?");
            if (isContinue) isEditorChangeUnsaved.current = false;
            return isContinue;
        }
        return true;
    }

    const reducer = (state: { page: Page, context: MainContentProps }, action: Action) => {
        switch (action.type) {
            case 'SWITCH_TO_SELECT_TEAM':
                if (!isContinueNavigate()) return { ...state };
                return { ...state, page: Page.SelectedTeam, context: { ...state.context, alertEditor: {} } };
            case 'SWITCH_TO_ALERT_OVERVIEW':
                if (!isContinueNavigate()) return { ...state };
                return { ...state, page: Page.AzureOverview, context: { ...state.context } };
            case 'SWITCH_TO_ALERT_EDITING':
                if (!isContinueNavigate()) return { ...state };
                return { ...state, page: Page.AlertEditor, context: { ...state.context } };
            case 'SET_ICM_TEAM':
                if (!isContinueNavigate()) return { ...state };
                return { ...state, page: Page.AzureOverview, context: { ...state.context, alertEditor: {}, selectedTeam: action.payload } };
            case 'SET_ALERT_CONFIG':
                if (!isContinueNavigate()) return { ...state };
                return { ...state, page: Page.AlertEditor, context: { ...state.context, alertEditor: action.payload } };
            default:
                return { ...state };
        }
    };


    const [state, dispatch] = useReducer(reducer, { page: Page.SelectedTeam, context: {} });
    const [displayStepper, setDisplayStepper] = useState(true);

    const steps = [
        {
            label: 'Select Team',
            page: Page.SelectedTeam,
            onClick: () => dispatch({ type: "SWITCH_TO_SELECT_TEAM" }),
            completed: () => Object.keys(state?.context?.selectedTeam ?? {}).length > 0,
        },
        {
            label: 'Choose Alert',
            page: Page.AzureOverview,
            onClick: () => {
                if (Object.keys(state?.context?.selectedTeam ?? {}).length === 0) return;
                dispatch({ type: "SWITCH_TO_ALERT_OVERVIEW" });
            },
            completed: () => Object.keys(state?.context?.alertEditor ?? {}).length > 0,
        },
        {
            label: 'Create Alert Config',
            page: Page.AlertEditor,
            onClick: () => {
                if (Object.keys(state?.context?.alertEditor ?? {}).length === 0) return;
                dispatch({ type: "SWITCH_TO_ALERT_EDITING" });
            },
            completed: () => false
        }
    ];

    const activateStep = useMemo(() => {
        return steps.findIndex((step) => step.page === state.page);
    }, [state.page]);

    const updateContextWithSelectedTeam = useCallback((props: IcmTeamInfo) => {
        dispatch({ type: 'SET_ICM_TEAM', payload: props });
    }, []);

    const updateContextWithAlertConfig = useCallback((props: AlertEditorProps) => {
        dispatch({ type: 'SET_ALERT_CONFIG', payload: props });
    }, []);

    const createNewAlertHandler = useCallback(() => {
        dispatch({ type: 'SWITCH_TO_SELECT_TEAM' });
        setDisplayStepper(true);
    }, []);

    const directToAlertEditor = useCallback((nextEditorProps: AlertEditorProps) => {
        dispatch({ type: 'SET_ALERT_CONFIG', payload: nextEditorProps });
        setDisplayStepper(false);
    }, [state.context]);

    const contentStyles = mergeStyles({
        width: "100%",
        height: "100%",
    });

    const separatorStyles = mergeStyles({
        height: "100%",
        width: "8px",
        backgroundColor: "rgb(223, 240, 255)",
    });



    return (
        <>
            <Stack horizontal verticalFill enableScopedSelectors tokens={{ childrenGap: 5 }}>
                <Stack.Item styles={{ root: { width: "15%" } }} grow={0}>
                    <SideNav onGetAlertConfig={directToAlertEditor} onCreateNewAlertHandler={createNewAlertHandler} selectedSideNavItemId={state.context?.alertEditor?.alertId ?? ""}/>
                </Stack.Item>
                <Stack.Item className={separatorStyles}>
                    <Separator vertical alignContent="center"></Separator>
                </Stack.Item>
                <Stack.Item grow={1}>
                    <Stack verticalFill tokens={{ childrenGap: 5 }} verticalAlign="start" enableScopedSelectors>
                        {displayStepper && <Stack.Item>
                            <Stepper activeStep={activateStep} alternativeLabel>
                                {steps.map((step) => (
                                    <Step key={step.label} onClick={step.onClick} completed={step.completed()} >
                                        <StepLabel>{step.label}</StepLabel>
                                    </Step>
                                ))}
                            </Stepper>
                        </Stack.Item>}
                        <Stack.Item className={contentStyles} align="start" >
                            {state.page === Page.SelectedTeam && <Landing onSelectTeam={updateContextWithSelectedTeam} defaultSelectedIcmInfo={state.context.selectedTeam} />}
                            {state.page === Page.AzureOverview && <EditOverview icmTeamInfo={state.context.selectedTeam} onGetAlertConfig={updateContextWithAlertConfig} />}
                            {state.page === Page.AlertEditor && <AlertEditor {...state.context.alertEditor} isChangeUnsaved={isEditorChangeUnsaved} />}
                        </Stack.Item>
                    </Stack>
                </Stack.Item>
            </Stack>
        </>
    );
}

export default memo(MainContent);