import { Breadcrumb, BreadcrumbButton, BreadcrumbDivider, BreadcrumbItem, tokens } from '@fluentui/react-components';
import { Formik, FormikErrors, useFormikContext } from 'formik';
import { FC, useCallback, useState } from 'react';
import { useIntl } from 'react-intl';
import { AgentMode } from '../../../Common/Contracts/Azure/SreAgent';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { useIncidentFilterFields } from '../../Hooks/useIncidentFilterFields';
import { QuickEditIncidentHandlerConsolidated } from '../QuickEditIncidentHandler/QuickEditIncidentHandlerConsolidated';
import { HANDLER_TOOL_LIMIT, HandlerCreateOrEditInfo, OperationStatus } from './Contracts';
import { DirtyStateConfirmationWrapper } from './DirtyStateConfirmationDialog';
import { FullEditIncidentHandlerConsolidated } from './FullEditIncidentHandler/FullEditIncidentHandlerConsolidated';
import { IncidentHandlerConsolidatedCreateContext } from './IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from './IncidentHandlerCreateFormValues';
import { DirtyStateNavigationConfirmDialog } from './NavigationConfirmDialog';
import { useConsolidatedCreateIncidentHandler } from './useConsolidatedCreateIncidentHandler';

interface CreateIncidentHandlerProps {
    exitToHome: () => void;
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
        agentMode: handlerCreateOrEditInfo?.filter?.agentMode || AgentMode.review,

        incidentIds: undefined,
        customInstructions: undefined,
        toolNames: undefined,
        incidentProcessingGuide: undefined,

        useCustomHandler: false,
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
    const { dirty } = useFormikContext<IncidentHandlerCreateFormValues>();
    const incidentHandlerCreateMetadata = useConsolidatedCreateIncidentHandler(
        exitToHome,
        setHandlerOperationStatus,
        handlerCreateOrEditInfo,
        setInitialValues
    );
    const { incidentTypeOptions, impactedServiceOptions, priorityOptions } = useIncidentFilterFields();

    const { filterMode, handlerMode } = incidentHandlerCreateMetadata;
    const intl = useIntl();

    return (
        <div
            style={{
                background: tokens.colorNeutralBackground3,
                height: 'calc(100vh - 45px)',
            }}
        >
            <DirtyStateNavigationConfirmDialog isDirty={dirty} />
            <Breadcrumb style={{ display: 'flex', height: 50, marginLeft: 16 }}>
                <BreadcrumbItem>
                    <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                        <BreadcrumbButton>{intl.formatMessage(IncidentHandlerCreateResources.incidentManagement)}</BreadcrumbButton>
                    </DirtyStateConfirmationWrapper>
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>
                    {intl.formatMessage(
                        filterMode === 'create'
                            ? IncidentHandlerCreateResources.newIncidentHandler
                            : IncidentHandlerCreateResources.editIncidentHandler
                    )}
                </BreadcrumbItem>
            </Breadcrumb>
            <div
                style={{
                    borderRadius: tokens.borderRadiusXLarge,
                    boxShadow: tokens.shadow4,
                    marginLeft: 20,
                    marginRight: 20,
                    height: 'calc(100% - 55px)',
                    background: tokens.colorNeutralBackground1,
                }}
            >
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
        </div>
    );
};
