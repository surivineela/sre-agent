import { Button, Checkbox, Dropdown, Field, Input, Option, Text, Textarea } from '@fluentui/react-components';
import {
    Add24Regular,
    CheckmarkCircle16Regular,
    Delete24Regular,
    Info16Regular,
    Warning16Regular,
    Warning24Regular,
} from '@fluentui/react-icons';
import { FC, useCallback, useEffect, useMemo, useState } from 'react';
import { IntlShape, useIntl } from 'react-intl';
import { ExtendedAgentsGraphResources } from '../../../../Strings/SREAgentResources';
import { ExtendedConnector, ExtendedTool, ToolParameter } from '../../../Contracts/ExtendedAgentGraph';
import { useCreationDialogStyles } from '../styles';
import { ENTITY_NAME_MAX_LENGTH, isEntityNameValid, sanitizeEntityName } from '../utils/nameValidation';

interface ToolDetailsStepProps {
    tool: Partial<ExtendedTool>;
    existingConnectors: ExtendedConnector[];
    onChange: (tool: Partial<ExtendedTool>) => void;
    intl: IntlShape;
}

export const ToolDetailsStep: FC<ToolDetailsStepProps> = ({ tool, existingConnectors, onChange, intl }) => {
    const styles = useCreationDialogStyles();
    const internalIntl = useIntl();
    const toolType = tool.type?.trim() || 'KustoTool';
    const isKustoTool = toolType === 'KustoTool';
    const [paramValidationError, setParamValidationError] = useState<string>('');
    const [detectedParams, setDetectedParams] = useState<string[]>([]);
    const [showParamDetectionWarning, setShowParamDetectionWarning] = useState(false);

    const availableConnectors = useMemo(
        () => existingConnectors.filter(connector => connector.name && connector.name.trim() !== ''),
        [existingConnectors]
    );
    const singleConnectorName = availableConnectors.length === 1 ? availableConnectors[0].name?.trim() : undefined;
    const trimmedSelectedConnector = tool.connector?.trim();

    useEffect(() => {
        if (!isKustoTool || !singleConnectorName) {
            return;
        }
        if (trimmedSelectedConnector === singleConnectorName) {
            return;
        }
        onChange({
            ...tool,
            connector: singleConnectorName,
        });
    }, [isKustoTool, singleConnectorName, trimmedSelectedConnector, onChange, tool]);

    const detectParametersInQuery = useCallback((query: string): string[] => {
        const paramPattern = /##([A-Za-z][A-Za-z0-9_]*)##/g;
        const matches = query.matchAll(paramPattern);
        const params = new Set<string>();
        for (const match of matches) {
            params.add(match[1]);
        }
        return Array.from(params);
    }, []);

    useEffect(() => {
        if (!tool.query) {
            setDetectedParams([]);
            setShowParamDetectionWarning(false);
            return;
        }
        const detected = detectParametersInQuery(tool.query);
        setDetectedParams(detected);
        const existingParamNames = (tool.parameters || []).map(p => p.name);
        const missingInDefinition = detected.filter(p => !existingParamNames.includes(p));
        setShowParamDetectionWarning(missingInDefinition.length > 0);
    }, [tool.query, tool.parameters, detectParametersInQuery]);

    const validateParameters = useCallback(() => {
        if (!tool.query || !tool.parameters || tool.parameters.length === 0) {
            setParamValidationError('');
            return;
        }
        const missingParams: string[] = [];
        tool.parameters.forEach(param => {
            if (param.name && !tool.query?.includes(`##${param.name}##`)) {
                missingParams.push(param.name);
            }
        });
        if (missingParams.length > 0) {
            setParamValidationError(`Missing: ${missingParams.join(', ')}`);
        } else {
            setParamValidationError('');
        }
    }, [tool.parameters, tool.query]);

    useEffect(() => {
        validateParameters();
    }, [validateParameters]);

    const addParameter = useCallback(() => {
        const defaultMapTo = toolType === 'KustoTool' ? 'args' : undefined;
        const defaultTarget = toolType === 'KustoTool' ? `dictionary:${defaultMapTo ?? 'args'}:string` : 'direct';
        const newParam: ToolParameter = {
            name: '',
            type: 'string',
            description: '',
            mapTo: defaultMapTo,
            target: defaultTarget,
            required: true,
        };
        onChange({
            ...tool,
            parameters: [...(tool.parameters ?? []), newParam],
        });
    }, [onChange, tool, toolType]);

    const autoAddDetectedParameters = useCallback(() => {
        const existingParamNames = (tool.parameters || [])
            .map(p => p.name?.trim())
            .filter((name): name is string => !!name && name.length > 0);
        const missingParams = detectedParams.filter(p => !existingParamNames.includes(p));
        if (missingParams.length === 0) return;

        const newParams: ToolParameter[] = missingParams.map(name => ({
            name,
            type: 'string',
            description: `Parameter for ${name}`,
            mapTo: toolType === 'KustoTool' ? 'args' : undefined,
            target: toolType === 'KustoTool' ? 'dictionary:args:string' : 'direct',
            required: true,
        }));
        onChange({
            ...tool,
            parameters: [...(tool.parameters ?? []), ...newParams],
        });
        setShowParamDetectionWarning(false);
    }, [detectedParams, onChange, tool, toolType]);

    const updateParameter = useCallback(
        (index: number, updated: Partial<ToolParameter>) => {
            if (!tool.parameters) {
                return;
            }
            const next = [...tool.parameters];
            const merged: ToolParameter = { ...next[index], ...updated };
            if (toolType === 'KustoTool') {
                const mapTo = merged.mapTo?.trim() || 'args';
                const targetParts = typeof merged.target === 'string' ? merged.target.split(':') : [];
                const inferredType = targetParts.length >= 3 ? targetParts[2]?.trim() : undefined;
                const valueType = (merged.type?.trim() || inferredType || 'string') as ToolParameter['type'];
                merged.mapTo = mapTo;
                merged.type = valueType;
                merged.target = `dictionary:${mapTo}:${valueType}`;
            }
            next[index] = merged;
            onChange({
                ...tool,
                parameters: next,
            });
        },
        [onChange, tool, toolType]
    );

    const removeParameter = useCallback(
        (index: number) => {
            if (!tool.parameters) {
                return;
            }
            const next = tool.parameters.filter((_, i) => i !== index);
            onChange({
                ...tool,
                parameters: next,
            });
        },
        [onChange, tool]
    );

    const toolName = tool.name ?? '';
    const toolNameValidationState = toolName.length === 0 || isEntityNameValid(toolName) ? 'none' : 'error';
    const toolNameValidationMessage =
        toolNameValidationState === 'error'
            ? intl.formatMessage(ExtendedAgentsGraphResources.entityNameValidationMessage, {
                  maxLength: ENTITY_NAME_MAX_LENGTH,
              })
            : undefined;

    return (
        <div className={styles.formSection}>
            {/* WORLD-CLASS: Two-column layout for basic info */}
            <div className={styles.formGrid}>
                <Field
                    label={intl.formatMessage(ExtendedAgentsGraphResources.toolName)}
                    required
                    validationState={toolNameValidationState}
                    validationMessage={toolNameValidationMessage}
                >
                    <Input
                        value={tool.name || ''}
                        onChange={(_, data) => onChange({ ...tool, name: sanitizeEntityName(data.value ?? '') })}
                        placeholder="e.g., QueryVMs"
                    />
                    <Text size={200} className={styles.helpText}>
                        {intl.formatMessage(ExtendedAgentsGraphResources.entityNameValidationMessage, {
                            maxLength: ENTITY_NAME_MAX_LENGTH,
                        })}
                    </Text>
                </Field>

                <Field label={intl.formatMessage(ExtendedAgentsGraphResources.toolType)} required>
                    <Dropdown
                        value={toolType}
                        selectedOptions={[toolType]}
                        onOptionSelect={(_, data) => onChange({ ...tool, type: (data.optionValue as string | undefined)?.trim() })}
                    >
                        <Option value="KustoTool">{intl.formatMessage(ExtendedAgentsGraphResources.kustoTool)}</Option>
                    </Dropdown>
                </Field>
            </div>

            {/* COMPACT: Description with optimized height */}
            <div className={styles.compactField}>
                <Field label={intl.formatMessage(ExtendedAgentsGraphResources.description)} required>
                    <Textarea
                        value={tool.description || ''}
                        onChange={(_, data) => onChange({ ...tool, description: data.value })}
                        placeholder="Brief description of what this tool does..."
                        className={styles.compactTextarea}
                        rows={2}
                    />
                </Field>
            </div>

            {isKustoTool && (
                <>
                    {/* TWO-COLUMN: Connector and database with consistent alignment */}
                    <div className={styles.formGrid}>
                        <div className={styles.compactField}>
                            <Field label={internalIntl.formatMessage(ExtendedAgentsGraphResources.connector)} required>
                                <Dropdown
                                    placeholder={availableConnectors.length === 0 ? 'No connectors' : 'Select connector'}
                                    value={trimmedSelectedConnector || ''}
                                    selectedOptions={trimmedSelectedConnector ? [trimmedSelectedConnector] : []}
                                    onOptionSelect={(_, data) =>
                                        onChange({ ...tool, connector: (data.optionValue as string | undefined)?.trim() })
                                    }
                                    disabled={availableConnectors.length === 0}
                                >
                                    {availableConnectors.map(connector => {
                                        const name = connector.name?.trim();
                                        return name ? (
                                            <Option key={name} value={name}>
                                                {name}
                                            </Option>
                                        ) : null;
                                    })}
                                </Dropdown>
                                {availableConnectors.length === 0 && (
                                    <div className={styles.warningMessage}>
                                        <Warning24Regular className={styles.warningIcon} />
                                        <Text size={200}>
                                            {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolNoConnectorsAvailable)}
                                        </Text>
                                    </div>
                                )}
                            </Field>
                        </div>

                        <div className={styles.compactField}>
                            <Field label="Database" required>
                                <Input
                                    value={tool.database || ''}
                                    onChange={(_, data) => onChange({ ...tool, database: data.value })}
                                    placeholder="Database name"
                                />
                                {/* Reserve space for consistent alignment */}
                                <div className={styles.alignmentSpacer}></div>
                            </Field>
                        </div>
                    </div>

                    {/* OPTIMIZED: Query section with smart height */}
                    <div className={styles.compactField}>
                        <Field label="Query" required>
                            <Textarea
                                value={tool.query || ''}
                                onChange={(_, data) => onChange({ ...tool, query: data.value })}
                                placeholder="KQL query with ##ParamName## placeholders"
                                rows={4}
                                style={{
                                    fontFamily: 'Consolas, Monaco, "Courier New", monospace',
                                    fontSize: '13px',
                                    maxHeight: '180px',
                                    resize: 'vertical',
                                }}
                            />
                            {/* COMPACT: Status indicators */}
                            <div className={styles.paramStatusRow}>
                                {paramValidationError && (
                                    <div className={styles.paramStatusDanger}>
                                        <Warning16Regular aria-hidden />
                                        <Text size={200}>{paramValidationError}</Text>
                                    </div>
                                )}
                                {showParamDetectionWarning && detectedParams.length > 0 && (
                                    <div className={styles.paramStatusInfo}>
                                        <Info16Regular aria-hidden />
                                        <Text size={200}>
                                            {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolQueryDetectedParamsLabel, {
                                                count: detectedParams.length,
                                            })}
                                        </Text>
                                        <Button appearance="subtle" size="small" onClick={autoAddDetectedParameters}>
                                            {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolAddAllButton)}
                                        </Button>
                                    </div>
                                )}
                                {detectedParams.length === 0 && tool.query && tool.query.trim() !== '' && !paramValidationError && (
                                    <div className={styles.paramStatusSuccess}>
                                        <CheckmarkCircle16Regular aria-hidden />
                                        <Text size={200}>
                                            {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolNoParamsNeeded)}
                                        </Text>
                                    </div>
                                )}
                            </div>
                        </Field>
                    </div>

                    {/* ULTRA-COMPACT: Parameters */}
                    <div className={styles.compactField}>
                        <Field label="Parameters">
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '6px' }}>
                                {tool.parameters &&
                                    tool.parameters.length > 0 &&
                                    tool.parameters.map((param, index) => (
                                        <div
                                            key={index}
                                            style={{
                                                padding: '8px',
                                                border: `1px solid #e1e1e1`,
                                                borderRadius: '4px',
                                                backgroundColor: '#fafafa',
                                            }}
                                        >
                                            {/* SINGLE ROW: Parameter layout */}
                                            <div style={{ display: 'flex', gap: '6px', alignItems: 'center', marginBottom: '4px' }}>
                                                <Input
                                                    size="small"
                                                    value={param.name}
                                                    onChange={(_, data) => updateParameter(index, { name: data.value })}
                                                    placeholder="Name"
                                                    style={{ flex: '1.2' }}
                                                />
                                                <Dropdown
                                                    size="small"
                                                    value={param.type}
                                                    selectedOptions={[param.type]}
                                                    onOptionSelect={(_, data) =>
                                                        updateParameter(index, { type: data.optionValue as string })
                                                    }
                                                    style={{ flex: '0.8', minWidth: '120px' }}
                                                >
                                                    <Option value="string">
                                                        {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolParamTypeText)}
                                                    </Option>
                                                    <Option value="int">Number</Option>
                                                    <Option value="bool">
                                                        {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolParamTypeYesNo)}
                                                    </Option>
                                                    <Option value="datetime">
                                                        {internalIntl.formatMessage(ExtendedAgentsGraphResources.toolParamTypeDate)}
                                                    </Option>
                                                </Dropdown>
                                                <Checkbox
                                                    label="Req"
                                                    checked={param.required ?? false}
                                                    onChange={(_, data) => updateParameter(index, { required: data.checked === true })}
                                                    style={{ fontSize: '11px' }}
                                                />
                                                <Button
                                                    size="small"
                                                    appearance="subtle"
                                                    icon={<Delete24Regular />}
                                                    onClick={() => removeParameter(index)}
                                                />
                                            </div>
                                            {}
                                            <Input
                                                size="small"
                                                value={param.description || ''}
                                                onChange={(_, data) => updateParameter(index, { description: data.value })}
                                                placeholder="Description (optional)"
                                                style={{ fontSize: '11px' }}
                                            />
                                        </div>
                                    ))}
                                <Button appearance="subtle" icon={<Add24Regular />} onClick={addParameter} size="small">
                                    Add Parameter
                                </Button>
                            </div>
                        </Field>
                    </div>
                </>
            )}
        </div>
    );
};
