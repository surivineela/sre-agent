import { Button } from '@fluentui/react-components';
import { useContext } from 'react';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';

export const GenerateHandler = () => {
    const context = useContext(IncidentHandlerCreateContext);
    const { setCurrentStep } = context;
    return (
        <div>
            <div>Content for Step 2: Generate handler</div>
            <div style={{ marginTop: '20px' }}>
                <Button onClick={() => setCurrentStep(IncidentHandlerCreateSteps.ReviewAndEdit)}>Next</Button>
            </div>
        </div>
    );
};
