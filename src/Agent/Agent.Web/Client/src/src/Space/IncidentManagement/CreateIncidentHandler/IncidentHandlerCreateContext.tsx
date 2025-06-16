import React from 'react';
import { IncidentDocument, ToolInfo, WithSelection } from '../../../Common/Contracts/Azure/IncidentHandler';

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
    currentStep: IncidentHandlerCreateSteps;
    setCurrentStep: React.Dispatch<React.SetStateAction<IncidentHandlerCreateSteps>>;
    generateInstructionsStepSkipped: boolean;
    setGenerateInstructionsStepSkipped: React.Dispatch<React.SetStateAction<boolean>>;
    exitToHome: () => void;
    name: string;
    setName: React.Dispatch<React.SetStateAction<string>>;
    description: string;
    setDescription: React.Dispatch<React.SetStateAction<string>>;
    incidentProcessingGuide: string;
    setIncidentProcessingGuide: React.Dispatch<React.SetStateAction<string>>;
    customInstructions: string;
    setCustomInstructions: React.Dispatch<React.SetStateAction<string>>;
    incidents: WithSelection<IncidentDocument>[] | undefined;
    setIncidents: React.Dispatch<React.SetStateAction<WithSelection<IncidentDocument>[] | undefined>>;
    onSelectedIncidentsChange: (newSelectedIncidentIds: string[]) => void;
    loadingIncidents: boolean;
    tools: WithSelection<ToolInfo>[];
    setTools: React.Dispatch<React.SetStateAction<WithSelection<ToolInfo>[] | undefined>>;
    onSelectedToolsChange: (newSelectedToolNames: string[]) => void;
    toolsLoading: boolean;
    selectedTimespan: TimeDuration;
    setSelectedTimespan: React.Dispatch<TimeDuration>;
    generatingInstructions: boolean;
    generateInstructions: () => void;
    initializeEditorDisplayValue: () => void;
    editorDisplayValue: string | undefined;
    setEditorDisplayValue: React.Dispatch<React.SetStateAction<string | undefined>>;
    onEditorValueChange: (value: string | undefined) => void;
    isEditorValueValid: boolean;
    setIsEditorValueValid: React.Dispatch<React.SetStateAction<boolean>>;
    saveHandler: () => void;
    deleteHandler: () => void;
    exportHandler: () => void;
    mode: CreateOrEditMode | undefined;
    setMode: React.Dispatch<React.SetStateAction<CreateOrEditMode | undefined>>;
    handlerLoaded: boolean;
}

export const IncidentHandlerCreateContext = React.createContext<IncidentHandlerCreateMetadata>({
    agentName: '',
    incidentFilterId: undefined,
    exitToHome: () => {},
    currentStep: IncidentHandlerCreateSteps.GenerateHandler,
    setCurrentStep: () => {},
    generateInstructionsStepSkipped: false,
    setGenerateInstructionsStepSkipped: () => {},
    name: '',
    setName: () => {},
    description: '',
    setDescription: () => {},
    incidentProcessingGuide: '',
    setIncidentProcessingGuide: () => {},
    customInstructions: '',
    setCustomInstructions: () => {},
    incidents: [],
    setIncidents: () => {},
    onSelectedIncidentsChange: () => {},
    loadingIncidents: false,
    tools: [],
    setTools: () => {},
    onSelectedToolsChange: () => {},
    toolsLoading: false,
    selectedTimespan: TimeDuration.Last60Days,
    setSelectedTimespan: () => {},
    generatingInstructions: false,
    generateInstructions: () => {},
    initializeEditorDisplayValue: () => {},
    editorDisplayValue: undefined,
    setEditorDisplayValue: () => {},
    onEditorValueChange: () => {},
    isEditorValueValid: true,
    setIsEditorValueValid: () => {},
    saveHandler: () => {},
    deleteHandler: () => {},
    exportHandler: () => {},
    mode: undefined,
    setMode: () => {},
    handlerLoaded: false,
});
