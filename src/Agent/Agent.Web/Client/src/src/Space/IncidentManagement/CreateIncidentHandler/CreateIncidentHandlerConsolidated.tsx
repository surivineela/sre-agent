import { tokens } from '@fluentui/react-components';
import { Formik, FormikErrors, useFormikContext } from 'formik';
import { FC, useCallback, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentMode } from '../../../Common/Contracts/Azure/SreAgent';
import { IncidentHandlerCreateResources, IncidentManagementResources } from '../../../Strings/SREAgentResources';
import { useIncidentFilterFields } from '../../Hooks/useIncidentFilterFields';
import { useIncidentManagementStyles } from '../../Styles/IncidentManagement.styles';
import BreadcrumbNavigation from '../Common/BreadcrumbNavigation';
import TitleBarNavigation from '../Common/TitleBarNavigation';
import { QuickEditIncidentHandlerConsolidated } from '../QuickEditIncidentHandler/QuickEditIncidentHandlerConsolidated';
import { HANDLER_TOOL_LIMIT, HandlerCreateOrEditInfo, OperationStatus } from './Contracts';
import { FullEditIncidentHandlerConsolidated } from './FullEditIncidentHandler/FullEditIncidentHandlerConsolidated';
import { IncidentHandlerConsolidatedCreateContext, IncidentHandlerCreateSteps } from './IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from './IncidentHandlerCreateFormValues';
import { DirtyStateNavigationConfirmDialog } from './NavigationConfirmDialog';
import { useConsolidatedCreateIncidentHandler } from './useConsolidatedCreateIncidentHandler';
import { useConsolidatedCreateIncidentHandlerforAgentBuilder } from './useConsolidatedCreateIncidentHandlerForAgentBuilder';

interface CreateIncidentHandlerProps {
    exitToHome: (filterName?: string, handlerId?: string, isNew?: boolean) => void;
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>;
    handlerCreateOrEditInfo: HandlerCreateOrEditInfo;
    suggestionsPanel?: React.ReactNode;
}

const CreateIncidentHandlerConsolidated: FC<CreateIncidentHandlerProps> = props => {
    const intl = useIntl();
    const { handlerCreateOrEditInfo } = props;
    const [initialValues, setInitialValues] = useState<IncidentHandlerCreateFormValues>({
        filterName: handlerCreateOrEditInfo?.filter?.id || '',
        incidentType: handlerCreateOrEditInfo?.filter?.incidentType || undefined,
        impactedService: handlerCreateOrEditInfo?.filter?.impactedService || undefined,
        priorities: handlerCreateOrEditInfo?.filter?.priorities || undefined,
        titleContains: handlerCreateOrEditInfo?.filter?.titleContains || undefined,
        agentMode: handlerCreateOrEditInfo?.filter?.agentMode || AgentMode.review,
        owningTeamId: handlerCreateOrEditInfo?.filter?.owningTeamId || undefined,
        createdBy: handlerCreateOrEditInfo?.filter?.createdBy || undefined,
        monitorId: handlerCreateOrEditInfo?.filter?.monitorId || undefined,
        handlingAgent: handlerCreateOrEditInfo?.filter?.handlingAgent || handlerCreateOrEditInfo?.subAgentTriggerInfo?.preSelectedAgent,
        // Phase 2: Initialize handlingAgents and triggers from filter
        handlingAgents: handlerCreateOrEditInfo?.filter?.handlingAgents || undefined,
        triggers: handlerCreateOrEditInfo?.filter?.triggers || undefined,

        incidentIds: undefined,
        customInstructions: undefined,
        toolNames: undefined,
        incidentProcessingGuide: undefined,

        useCustomHandler: !!handlerCreateOrEditInfo.handlerId,
        deepInvestigationEnabled: handlerCreateOrEditInfo?.filter?.deepInvestigationEnabled || false,
        includePastIncidents: false,

        isIncidentTriggerWithLearnings: !!handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo,
        extendedAgents: handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo?.extendedAgents || [],
        extendedTools: handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo?.extendedTools || [],
        systemTools: handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo?.systemTools || [],
        mcpConnections: handlerCreateOrEditInfo?.incidentTriggerWithLearningsInfo?.mcpConnections || [],
    });

    const validate = useCallback(
        (formValues: IncidentHandlerCreateFormValues): Promise<FormikErrors<IncidentHandlerCreateFormValues>> => {
            if ((formValues.toolNames?.length || 0) > HANDLER_TOOL_LIMIT) {
                return Promise.resolve({
                    toolNames: intl.formatMessage(IncidentHandlerCreateResources.maximumToolsErrorMessage, {
                        maxTools: HANDLER_TOOL_LIMIT,
                    }),
                });
            }
            return Promise.resolve({});
        },
        [intl]
    );

    return (
        <Formik initialValues={initialValues} onSubmit={() => {}} enableReinitialize={true} validate={validate}>
            <CreateIncidentHandlerConsolidatedInner {...props} setInitialValues={setInitialValues} />
        </Formik>
    );
};

export default CreateIncidentHandlerConsolidated;

interface CreateIncidentHandlerInnerProps extends CreateIncidentHandlerProps {
    setInitialValues: React.Dispatch<React.SetStateAction<IncidentHandlerCreateFormValues>>;
}

const CreateIncidentHandlerConsolidatedInner: FC<CreateIncidentHandlerInnerProps> = ({
    exitToHome,
    handlerCreateOrEditInfo,
    setHandlerOperationStatus,
    setInitialValues,
    suggestionsPanel,
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const { dirty, values } = useFormikContext<IncidentHandlerCreateFormValues>();

    const incidentHandlerForAgentBuilder = useConsolidatedCreateIncidentHandlerforAgentBuilder(
        exitToHome,
        setHandlerOperationStatus,
        handlerCreateOrEditInfo,
        setInitialValues
    );

    const incidentHandlerCreateMetadata = useConsolidatedCreateIncidentHandler(
        exitToHome,
        setHandlerOperationStatus,
        handlerCreateOrEditInfo,
        setInitialValues
    );

    const activeHandlerMetadata = useMemo(
        () => (values.isIncidentTriggerWithLearnings ? incidentHandlerForAgentBuilder : incidentHandlerCreateMetadata),
        [values.isIncidentTriggerWithLearnings, incidentHandlerForAgentBuilder, incidentHandlerCreateMetadata]
    );

    const { incidentTypeOptions, impactedServiceOptions, priorityOptions, titleContainsOptions, filterFieldOptionsLoading } =
        useIncidentFilterFields();
    const { filterMode, handlerMode, currentStep } = activeHandlerMetadata;

    const shouldShowSuggestions = suggestionsPanel && currentStep === IncidentHandlerCreateSteps.IncidentTriggerStep;

    const innerComponent = useMemo(() => {
        return (
            <div className={shouldShowSuggestions ? styles.navPanelContentWithSidebar : styles.navPanelContent}>
                <DirtyStateNavigationConfirmDialog isDirty={dirty} />
                <IncidentHandlerConsolidatedCreateContext.Provider
                    value={{
                        ...activeHandlerMetadata,
                        incidentTypeOptions,
                        impactedServiceOptions,
                        priorityOptions,
                        titleContainsOptions,
                        filterFieldOptionsLoading,
                    }}
                >
                    <div className={styles.mainFormContent}>
                        {handlerMode === 'quickEdit' ? <QuickEditIncidentHandlerConsolidated /> : <FullEditIncidentHandlerConsolidated />}
                    </div>
                    {shouldShowSuggestions && suggestionsPanel}
                </IncidentHandlerConsolidatedCreateContext.Provider>
            </div>
        );
    }, [
        dirty,
        activeHandlerMetadata,
        incidentTypeOptions,
        impactedServiceOptions,
        priorityOptions,
        titleContainsOptions,
        filterFieldOptionsLoading,
        handlerMode,
        styles,
        suggestionsPanel,
        shouldShowSuggestions,
    ]);

    // When rendering in a dialog (renderSuggestionsPanel provided) or for subagent triggers,
    // return inner component directly without breadcrumb navigation
    if (activeHandlerMetadata.isSubagentTrigger || values.isIncidentTriggerWithLearnings || suggestionsPanel) {
        return innerComponent;
    }

    return filterMode === 'create' ? (
        <BreadcrumbNavigation
            title={intl.formatMessage(IncidentHandlerCreateResources.addIncidentResponsePlan)}
            parentTitle={intl.formatMessage(IncidentManagementResources.handlerConfiguration)}
            onParentClick={exitToHome}
            isDirty={dirty}
        >
            <div style={{ borderTop: `1px solid ${tokens.colorNeutralStroke1}`, height: '100%' }}>{innerComponent}</div>
        </BreadcrumbNavigation>
    ) : (
        <TitleBarNavigation
            title={intl.formatMessage(IncidentHandlerCreateResources.editIncidentHandler)}
            onBackClick={exitToHome}
            isDirty={dirty}
        >
            {innerComponent}
        </TitleBarNavigation>
    );
};
