import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    Field,
    Input,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik, FormikHelpers, useFormikContext } from 'formik';
import { Dispatch, FC, useCallback, useEffect, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { SubAgent } from '../../Common/Contracts/SubAgent';
import { SreAgentResources, SubAgentsResources } from '../../Strings/SREAgentResources';

interface CreateSubAgentProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    createSubAgent: (subAgent: SubAgentFormProps) => Promise<void>;
    isOperationInProgress?: boolean;
    existingSubAgents?: SubAgent[];
}

interface CreateSubAgentFormProps {
    isDialogOpen: boolean;
    setIsDialogOpen: Dispatch<React.SetStateAction<boolean>>;
    isOperationInProgress?: boolean;
    existingSubAgents?: SubAgent[];
}

export interface SubAgentFormProps {
    name: string;
}

export const CreateSubAgentDialog: FC<CreateSubAgentProps> = ({
    isDialogOpen,
    setIsDialogOpen,
    createSubAgent,
    isOperationInProgress = false,
    existingSubAgents,
}) => {
    const handleSubmit = useCallback(
        async (values: SubAgentFormProps, formikHelpers: FormikHelpers<SubAgentFormProps>) => {
            await createSubAgent(values);

            formikHelpers.resetForm();
        },
        [createSubAgent]
    );

    return (
        <Formik<SubAgentFormProps> initialValues={{ name: '' }} enableReinitialize={true} onSubmit={handleSubmit}>
            <CreateSubAgentForm
                isDialogOpen={isDialogOpen}
                setIsDialogOpen={setIsDialogOpen}
                isOperationInProgress={isOperationInProgress}
                existingSubAgents={existingSubAgents}
            />
        </Formik>
    );
};

const CreateSubAgentForm = ({
    isDialogOpen,
    setIsDialogOpen,
    isOperationInProgress = false,
    existingSubAgents,
}: CreateSubAgentFormProps) => {
    const intl = useIntl();
    const [nameError, setNameError] = useState<string | undefined>();

    const { values, setFieldValue, submitForm, resetForm } = useFormikContext<SubAgentFormProps>();

    useEffect(() => {
        if (!values.name) {
            setNameError(undefined);
            return;
        }

        const isDuplicate = existingSubAgents?.some(subAgent => subAgent.agentName.toLowerCase() === values.name.toLowerCase());
        setNameError(isDuplicate ? intl.formatMessage(SubAgentsResources.duplicateNameError) : undefined);
    }, [values.name, existingSubAgents, intl]);

    const isSaveDisabled = useMemo((): boolean => {
        return !values.name || isOperationInProgress || !!nameError;
    }, [values.name, isOperationInProgress, nameError]);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)}>
            <DialogSurface>
                <DialogBody>
                    <DialogTitle
                        action={<Button appearance="transparent" icon={<Dismiss24Regular />} onClick={() => setIsDialogOpen(false)} />}
                    >
                        {intl.formatMessage(SubAgentsResources.createSubAgent)}
                    </DialogTitle>
                    <DialogContent>
                        <form style={{ display: 'flex', flexDirection: 'column', gap: 16, paddingBottom: 16 }}>
                            <Field
                                label={intl.formatMessage(SreAgentResources.name)}
                                required
                                validationState={nameError ? 'error' : 'none'}
                                validationMessage={nameError}
                            >
                                <Input
                                    name="name"
                                    value={values.name}
                                    onChange={(_, data) => setFieldValue('name', data.value)}
                                    placeholder={intl.formatMessage(SubAgentsResources.namePlaceholder)}
                                    disabled={isOperationInProgress}
                                />
                            </Field>
                        </form>
                    </DialogContent>
                    <DialogActions>
                        <Button appearance="primary" type="submit" onClick={() => submitForm()} disabled={isSaveDisabled}>
                            {intl.formatMessage(SreAgentResources.save)}
                        </Button>
                        <Button
                            appearance="secondary"
                            onClick={() => {
                                setIsDialogOpen(false);
                                resetForm();
                            }}
                            disabled={isOperationInProgress}
                        >
                            {intl.formatMessage(SreAgentResources.cancel)}
                        </Button>
                    </DialogActions>
                </DialogBody>
            </DialogSurface>
        </Dialog>
    );
};
