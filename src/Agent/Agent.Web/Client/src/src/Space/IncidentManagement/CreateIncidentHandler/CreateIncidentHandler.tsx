import { Breadcrumb, BreadcrumbButton, BreadcrumbDivider, BreadcrumbItem, tokens } from '@fluentui/react-components';
import { FC } from 'react';
import { IncidentHandler } from '../../../Common/Contracts/Azure/IncidentHandler';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from './IncidentHandlerCreateContext';
import { GenerateHandler } from './Steps/GenerateHandler';
import { ReviewAndEdit } from './Steps/ReviewAndEdit';
import { StepWizard } from './StepWizard/StepWizard';
import { useCreateIncidentHandler } from './useCreateIncidentHandler';

interface CreateIncidentHandlerProps {
    exitToHome: () => void;
    incidentFilterId: string;
    createHandler: (handler: IncidentHandler) => void; // Replace 'any' with the actual type of handler
}

const CreateIncidentHandler: FC<CreateIncidentHandlerProps> = ({ exitToHome, incidentFilterId, createHandler }) => {
    const incidentHandlerCreateMetadata = useCreateIncidentHandler(incidentFilterId, exitToHome, createHandler);
    const { intl, currentStep } = incidentHandlerCreateMetadata;

    return (
        <div style={{ background: tokens.colorNeutralBackground3 }}>
            <Breadcrumb style={{ display: 'flex', height: 50, marginLeft: 16 }}>
                <BreadcrumbItem>
                    <BreadcrumbButton onClick={() => exitToHome()}>
                        {intl.formatMessage(IncidentHandlerCreateResources.incidentManagement)}
                    </BreadcrumbButton>
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>
                    {intl.formatMessage(IncidentHandlerCreateResources.newCustomHandler)}
                </BreadcrumbItem>
            </Breadcrumb>
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'row',
                    gap: 12,
                    borderRadius: tokens.borderRadiusXLarge,
                    boxShadow: tokens.shadow4,
                    marginLeft: 16,
                    height: 'calc(100vh - 95px)',
                    background: tokens.colorNeutralBackground1,
                }}
            >
                <div
                    style={{
                        padding: 20,
                        borderRight: `1px solid ${tokens.colorNeutralStroke2}`,
                        minWidth: 280,
                        overflowY: 'auto',
                    }}
                >
                    <StepWizard
                        currentStep={currentStep}
                        steps={[
                            {
                                stepKey: IncidentHandlerCreateSteps.GenerateHandler,
                                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.generateHandler),
                            },
                            {
                                stepKey: IncidentHandlerCreateSteps.ReviewAndEdit,
                                stepTitle: intl.formatMessage(IncidentHandlerCreateResources.reviewAndEdit),
                            },
                        ]}
                    />
                </div>
                <div
                    style={{
                        height: '100%',
                        width: '100%',
                        overflowY: 'auto',
                    }}
                >
                    <IncidentHandlerCreateContext.Provider value={incidentHandlerCreateMetadata}>
                        {currentStep === IncidentHandlerCreateSteps.GenerateHandler ? (
                            <GenerateHandler />
                        ) : currentStep === IncidentHandlerCreateSteps.ReviewAndEdit ? (
                            <ReviewAndEdit />
                        ) : null}
                    </IncidentHandlerCreateContext.Provider>
                </div>
            </div>
        </div>
    );
};

export default CreateIncidentHandler;
