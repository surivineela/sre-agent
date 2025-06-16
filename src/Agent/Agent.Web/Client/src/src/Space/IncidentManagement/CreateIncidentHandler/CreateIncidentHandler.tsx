import { Breadcrumb, BreadcrumbButton, BreadcrumbDivider, BreadcrumbItem, tokens } from '@fluentui/react-components';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { QuickEditIncidentHandler } from '../QuickEditIncidentHandler/QuickEditIncidentHandler';
import { FullEditIncidentHandler } from './FullEditIncidentHandler/FullEditIncidentHandler';
import { IncidentHandlerCreateContext, OperationStatus } from './IncidentHandlerCreateContext';
import { useCreateIncidentHandler } from './useCreateIncidentHandler';

interface CreateIncidentHandlerProps {
    exitToHome: () => void;
    setHandlerOperationStatus: React.Dispatch<React.SetStateAction<OperationStatus | undefined>>;
    handlerCreateOrEditInfo: {
        filterId: string;
        handlerId?: string;
    };
}

const CreateIncidentHandler: FC<CreateIncidentHandlerProps> = ({ exitToHome, handlerCreateOrEditInfo, setHandlerOperationStatus }) => {
    const incidentHandlerCreateMetadata = useCreateIncidentHandler(exitToHome, setHandlerOperationStatus, handlerCreateOrEditInfo);
    const { mode } = incidentHandlerCreateMetadata;
    const intl = useIntl();

    return (
        <div style={{ background: tokens.colorNeutralBackground3 }}>
            <Breadcrumb style={{ display: 'flex', height: 50, marginLeft: 16 }}>
                <BreadcrumbItem>
                    <BreadcrumbButton onClick={() => exitToHome()}>
                        {intl.formatMessage(IncidentHandlerCreateResources.incidentManagement)}
                    </BreadcrumbButton>
                </BreadcrumbItem>
                <BreadcrumbDivider />
                <BreadcrumbItem style={{ marginLeft: 6 }}>
                    {intl.formatMessage(
                        mode === 'create'
                            ? IncidentHandlerCreateResources.newCustomHandler
                            : IncidentHandlerCreateResources.editCustomHandler
                    )}
                </BreadcrumbItem>
            </Breadcrumb>
            <div
                style={{
                    borderRadius: tokens.borderRadiusXLarge,
                    boxShadow: tokens.shadow4,
                    marginLeft: 20,
                    height: 'calc(100vh - 95px)',
                    background: tokens.colorNeutralBackground1,
                }}
            >
                <IncidentHandlerCreateContext.Provider value={incidentHandlerCreateMetadata}>
                    {mode === 'quickEdit' ? <QuickEditIncidentHandler /> : <FullEditIncidentHandler />}
                </IncidentHandlerCreateContext.Provider>
            </div>
        </div>
    );
};

export default CreateIncidentHandler;
