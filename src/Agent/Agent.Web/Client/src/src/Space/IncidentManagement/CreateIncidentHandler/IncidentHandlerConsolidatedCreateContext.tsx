import React from 'react';
import { IncidentDocument, ToolInfo } from '../../../Common/Contracts/Azure/IncidentHandler';
import { FilterMode, HandlerMode, TimeDuration } from './Contracts';

export enum IncidentHandlerCreateSteps {
    FilterStep = 'FilterStep',
    PreviewIncidentsStep = 'PreviewIncidentsStep',
    IncidentsAndGuidanceStep = 'IncidentsAndGuidanceStep',
    ReviewAndTestStep = 'ReviewAndTestStep',
}

export interface IncidentHandlerConsolidatedCreateMetadata {
    exitToHome: () => void;
    goToFullEditMode: () => void;
    currentStep: IncidentHandlerCreateSteps;
    setCurrentStep: React.Dispatch<React.SetStateAction<IncidentHandlerCreateSteps>>;
    generateInstructionsStepSkipped: boolean;
    setGenerateInstructionsStepSkipped: React.Dispatch<React.SetStateAction<boolean>>;
    incidents: IncidentDocument[] | undefined;
    selectedIncidents: IncidentDocument[] | undefined;
    onSelectedIncidentsChange: (newSelectedIncidentIds: string[]) => void;
    loadingIncidents: boolean;
    tools: ToolInfo[];
    toolsLoading: boolean;
    selectedTimespan: TimeDuration;
    onSelectedTimespanChange: (value: TimeDuration) => void;
    generatingInstructions: boolean;
    generateInstructions: () => void;
    generatingUpdatedTools: boolean;
    generateUpdatedTools: () => void;
    deleteHandler: () => void;
    exportHandler: () => void;
    saveHandler: () => Promise<void>;
    filterMode: FilterMode | undefined;
    handlerMode: HandlerMode | undefined;
    handlerLoaded: boolean;
    incidentsListDivRef: React.RefObject<HTMLDivElement | null>;
    isLoadingInitialIncidents: boolean;
    hasMoreOldIncidents: boolean;
    loadMoreOldIncidents: (overflowDiv: boolean) => Promise<boolean | undefined>;

    priorityOptions: string[];
    impactedServiceOptions: string[];
    incidentTypeOptions: string[];
}

export const IncidentHandlerConsolidatedCreateContext = React.createContext<IncidentHandlerConsolidatedCreateMetadata>({
    exitToHome: () => {},
    goToFullEditMode: () => {}, // only used in quick edit mode
    currentStep: IncidentHandlerCreateSteps.FilterStep,
    setCurrentStep: () => {},
    generateInstructionsStepSkipped: false,
    setGenerateInstructionsStepSkipped: () => {},
    incidents: [],
    selectedIncidents: [],
    onSelectedIncidentsChange: () => {},
    loadingIncidents: false,
    tools: [],
    toolsLoading: false,
    selectedTimespan: TimeDuration.Last60Days,
    onSelectedTimespanChange: () => {},
    generatingInstructions: false,
    generateInstructions: () => {},
    generatingUpdatedTools: false,
    generateUpdatedTools: () => {},
    deleteHandler: () => {},
    exportHandler: () => {},
    saveHandler: () => Promise.resolve(),
    filterMode: undefined,
    handlerMode: undefined,
    handlerLoaded: false,
    incidentsListDivRef: React.createRef<HTMLDivElement | null>(),
    isLoadingInitialIncidents: false,
    hasMoreOldIncidents: true,
    loadMoreOldIncidents: () => Promise.resolve(true),

    priorityOptions: [],
    impactedServiceOptions: [],
    incidentTypeOptions: [],
});
