import React from 'react';
import { IncidentDocument, ToolInfo } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentManagementType } from '../../../Common/Contracts/Azure/SreAgent';
import { FilterMode, HandlerMode, TimeDuration } from './Contracts';

export enum IncidentHandlerCreateSteps {
    FilterStep = 'FilterStep',
    IncidentTriggerStep = 'IncidentTriggerStep',
    PreviewIncidentsStep = 'PreviewIncidentsStep',
    IncidentsAndGuidanceStep = 'IncidentsAndGuidanceStep',
    ReviewAndTestStep = 'ReviewAndTestStep',
}

export interface IncidentHandlerTestMetadata {
    searchTerm?: string;
    setSearchTerm: React.Dispatch<React.SetStateAction<string>>;
    incidents?: IncidentDocument[];
    loadingIncidents?: boolean;

    createTestThread: () => void;
    createTestThreadFailure?: string;
    creatingTestThread?: boolean;
    testIncidentThreadId?: string;
}

export interface IncidentsPreviewMetadata {
    loadingIncidents: boolean;
    incidents: IncidentDocument[] | undefined;

    selectedTimespan: TimeDuration;
    onSelectedTimespanChange: (value: TimeDuration) => void;

    // For paginating incidents on scroll
    incidentsListDivRef: React.RefObject<HTMLDivElement | null>;
    isLoadingInitialIncidents: boolean;
    hasMoreOldIncidents: boolean;
    loadMoreOldIncidents: (overflowDiv: boolean) => Promise<boolean | undefined>;
}

export interface IncidentHandlerConsolidatedCreateMetadata {
    incidentPlatformType: IncidentManagementType | undefined;
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
    isSubagentTrigger?: boolean;
    subAgentNames?: string[];
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

    handlerTestMetadata: IncidentHandlerTestMetadata;
    incidentsPreviewMetadata: IncidentsPreviewMetadata;
}

export const IncidentHandlerConsolidatedCreateContext = React.createContext<IncidentHandlerConsolidatedCreateMetadata>({
    incidentPlatformType: undefined,
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
    selectedTimespan: TimeDuration.Last30Days,
    onSelectedTimespanChange: () => {},
    generatingInstructions: false,
    generateInstructions: () => {},
    generatingUpdatedTools: false,
    generateUpdatedTools: () => {},
    deleteHandler: () => {},
    exportHandler: () => {},
    saveHandler: () => Promise.resolve(),
    isSubagentTrigger: undefined,
    subAgentNames: undefined,
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
    handlerTestMetadata: {
        searchTerm: '',
        setSearchTerm: () => {},
        incidents: [],
        loadingIncidents: false,
        createTestThread: () => {},
        createTestThreadFailure: undefined,
        creatingTestThread: false,
        testIncidentThreadId: undefined,
    },
    incidentsPreviewMetadata: {
        loadingIncidents: false,
        incidents: [],
        selectedTimespan: TimeDuration.Last30Days,
        onSelectedTimespanChange: () => {},
        incidentsListDivRef: React.createRef<HTMLDivElement | null>(),
        isLoadingInitialIncidents: false,
        hasMoreOldIncidents: true,
        loadMoreOldIncidents: () => Promise.resolve(true),
    },
});
