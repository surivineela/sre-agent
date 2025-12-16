import { Button } from '@fluentui/react-components';
import { useFormikContext } from 'formik';
import { FC, useContext } from 'react';
import { useIntl } from 'react-intl';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { ReviewAndTestContent } from '../CreateIncidentHandler/Common/ReviewAndTestContent';
import { DirtyStateConfirmationWrapper } from '../CreateIncidentHandler/DirtyStateConfirmationDialog';
import { IncidentHandlerConsolidatedCreateContext } from '../CreateIncidentHandler/IncidentHandlerConsolidatedCreateContext';
import { IncidentHandlerCreateFormValues } from '../CreateIncidentHandler/IncidentHandlerCreateFormValues';
import QuickEditIncidentHandlerToolbar from './QuickEditIncidentHandlerToolbar';

export const QuickEditIncidentHandlerConsolidated: FC = () => {
    const { dirty } = useFormikContext<IncidentHandlerCreateFormValues>();
    const { exitToHome, goToFullEditMode, deleteHandler, saveHandler, exportHandler } = useContext(
        IncidentHandlerConsolidatedCreateContext
    );

    const intl = useIntl();

    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'column',
                height: '100%',
            }}
        >
            <QuickEditIncidentHandlerToolbar
                isDirty={dirty}
                onRegenerateClick={goToFullEditMode}
                onExportClick={() => exportHandler()}
                onDeleteClick={() => deleteHandler()}
            />
            <div
                style={{
                    display: 'flex',
                    flexDirection: 'column',
                    height: '100%',
                    margin: '20px 20px 0 30px',
                }}
            >
                <ReviewAndTestContent />
                <div
                    style={{
                        display: 'flex',
                        gap: 10,
                        marginTop: 'auto',
                        paddingTop: 20,
                        paddingBottom: 20,
                    }}
                >
                    <Button appearance="primary" onClick={() => saveHandler()} disabled={!dirty}>
                        {intl.formatMessage(IncidentHandlerCreateResources.save)}
                    </Button>
                    <DirtyStateConfirmationWrapper isDirty={dirty} onConfirm={exitToHome}>
                        <Button>{intl.formatMessage(IncidentHandlerCreateResources.cancel)}</Button>
                    </DirtyStateConfirmationWrapper>
                </div>
            </div>
        </div>
    );
};
