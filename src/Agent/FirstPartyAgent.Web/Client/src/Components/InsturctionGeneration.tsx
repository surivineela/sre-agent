import React, { useEffect, useState, useRef, memo, MutableRefObject } from "react";
import { generateInstructions, getIncidents } from "../Services/Request";
import { GenerateInstructionsRequest, IcmIncident } from "../Models/Response";
import { DetailsList, DetailsListLayoutMode, Dropdown, IColumn, MessageBar, MessageBarType, PrimaryButton, SelectionMode, Stack, TextField, Selection, IDropdownOption } from "@fluentui/react";
import { useMutation, useQuery } from "@tanstack/react-query";
import LoadingErrorWrapper from "./LoadingErrorWrapper";
import ErrorUtilities from "../Helpers/Error";
import { ICMAlertConfig } from "../Models/ICMAlertConfig";

export interface InstructionGenerationProps {
    onGeneratedInstruction: (instructions: string[]) => void;
    alertConfigRef: MutableRefObject<ICMAlertConfig>;
}

const InstructionGeneration = (props: InstructionGenerationProps) => {
    const dropdownOptions: IDropdownOption<{ numberOfDays: number }>[] = [
        { key: "15 days", text: "15 days", data: { numberOfDays: 15 } },
        { key: "30 days", text: "30 days", data: { numberOfDays: 30 } },
        { key: "60 days", text: "60 days", data: { numberOfDays: 60 } },
        { key: "90 days", text: "90 days", data: { numberOfDays: 90 } }
    ];
    //Set last 30 days as default
    const defaultOption = dropdownOptions.find(option => option.data.numberOfDays === 30) ?? null;
    const [selectedOption, setSelectedOption] = useState(defaultOption);
    const [customInstruction, setCustomInstruction] = useState<string>("");
    const [validateInstructionError, setValidateInstructionError] = useState<string>("");
    const selectedICMsRef = useRef<IcmIncident[]>([]);

    const { refetch: getIncidentsRefresh, data: getIncidentsData = [], status: getIncidentsStatus, error: getIncidentsError } = useQuery({
        queryKey: ["getIncidents", props.alertConfigRef.current.teamId, props.alertConfigRef.current.incidentTitle, selectedOption?.data.numberOfDays],
        queryFn: () => getIncidents(props.alertConfigRef.current.teamId, props.alertConfigRef.current.incidentTitle, selectedOption?.data.numberOfDays),
    });

    const { status: generateInstructionStatus, mutateAsync: generateInstructionAsync,reset: resetGenerateInstruction } = useMutation({
        mutationFn: (request: GenerateInstructionsRequest) => generateInstructions(request),
        mutationKey: ["generateInstructions"],
    });

    useEffect(() => {
        (async () => {
            await getIncidentsRefresh();
        })();
    }, [selectedOption]);

    const runGenerateInstruction = async () => {
        setValidateInstructionError("");
        if ((!selectedICMsRef.current || selectedICMsRef.current.length === 0) && customInstruction.trim() === "") {
            setValidateInstructionError("Please select at least one incident or provide a custom instruction.");
            return;
        }

        const incidentIds = selectedICMsRef.current.map(icm => icm.id);
        const request: GenerateInstructionsRequest = {
            incidentIds: incidentIds,
            customInstructions: customInstruction,
        }
        const res = await generateInstructionAsync(request);
        if (res) {
            props.onGeneratedInstruction(res.instructions);
        }
        resetGenerateInstruction();
    }

    const renderGenerateInstructionError = () => {
        let message = "";
        if (validateInstructionError) {
            message = validateInstructionError;
        }
        else if (generateInstructionStatus === "error") {
            message = "Error occurred during generation instruction, please retry";
        }

        if (message === "") {
            return null;
        } else {
            return <MessageBar isMultiline messageBarType={MessageBarType.error} styles={{ text: { fontSize: "15px" } }}>{message}</MessageBar>;
        }
    }

    return (
        <div>
            <Stack tokens={{ childrenGap: 10 }}>
                <Stack horizontal horizontalAlign="space-between" verticalAlign="center">
                    <h3>Past Mitigated/Resolved Incidents of this Alert</h3>
                    <Dropdown options={dropdownOptions} selectedKey={selectedOption.key} onChange={(e, o) => setSelectedOption(o)} disabled={getIncidentsStatus === "pending"} />
                </Stack>

                <div style={{ height: "50vh" }}>
                    <LoadingErrorWrapper error={getIncidentsError} status={getIncidentsStatus} renderLoading="Loading ICM incidents..." renderError="An error occurred while loading ICM incidents">
                        <InstructionGenerationIcmList data={getIncidentsData} selectedICMsRef={selectedICMsRef} />
                    </LoadingErrorWrapper>
                </div>

                <TextField label="Custom Instruction" multiline rows={10} value={customInstruction} onChange={(e, newValue) => setCustomInstruction(newValue)} />
                <Stack horizontal horizontalAlign="center">
                    <PrimaryButton text="Generate Instruction" onClick={(e) => runGenerateInstruction()} disabled={getIncidentsStatus === "pending" || generateInstructionStatus === "pending"} />
                </Stack>
                {renderGenerateInstructionError()}
            </Stack>
        </div>
    );

}

const InstructionGenerationIcmList = (props: { data: IcmIncident[], selectedICMsRef: React.MutableRefObject<IcmIncident[]> }) => {
    const columns: IColumn[] = [
        {
            key: "id",
            name: "Id",
            fieldName: "id",
            minWidth: 50,
            maxWidth: 60,
        },
        {
            key: "title",
            name: "Title",
            fieldName: "title",
            minWidth: 180,
            isMultiline: true,
        },
        {
            key: "state",
            name: "State",
            fieldName: "state",
            minWidth: 60,
            maxWidth: 60,
        },
        {
            key: "severity",
            name: "Sev",
            fieldName: "severity",
            minWidth: 30,
            maxWidth: 30,
        },
        {
            key: "createdDate",
            name: "Created Date",
            fieldName: "createdDate",
            minWidth: 100,
            maxWidth: 100,
            onRender: (item: IcmIncident) => {
                const date = new Date(item.createdDate);

                const datePart = Intl.DateTimeFormat("en-US", {
                    year: 'numeric',
                    month: '2-digit',
                    day: '2-digit',
                }).format(date);

                const timePart = Intl.DateTimeFormat("en-US", {
                    hour: '2-digit',
                    minute: '2-digit',
                    second: '2-digit',
                    hour12: false,
                    timeZoneName: 'short',
                }).format(date);


                return (
                    <>
                        <div>{datePart}</div>
                        <div>{timePart}</div>
                    </>
                );
            }
        }
    ];

    const listSelector = new Selection({
        onSelectionChanged: () => {
            const selected = listSelector.getSelection() as IcmIncident[];
            props.selectedICMsRef.current = selected;
        },
    })

    useEffect(() => {
        const selectedICMs = [];
        for (let i = 0; i < props.data.length; i++) {
            const icm = props.data[i];
            if (icm.state === "MITIGATED" || icm.state === "RESOLVED") {
                selectedICMs.push(icm);
                listSelector.setIndexSelected(i, true, false);
            }
            if (selectedICMs.length >= 3) {
                break;
            }
        }
        props.selectedICMsRef.current = selectedICMs;
    }, []);

    return (
        <>
            {props.data.length === 0 && <MessageBar isMultiline messageBarType={MessageBarType.info}>No mitigated/resolved incidents found for this alert during selected time range.</MessageBar>}
            {props.data.length > 0 && <DetailsList columns={columns} items={props.data} selectionMode={SelectionMode.multiple} layoutMode={DetailsListLayoutMode.justified} compact={true} selection={listSelector} />}
        </>
    );
}

export default InstructionGeneration;
