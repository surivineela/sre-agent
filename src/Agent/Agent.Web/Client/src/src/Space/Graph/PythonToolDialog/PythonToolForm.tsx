import { Field, Input } from '@fluentui/react-components';
import MonacoEditor from '@monaco-editor/react';
import { useFormikContext } from 'formik';
import { FC, useCallback } from 'react';
import { useIntl } from 'react-intl';
import InputFormik from '../../../Common/Components/Input/InputFormik';
import TextareaFormik from '../../../Common/Components/Textarea/TextareaFormik';
import { SreAgentResources } from '../../../Strings/SREAgentResources';
import { sanitizeEntityName } from '../ExtendedAgentCreationDialog/utils/nameValidation';
import { usePythonToolDialogStyles } from './PythonToolDialog.Styles';
import { PythonToolFormProps, getDefaultCode } from './PythonToolUtilities';

interface PythonToolFormProps2 {
    isGenerating: boolean;
}

export const PythonToolForm: FC<PythonToolFormProps2> = ({ isGenerating }) => {
    const intl = useIntl();
    const styles = usePythonToolDialogStyles();
    const { values, setFieldValue, errors, touched } = useFormikContext<PythonToolFormProps>();

    const handleNameChange = useCallback(
        (_: React.ChangeEvent<HTMLInputElement>, data: { value: string }) => {
            setFieldValue('name', sanitizeEntityName(data.value));
        },
        [setFieldValue]
    );

    const handleTimeoutChange = useCallback(
        (_: React.ChangeEvent<HTMLInputElement>, data: { value: string }) => {
            const parsed = parseInt(data.value) || 120;
            setFieldValue('timeoutSeconds', parsed);
        },
        [setFieldValue]
    );

    const handleCodeChange = useCallback(
        (value: string | undefined) => {
            setFieldValue('functionCode', value || '');
        },
        [setFieldValue]
    );

    return (
        <div className={styles.toolFormLeft}>
            {/* Name and Timeout Row */}
            <div className={styles.headerRow}>
                <InputFormik
                    name="name"
                    label={intl.formatMessage(SreAgentResources.pythonToolBuilderNameLabel)}
                    placeholder={intl.formatMessage(SreAgentResources.pythonToolCreatorToolNamePlaceholder)}
                    orientation="vertical"
                    required
                    onChange={handleNameChange}
                    className={styles.nameField}
                    disabled={isGenerating}
                />
                <Field
                    label={intl.formatMessage(SreAgentResources.pythonToolBuilderTimeoutLabel)}
                    className={styles.timeoutField}
                    validationState={touched.timeoutSeconds && errors.timeoutSeconds ? 'error' : undefined}
                    validationMessage={touched.timeoutSeconds ? errors.timeoutSeconds : undefined}
                >
                    <Input
                        type="number"
                        value={values.timeoutSeconds?.toString() || '120'}
                        onChange={handleTimeoutChange}
                        min={5}
                        max={900}
                        disabled={isGenerating}
                    />
                </Field>
            </div>

            {/* Description */}
            <TextareaFormik
                name="description"
                label={intl.formatMessage(SreAgentResources.pythonToolBuilderDescriptionLabel)}
                placeholder={intl.formatMessage(SreAgentResources.pythonToolCreatorDescriptionPlaceholder)}
                orientation="vertical"
                required
                className={styles.descriptionField}
                resize="vertical"
                disabled={isGenerating}
            />

            {/* Monaco Editor */}
            <div className={styles.editorContainer}>
                <MonacoEditor
                    height="100%"
                    language="python"
                    theme="vs-light"
                    value={values.functionCode || getDefaultCode()}
                    onChange={handleCodeChange}
                    options={{
                        minimap: { enabled: false },
                        lineNumbers: 'on',
                        scrollBeyondLastLine: false,
                        fontSize: 13,
                        fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                        tabSize: 4,
                        wordWrap: 'on',
                        automaticLayout: true,
                        readOnly: isGenerating,
                        padding: { top: 12, bottom: 12 },
                    }}
                />
            </div>
        </div>
    );
};
