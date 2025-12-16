import { Button, Field, Radio, RadioGroup } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export enum IncludedIncidents {
    FutureOnly = 'futureOnly',
    PastAndFuture = 'pastAndFuture',
}

export const DeployStep: FC = () => {
    const intl = useIntl();
    const { setCurrentStep, exitToHome, saveHandler } = useContext(IncidentHandlerConsolidatedCreateContext);
    const { values, setFieldValue, dirty } = useFormikContext<IncidentHandlerCreateFormValues>();

    const IncludePastIncidentsOptions = useMemo(
        () => [
            { key: IncludedIncidents.FutureOnly, display: intl.formatMessage(IncidentHandlerCreateResources.includedIncidentsFutureOnly) },
            {
                key: IncludedIncidents.PastAndFuture,
                display: intl.formatMessage(IncidentHandlerCreateResources.includedIncidentsPastAndFuture),
            },
        ],
        [intl]
    );

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
            <form style={{ display: 'flex', flexDirection: 'column', gap: 16 }}>
                <Field label={intl.formatMessage(IncidentHandlerCreateResources.includedIncidentsLabel)}>
                    <RadioGroup
                        layout="vertical"
                        name={'includePastIncidents'}
                        value={values.includePastIncidents ? IncludedIncidents.PastAndFuture : IncludedIncidents.FutureOnly}
                        onChange={(_, data) =>
                            setFieldValue('includePastIncidents', data.value === IncludedIncidents.FutureOnly ? false : true)
                        }
                    >
                        {IncludePastIncidentsOptions.map(option => (
                            <Radio value={option.key} label={option.display} />
                        ))}
                    </RadioGroup>
                </Field>
            </form>
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
                        setCurrentStep(
                            values.useCustomHandler
                                ? IncidentHandlerCreateSteps.ReviewAndTestStep
                                : IncidentHandlerCreateSteps.PreviewIncidentsStep
                        );
                    }}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.back)}
                </Button>
                <Button appearance="primary" onClick={() => saveHandler()} disabled={!dirty}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={() => exitToHome()}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </div>
    );
};
