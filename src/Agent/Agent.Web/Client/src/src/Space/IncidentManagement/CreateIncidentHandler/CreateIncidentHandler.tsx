import { Breadcrumb, BreadcrumbButton, BreadcrumbDivider, BreadcrumbItem, tokens } from '@fluentui/react-components';
import { FC, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from './IncidentHandlerCreateContext';
import { GenerateHandler } from './Steps/GenerateHandler';
import { ReviewAndEdit } from './Steps/ReviewAndEdit';
import { StepWizard } from './StepWizard/StepWizard';

interface CreateIncidentHandlerProps {
    exitToHome: () => void;
}

const CreateIncidentHandler: FC<CreateIncidentHandlerProps> = ({ exitToHome }) => {
    const intl = useIntl();
    const [currentStep, setCurrentStep] = useState<IncidentHandlerCreateSteps>(IncidentHandlerCreateSteps.GenerateHandler);
    const [instructions, setInstructions] = useState<string>('');

    return (
        <div style={{ background: tokens.colorNeutralBackground3 }}>
            <Breadcrumb style={{ display: 'flex', height: 50, marginLeft: 16 }}>
                <BreadcrumbItem>
                    <BreadcrumbButton onClick={() => exitToHome()}>Incident handlers</BreadcrumbButton>
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>New incident handler</BreadcrumbItem>
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
                <div style={{ width: '100%' }}>
                    <IncidentHandlerCreateContext.Provider
                        value={{
                            currentStep,
                            setCurrentStep,
                            instructions,
                            setInstructions,
                            exitToHome,
                        }}
                    >
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
