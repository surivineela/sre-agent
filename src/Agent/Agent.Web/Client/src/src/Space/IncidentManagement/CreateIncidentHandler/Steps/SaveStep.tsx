import {
    Button,
    Checkbox,
    Field,
    InfoLabel,
    Link,
    MessageBar,
    MessageBarBody,
    MessageBarTitle,
    Radio,
    RadioGroup,
    Text,
} from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { LearnMoreLinks } from '../../../../Common/Constants/Links';
import { AgentMode } from '../../../../Common/Contracts/Azure/SreAgent';
import { AgentTaskResources, IncidentHandlerCreateResources, IncidentManagementResources } from '../../../../Strings/SREAgentResources';
import { useIncidentManagementStyles } from '../../../Styles/IncidentManagement.styles';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

export const SaveStep: FC = () => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const { dirty, isValid, values, setFieldValue } = useFormikContext<IncidentHandlerCreateFormValues>();
    const { generatingUpdatedTools, exitToHome, setCurrentStep, saveHandler } = useContext(IncidentHandlerConsolidatedCreateContext);

    return (
        <>
            <div className={styles.stepContent}>
                <div className={styles.stepContentSection}>
                    <Field label={intl.formatMessage(IncidentHandlerCreateResources.chooseAgentAutonomyLevel)}>
                        <RadioGroup
                            name="agentMode"
                            value={values.agentMode}
                            onChange={(_, data) => setFieldValue('agentMode', data.value)}
                        >
                            <Radio
                                value={AgentMode.review}
                                label={
                                    <>
                                        {intl.formatMessage(IncidentManagementResources.reviewDefault)}
                                        <br />
                                        <Text size={200}>
                                            {intl.formatMessage(IncidentManagementResources.autonomyLevelReviewDescription)}
                                        </Text>
                                    </>
                                }
                            />
                            <Radio
                                value={AgentMode.autonomous}
                                label={
                                    <>
                                        {intl.formatMessage(IncidentManagementResources.autonomousWord)}
                                        <br />
                                        <Text size={200}>
                                            {intl.formatMessage(IncidentManagementResources.autonomyLevelAutonomousDescription)}
                                        </Text>
                                    </>
                                }
                            />
                        </RadioGroup>
                    </Field>
                </div>

                <div className={styles.stepContentSection}>
                    <InfoLabel
                        info={
                            <>
                                {intl.formatMessage(AgentTaskResources.deepInvestigationDescription)}{' '}
                                <Link href={LearnMoreLinks.deepInvestigation} target="_blank">
                                    {intl.formatMessage(AgentTaskResources.learnMoreLinkText)}
                                </Link>
                            </>
                        }
                    >
                        <Text size={300} id="enable-deep-investigation-description">
                            {intl.formatMessage(IncidentHandlerCreateResources.enableDeepInvestigationTitle)}
                        </Text>
                    </InfoLabel>
                    <Checkbox
                        name={'deepInvestigationEnabled'}
                        checked={values.deepInvestigationEnabled}
                        onChange={(_, data) => setFieldValue('deepInvestigationEnabled', !!data.checked)}
                        label={intl.formatMessage(IncidentHandlerCreateResources.enableDeepInvestigationDescription)}
                        labelPosition="after"
                        aria-describedby="enable-deep-investigation-description"
                    />
                    {values.deepInvestigationEnabled && (
                        <MessageBar intent={'warning'} layout={'multiline'} className={styles.inputField}>
                            <MessageBarBody>
                                <MessageBarTitle>{intl.formatMessage(AgentTaskResources.consumptionReminder)}</MessageBarTitle>
                                <div>
                                    {intl.formatMessage(AgentTaskResources.deepInvestigationWarning)}{' '}
                                    <Link href={LearnMoreLinks.usage} target="_blank">
                                        {intl.formatMessage(AgentTaskResources.usageLearnMoreLinkText)}
                                    </Link>
                                </div>
                            </MessageBarBody>
                        </MessageBar>
                    )}
                </div>
            </div>
            <div className={styles.stepFooter}>
                <Button
                    onClick={() => {
                        setCurrentStep(IncidentHandlerCreateSteps.DefineAgentLearningStep);
                    }}
                    disabled={generatingUpdatedTools}
                >
                    {intl.formatMessage(IncidentHandlerCreateResources.back)}
                </Button>
                <Button appearance="primary" onClick={() => saveHandler()} disabled={!dirty || !isValid}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={() => exitToHome()}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};
