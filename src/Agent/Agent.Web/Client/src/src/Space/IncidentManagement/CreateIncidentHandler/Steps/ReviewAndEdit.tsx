import { Button } from '@fluentui/react-components';
import { useContext } from 'react';
import { IncidentHandlerCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerCreateContext';

export const ReviewAndEdit = () => {
    const context = useContext(IncidentHandlerCreateContext);
    const { setCurrentStep } = context;
    return (
        <div>
            <div>Content for Step 3: Review + edit</div>
            <div style={{ marginTop: '20px' }}>
                <Button onClick={() => setCurrentStep(IncidentHandlerCreateSteps.GenerateHandler)}>Previous</Button>
            </div>
        </div>
    );
};
