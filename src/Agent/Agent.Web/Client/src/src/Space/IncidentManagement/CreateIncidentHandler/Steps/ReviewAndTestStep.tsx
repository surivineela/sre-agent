import { Button, Tab, TabList, tokens } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../../Strings/SREAgentResources';
import useWindowSize from '../../../Hooks/useWindowSize';
import { ReviewAndTestContent, ReviewAndTestView } from '../Common/ReviewAndTestContent';
import { DirtyStateConfirmationWrapper } from '../DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from '../IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../IncidentHandlerCreateFormValues';

const buttonsDivHeight = 74;
const tabsDivHeight = 49;
const tabViewThreshold = 1366;

export const ReviewAndTestStep: FC = () => {
    const { isValid, dirty } = useFormikContext<IncidentHandlerCreateFormValues>();
    const { generatingUpdatedTools, exitToHome, setCurrentStep, saveHandler } = useContext(IncidentHandlerConsolidatedCreateContext);
    const [selectedTab, setSelectedTab] = useState<ReviewAndTestView>('review');
    const intl = useIntl();
    const { width } = useWindowSize();

    const showTabs = useMemo(() => {
        if (!width) {
            return false;
        }
        return width <= tabViewThreshold;
    }, [width]);

    const panelHeight = useMemo(() => {
        const nonPanelHeight = buttonsDivHeight + (showTabs ? tabsDivHeight : 0);
        return `calc(100% - ${nonPanelHeight}px)`;
    }, [showTabs]);

    return (
        <>
            {showTabs && (
                <TabList
                    selectedValue={selectedTab}
                    onTabSelect={(_, data) => setSelectedTab(data.value as ReviewAndTestView)}
                    style={{ padding: '5px 0px 0px 10px' }}
                >
                    <Tab id="Review" value={'review'}>
                        {intl.formatMessage(IncidentHandlerCreateResources.reviewCustomInstructionsTitle)}
                    </Tab>
                    <Tab id="Test" value={'test'}>
                        {intl.formatMessage(IncidentHandlerCreateResources.testHandlerTitle)}
                    </Tab>
                </TabList>
            )}
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    padding: '0px 20px 0px 20px',
                    height: panelHeight,
                    overflowY: 'auto',
                }}
            >
                <ReviewAndTestContent view={showTabs ? selectedTab : undefined} />
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
                <Button appearance="primary" onClick={saveHandler} disabled={!dirty || !isValid}>
                    {intl.formatMessage(IncidentHandlerCreateResources.save)}
                </Button>
                <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                    <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                </DirtyStateConfirmationWrapper>
            </div>
        </>
    );
};
