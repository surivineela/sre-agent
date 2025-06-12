import { IDropdownOption, Selection } from '@fluentui/react';
import React from 'react';
import { IIncidentDocumentWithKeyAndSelection, ToolInfoWithKeyAndSelection } from '../../../Common/Contracts/Azure/IncidentHandler';

export enum IncidentHandlerCreateSteps {
    GenerateHandler = 'GenerateHandler',
    ReviewAndEdit = 'ReviewAndEdit',
}

export interface IncidentHandlerCreateMetadata {
    agentName: string;
    incidentFilterId: string;
    currentStep: IncidentHandlerCreateSteps;
    setCurrentStep: React.Dispatch<React.SetStateAction<IncidentHandlerCreateSteps>>;
    exitToHome: () => void;
    name: string;
    setName: React.Dispatch<React.SetStateAction<string>>;
    description: string;
    setDescription: React.Dispatch<React.SetStateAction<string>>;
    incidentProcessingGuide: string;
    setIncidentProcessingGuide: React.Dispatch<React.SetStateAction<string>>;
    selectedTools: ToolInfoWithKeyAndSelection[];
    toolsSelection: React.MutableRefObject<Selection<ToolInfoWithKeyAndSelection> | undefined>;
    selectedIncidents: IIncidentDocumentWithKeyAndSelection[];
    incidentsSelection: React.MutableRefObject<Selection<IIncidentDocumentWithKeyAndSelection> | undefined>;
    customInstructions: string;
    setCustomInstructions: React.Dispatch<React.SetStateAction<string>>;
    incidentDocuments: IIncidentDocumentWithKeyAndSelection[] | undefined;
    loadingIncidents: boolean;
    toolInfos: ToolInfoWithKeyAndSelection[];
    loadingTools: boolean;
    timespanDropdownOptions: IDropdownOption<{ numberOfDays: number; isDefault?: boolean }>[];
    selectedTimespanOption: IDropdownOption<{ numberOfDays: number; isDefault?: boolean }> | undefined;
    setSelectedTimespanOption: React.Dispatch<IDropdownOption<{ numberOfDays: number; isDefault?: boolean }> | undefined>;
    isGeneratingInstructions: boolean;
    handleGenerateInstructions: () => void;
    initializeEditorDisplayValue: () => void;
    editorDisplayValue: string | undefined;
    setEditorDisplayValue: React.Dispatch<React.SetStateAction<string | undefined>>;
    onEditorValueChange: (value: string | undefined) => void;
    save: () => void;
}

export const IncidentHandlerCreateContext = React.createContext<IncidentHandlerCreateMetadata>({
    agentName: '',
    incidentFilterId: '',
    exitToHome: () => {},
    currentStep: IncidentHandlerCreateSteps.GenerateHandler,
    setCurrentStep: () => {},
    name: '',
    setName: () => {},
    description: '',
    setDescription: () => {},
    incidentProcessingGuide: '',
    setIncidentProcessingGuide: () => {},
    selectedTools: [],
    toolsSelection: { current: undefined },
    selectedIncidents: [],
    incidentsSelection: { current: undefined },
    customInstructions: '',
    setCustomInstructions: () => {},
    incidentDocuments: [],
    loadingIncidents: false,
    toolInfos: [],
    loadingTools: false,
    timespanDropdownOptions: [],
    selectedTimespanOption: undefined,
    setSelectedTimespanOption: () => {},
    isGeneratingInstructions: false,
    handleGenerateInstructions: () => {},
    initializeEditorDisplayValue: () => {},
    editorDisplayValue: undefined,
    setEditorDisplayValue: () => {},
    onEditorValueChange: () => {},
    save: () => {},
});
