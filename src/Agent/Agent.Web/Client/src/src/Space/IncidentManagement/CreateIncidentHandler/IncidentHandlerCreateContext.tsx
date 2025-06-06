import React from 'react';

export enum IncidentHandlerCreateSteps {
    GenerateHandler = 'GenerateHandler',
    ReviewAndEdit = 'ReviewAndEdit',
}

export interface IncidentHandlerCreateMetadata {
    currentStep: IncidentHandlerCreateSteps;
    setCurrentStep: React.Dispatch<React.SetStateAction<IncidentHandlerCreateSteps>>;
}

export const IncidentHandlerCreateContext = React.createContext<IncidentHandlerCreateMetadata>({
    currentStep: IncidentHandlerCreateSteps.GenerateHandler,
    setCurrentStep: () => {},
});
