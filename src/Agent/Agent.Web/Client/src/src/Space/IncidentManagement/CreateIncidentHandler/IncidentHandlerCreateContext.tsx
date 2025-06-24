import React from 'react';
import { IncidentDocument, ToolInfo } from '../../../Common/Contracts/Azure/IncidentHandler';

export enum IncidentHandlerCreateSteps {
    GenerateHandler = 'GenerateHandler',
    ReviewAndEdit = 'ReviewAndEdit',
}

export enum TimeDuration {
    Last15Days = 15,
    Last30Days = 30,
    Last60Days = 60,
    Last90Days = 90,
}

export type CreateOrEditMode = 'create' | 'edit' | 'quickEdit';
export type OperationStatus = 'inprogress' | 'succeeded' | 'failed';

export interface IncidentHandlerCreateMetadata {
    agentName: string;
    incidentFilterId: string | undefined;
    exitToHome: () => void;
    goToFullEditMode: () => void;
    isDirty: boolean;
    currentStep: IncidentHandlerCreateSteps;
    setCurrentStep: React.Dispatch<React.SetStateAction<IncidentHandlerCreateSteps>>;
    generateInstructionsStepSkipped: boolean;
    setGenerateInstructionsStepSkipped: React.Dispatch<React.SetStateAction<boolean>>;
    name: string;
    onNameChange: (value: string) => void;
    description: string;
    onDescriptionChange: (value: string) => void;
    incidentProcessingGuide: string;
    customInstructions: string;
    onCustomInstructionsChange: (value: string) => void;
    incidents: IncidentDocument[] | undefined;
    selectedIncidentIds: string[] | undefined;
    selectedIncidents: IncidentDocument[] | undefined;
    onSelectedIncidentsChange: (newSelectedIncidentIds: string[]) => void;
    loadingIncidents: boolean;
    tools: ToolInfo[];
    selectedToolNames: string[] | undefined;
    onSelectedToolsChange: (newSelectedToolNames: string[]) => void;
    toolsLoading: boolean;
    selectedTimespan: TimeDuration;
    onSelectedTimespanChange: (value: TimeDuration) => void;
    generatingInstructions: boolean;
    generateInstructions: () => void;
    initializeEditorDisplayValue: () => void;
    editorDisplayValue: string | undefined;
    onEditorValueChange: (value: string | undefined) => void;
    isEditorValueValid: boolean;
    setIsEditorValueValid: React.Dispatch<React.SetStateAction<boolean>>;
    saveHandler: () => void;
    deleteHandler: () => void;
    exportHandler: () => void;
    mode: CreateOrEditMode | undefined;
    handlerLoaded: boolean;
    incidentsListDivRef: React.RefObject<HTMLDivElement | null>;
    isLoadingInitialIncidents: boolean;
    hasMoreOldIncidents: boolean;
    loadMoreOldIncidents: (overflowDiv: boolean) => Promise<boolean | undefined>;
}

export const IncidentHandlerCreateContext = React.createContext<IncidentHandlerCreateMetadata>({
    agentName: '',
    incidentFilterId: undefined,
    exitToHome: () => {},
    goToFullEditMode: () => {},
    isDirty: false,
    currentStep: IncidentHandlerCreateSteps.GenerateHandler,
    setCurrentStep: () => {},
    generateInstructionsStepSkipped: false,
    setGenerateInstructionsStepSkipped: () => {},
    name: '',
    onNameChange: () => {},
    description: '',
    onDescriptionChange: () => {},
    incidentProcessingGuide: '',
    customInstructions: '',
    onCustomInstructionsChange: () => {},
    incidents: [],
    selectedIncidentIds: [],
    selectedIncidents: [],
    onSelectedIncidentsChange: () => {},
    loadingIncidents: false,
    tools: [],
    selectedToolNames: [],
    onSelectedToolsChange: () => {},
    toolsLoading: false,
    selectedTimespan: TimeDuration.Last60Days,
    onSelectedTimespanChange: () => {},
    generatingInstructions: false,
    generateInstructions: () => {},
    initializeEditorDisplayValue: () => {},
    editorDisplayValue: undefined,
    onEditorValueChange: () => {},
    isEditorValueValid: true,
    setIsEditorValueValid: () => {},
    saveHandler: () => {},
    deleteHandler: () => {},
    exportHandler: () => {},
    mode: undefined,
    handlerLoaded: false,
    incidentsListDivRef: React.createRef<HTMLDivElement | null>(),
    isLoadingInitialIncidents: false,
    hasMoreOldIncidents: true,
    loadMoreOldIncidents: () => Promise.resolve(true),
});
