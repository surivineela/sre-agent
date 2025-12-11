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
import { IncidentHandlerConsolidatedCreateContext } from './IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from './IncidentHandlerCreateFormValues';
import { DirtyStateNavigationConfirmDialog } from './NavigationConfirmDialog';
import { useConsolidatedCreateIncidentHandler } from './useConsolidatedCreateIncidentHandler';

interface CreateIncidentHandlerProps {
    exitToHome: (filterName?: string, handlerId?: string, isNew?: boolean) => void;
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>;
    handlerCreateOrEditInfo: HandlerCreateOrEditInfo;
}

const CreateIncidentHandlerConsolidated: FC<CreateIncidentHandlerProps> = props => {
    const intl = useIntl();
    const { handlerCreateOrEditInfo } = props;
    const [initialValues, setInitialValues] = useState<IncidentHandlerCreateFormValues>({
        filterName: handlerCreateOrEditInfo?.filter?.id || '',
        incidentType: handlerCreateOrEditInfo?.filter?.incidentType || undefined,
        impactedService: handlerCreateOrEditInfo?.filter?.impactedService || undefined,
        priority: handlerCreateOrEditInfo?.filter?.priority || undefined,
        titleContains: handlerCreateOrEditInfo?.filter?.titleContains || undefined,
        agentMode: handlerCreateOrEditInfo?.filter?.agentMode || AgentMode.autonomous,
        owningTeamId: handlerCreateOrEditInfo?.filter?.owningTeamId || undefined,
        createdBy: handlerCreateOrEditInfo?.filter?.createdBy || undefined,
        monitorId: handlerCreateOrEditInfo?.filter?.monitorId || undefined,
        handlingAgent: handlerCreateOrEditInfo?.filter?.handlingAgent || handlerCreateOrEditInfo?.subAgentTriggerInfo?.preSelectedAgent,

        incidentIds: undefined,
        customInstructions: undefined,
        toolNames: undefined,
        incidentProcessingGuide: undefined,

        useCustomHandler: !!handlerCreateOrEditInfo.handlerId,
        deepInvestigationEnabled: handlerCreateOrEditInfo?.filter?.deepInvestigationEnabled || false,
        includePastIncidents: false,
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
}) => {
    const intl = useIntl();
    const styles = useIncidentManagementStyles();
    const { dirty } = useFormikContext<IncidentHandlerCreateFormValues>();
    const incidentHandlerCreateMetadata = useConsolidatedCreateIncidentHandler(
        exitToHome,
        setHandlerOperationStatus,
        handlerCreateOrEditInfo,
        setInitialValues
    );
    const { incidentTypeOptions, impactedServiceOptions, priorityOptions } = useIncidentFilterFields();
    const { filterMode, handlerMode } = incidentHandlerCreateMetadata;

    const innerComponent = useMemo(() => {
        return (
            <div className={styles.navPanelContent}>
                <DirtyStateNavigationConfirmDialog isDirty={dirty} />
                <IncidentHandlerConsolidatedCreateContext.Provider
                    value={{
                        ...incidentHandlerCreateMetadata,
                        incidentTypeOptions,
                        impactedServiceOptions,
                        priorityOptions,
                    }}
                >
                    {handlerMode === 'quickEdit' ? <QuickEditIncidentHandlerConsolidated /> : <FullEditIncidentHandlerConsolidated />}
                </IncidentHandlerConsolidatedCreateContext.Provider>
            </div>
        );
    }, [dirty, incidentHandlerCreateMetadata, incidentTypeOptions, impactedServiceOptions, priorityOptions, handlerMode, styles]);

    if (incidentHandlerCreateMetadata.isSubagentTrigger) {
        return innerComponent;
    }

    return filterMode === 'create' ? (
        <BreadcrumbNavigation
            title={intl.formatMessage(IncidentHandlerCreateResources.newIncidentHandler)}
            parentTitle={intl.formatMessage(IncidentManagementResources.handlerConfiguration)}
            onParentClick={exitToHome}
            isDirty={dirty}
        >
            {innerComponent}
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
