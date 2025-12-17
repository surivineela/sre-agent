import {
    Button,
    Dialog,
    DialogActions,
    DialogBody,
    DialogContent,
    DialogSurface,
    DialogTitle,
    DialogTrigger,
    ToolbarButton,
} from '@fluentui/react-components';
import { Dismiss24Regular } from '@fluentui/react-icons';
import { Formik } from 'formik';
import { FC, useEffect, useState } from 'react';
import { useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../Contracts/ExtendedAgentGraph';
import { usePythonToolSettings } from './Hooks/usePythonToolSettings';
import { usePythonToolDialogStyles } from './PythonToolDialog.Styles';
import { PythonToolForm } from './PythonToolForm';
import { PythonToolTestPanel } from './PythonToolTestPanel';
import { PythonToolDialogMode, PythonToolFormProps, getPythonToolFingerprint } from './PythonToolUtilities';

interface PythonToolDialogProps {
    isDialogOpen: boolean;
    setIsDialogOpen: (open: boolean) => void;
    agentName?: string;
    addToolsToAgent: (agentName: string, toolsNames: string[]) => void;
    refresh?: () => void;
    pythonTool?: ExtendedTool;
    mode: PythonToolDialogMode;
}

export const PythonToolDialog: FC<PythonToolDialogProps> = ({
    isDialogOpen,
    setIsDialogOpen,
    agentName,
    addToolsToAgent,
    refresh,
    pythonTool,
    mode,
}) => {
    const intl = useIntl();
    const styles = usePythonToolDialogStyles();
    const {
        initialValues,
        validationSchema,
        save: savePythonToolSettings,
    } = usePythonToolSettings(mode, mode === PythonToolDialogMode.Edit ? pythonTool : undefined);

    const [hasSuccessRunTest, setHasSuccessRunTest] = useState<boolean>(false);
    const [lastTestedFingerprint, setLastTestedFingerprint] = useState<string | null>(null);
    const [isGenerating, setIsGenerating] = useState<boolean>(false);

    // Reset test state when dialog opens
    useEffect(() => {
        if (isDialogOpen) {
            setHasSuccessRunTest(false);
            setLastTestedFingerprint(null);
            setIsGenerating(false);
        }
    }, [isDialogOpen]);

    return (
        <Dialog open={isDialogOpen} onOpenChange={(_, data) => setIsDialogOpen(data.open)} modalType="alert">
            <Formik<PythonToolFormProps>
                initialValues={initialValues}
                validationSchema={validationSchema}
                enableReinitialize={true}
                onSubmit={async (values: PythonToolFormProps) => {
                    const response = await savePythonToolSettings(values);
                    if (response?.isSuccessful) {
                        setIsDialogOpen(false);
                        if (mode === PythonToolDialogMode.Create) {
                            if (agentName) {
                                await addToolsToAgent(agentName, [values.name]);
                            }
                        } else {
                            refresh?.();
                        }
                    }
                }}
            >
                {({ submitForm, dirty, isValid, values }) => {
                    // Check if code changed after successful test
                    const currentFingerprint = getPythonToolFingerprint(values.functionCode, values.parameters, values.timeoutSeconds);
                    const testStillValid = hasSuccessRunTest && currentFingerprint === lastTestedFingerprint;
                    const canSubmit = dirty && isValid && testStillValid;

                    return (
                        <DialogSurface className={styles.dialogSurface}>
                            <DialogBody className={styles.dialogBody}>
                                <div className={styles.dialogTitleWrapper}>
                                    <DialogTitle
                                        className={styles.dialogTitle}
                                        action={
                                            <ToolbarButton
                                                aria-label={intl.formatMessage(SreAgentResources.close)}
                                                appearance="transparent"
                                                icon={<Dismiss24Regular />}
                                                onClick={() => setIsDialogOpen(false)}
                                            />
                                        }
                                    >
                                        {mode === PythonToolDialogMode.Create
                                            ? intl.formatMessage(ExtendedAgentsGraphResources.createPythonTool)
                                            : intl.formatMessage(ExtendedAgentsGraphResources.editPythonTool)}
                                    </DialogTitle>
                                </div>
                                <DialogContent>
                                    <div className={styles.toolForm}>
                                        <PythonToolForm isGenerating={isGenerating} />
                                        <div className={styles.toolFormDivider} />
                                        <PythonToolTestPanel
                                            hasSuccessRunTest={hasSuccessRunTest}
                                            setHasSuccessRunTest={setHasSuccessRunTest}
                                            lastTestedFingerprint={lastTestedFingerprint}
                                            setLastTestedFingerprint={setLastTestedFingerprint}
                                            isGenerating={isGenerating}
                                            setIsGenerating={setIsGenerating}
                                        />
                                    </div>
                                </DialogContent>
                                <DialogActions className={styles.dialogActions}>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="primary" onClick={submitForm} disabled={!canSubmit}>
                                            {mode === PythonToolDialogMode.Create
                                                ? intl.formatMessage(ExtendedAgentsGraphResources.createTool)
                                                : intl.formatMessage(SreAgentResources.save)}
                                        </Button>
                                    </DialogTrigger>
                                    <DialogTrigger disableButtonEnhancement>
                                        <Button appearance="secondary" onClick={e => e.stopPropagation()}>
                                            {intl.formatMessage(SreAgentResources.cancel)}
                                        </Button>
                                    </DialogTrigger>
                                </DialogActions>
                            </DialogBody>
                        </DialogSurface>
                    );
                }}
            </Formik>
        </Dialog>
    );
};

export { PythonToolDialogMode };
