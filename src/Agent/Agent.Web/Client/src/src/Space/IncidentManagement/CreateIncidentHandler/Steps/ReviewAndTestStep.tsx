import { Button, tokens } from '@fluentui/react-components';
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
        <>
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    padding: '0px 20px 0px 20px',
                    gap: '20px',
                    height: 'calc(100% - 74px)',
                    overflowY: 'auto',
                }}
            >
                <ReviewAndTestContent />
            </div>
            <div
                style={{
                    display: 'flex',
                    gap: 10,
                    padding: 20,
                    borderTop: `1px solid ${tokens.colorNeutralStroke2}`,
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
        </>
    );
};
