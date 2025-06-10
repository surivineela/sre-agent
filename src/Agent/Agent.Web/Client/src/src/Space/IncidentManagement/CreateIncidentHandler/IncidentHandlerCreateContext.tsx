import React from 'react';

export enum IncidentHandlerCreateSteps {
    GenerateHandler = 'GenerateHandler',
    ReviewAndEdit = 'ReviewAndEdit',
}

export interface IncidentHandlerCreateMetadata {
    currentStep: IncidentHandlerCreateSteps;
    setCurrentStep: React.Dispatch<React.SetStateAction<IncidentHandlerCreateSteps>>;
    instructions: string;
    setInstructions: React.Dispatch<React.SetStateAction<string>>;
    exitToHome: () => void;
}

export const IncidentHandlerCreateContext = React.createContext<IncidentHandlerCreateMetadata>({
    currentStep: IncidentHandlerCreateSteps.GenerateHandler,
    setCurrentStep: () => {},
    instructions: '',
    setInstructions: () => {},
    exitToHome: () => {},
});
