import { Button } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { ReviewAndTestContent } from '../Common/ReviewAndTestContent';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const ReviewAndTestStep: FC = () => {
    const { dirty } = useFormikContext<IncidentHandlerCreateFormValues>();
    const { generatingUpdatedTools, exitToHome, setCurrentStep, saveHandler } = useContext(IncidentHandlerConsolidatedCreateContext);
    const intl = useIntl();

    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'column',
                margin: '20px 20px 0 20px',
                gap: '20px',
                height: 'calc(100% - 20px)',
            }}
        >
            <ReviewAndTestContent />
            <div
                style={{
                    display: 'flex',
                    gap: 10,
                    marginTop: 'auto',
                    paddingBottom: 20,
                }}
            >
                <Button
                    onClick={() => {
                        setCurrentStep(IncidentHandlerCreateSteps.IncidentsAndGuidanceStep);
                    }}
                    disabled={generatingUpdatedTools}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.back)}
                </Button>
                <Button appearance="primary" onClick={saveHandler} disabled={!dirty}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </div>
    );
};
