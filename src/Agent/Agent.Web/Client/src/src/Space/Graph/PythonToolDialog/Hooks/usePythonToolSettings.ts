import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { array, boolean, number, object, string } from 'yup';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';
import { ENTITY_NAME_MAX_LENGTH } from '../../ExtendedAgentCreationDialog/utils/nameValidation';
import {
    PythonToolDialogMode,
    PythonToolFormProps,
    getDefaultCode,
    scopeConfigsToStrings,
    scopeStringsToConfigs,
} from '../PythonToolUtilities';

export const usePythonToolSettings = (mode: PythonToolDialogMode, tool?: ExtendedTool) => {
    const intl = useIntl();
    const [isSaving, setIsSaving] = useState<boolean>(false);
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const initialValues: PythonToolFormProps = useMemo(() => {
        return {
            name: tool?.name ?? '',
            description: tool?.description ?? '',
            functionCode: tool?.functionCode ?? getDefaultCode(),
            timeoutSeconds: tool?.timeoutSeconds ?? 120,
            parameters: tool?.parameters ?? [],
            authEnabled: tool?.authEnabled ?? false,
            authScopes: scopeStringsToConfigs(tool?.authScopes),
        };
    }, [tool]);

    const validationSchema = useMemo(
        () =>
            object({
                name: string()
                    .required(intl.formatMessage(SreAgentResources.fieldRequired))
                    .test(
                        'validateNameFormat',
                        intl.formatMessage(ExtendedAgentsGraphResources.entityNameValidationMessage, {
                            maxLength: ENTITY_NAME_MAX_LENGTH,
                        }),
                        (name: string) => {
                            return new RegExp(`^[a-zA-Z0-9-]{1,${ENTITY_NAME_MAX_LENGTH}}$`).test(name);
                        }
                    ),
                description: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                functionCode: string()
                    .required(intl.formatMessage(SreAgentResources.fieldRequired))
                    .test(
                        'validateMainFunction',
                        intl.formatMessage(SreAgentResources.pythonToolCreatorMainFunctionRequired),
                        (code: string) => {
                            return code?.includes('def main');
                        }
                    ),
                timeoutSeconds: number()
                    .min(5, intl.formatMessage(SreAgentResources.pythonToolCreatorTimeoutMin))
                    .max(900, intl.formatMessage(SreAgentResources.pythonToolCreatorTimeoutMax))
                    .required(intl.formatMessage(SreAgentResources.fieldRequired)),
                parameters: array().of(
                    object({
                        name: string(),
                        type: string(),
                        description: string(),
                        required: string(),
                    })
                ),
                authEnabled: boolean(),
                authScopes: array()
                    .of(string())
                    .when('authEnabled', {
                        is: true,
                        then: schema => schema.min(1, intl.formatMessage(SreAgentResources.pythonToolAuthScopeRequired)),
                    }),
            }),
        [intl]
    );

    const save = useCallback(
        async (values: PythonToolFormProps) => {
            setIsSaving(true);

            const body: ExtendedTool = {
                name: values.name,
                type: 'PythonFunctionTool',
                description: values.description,
                functionCode: values.functionCode,
                timeoutSeconds: values.timeoutSeconds,
                parameters: values.parameters,
                authEnabled: values.authEnabled,
                authScopes: scopeConfigsToStrings(values.authScopes),
            };

            const isCreate = mode === PythonToolDialogMode.Create;
            const resources = isCreate
                ? {
                      title: ExtendedAgentsGraphResources.createPythonToolTitle,
                      inProgress: ExtendedAgentsGraphResources.createPythonToolInProgress,
                      success: ExtendedAgentsGraphResources.toolCreatedSuccessfully,
                      failure: ExtendedAgentsGraphResources.failedToCreateTool,
                  }
                : {
                      title: ExtendedAgentsGraphResources.updatePythonToolTitle,
                      inProgress: ExtendedAgentsGraphResources.updatePythonToolInProgress,
                      success: ExtendedAgentsGraphResources.toolUpdatedSuccessfully,
                      failure: ExtendedAgentsGraphResources.failedToUpdateTool,
                  };

            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(resources.title),
                intl.formatMessage(resources.inProgress)
            );

            const response = await extendedAgentClient.applyEntity(body, 'tool');

            azPortalContext.stopNotification(
                notificationId,
                response.isSuccessful,
                response.isSuccessful
                    ? intl.formatMessage(resources.success)
                    : intl.formatMessage(resources.failure, { errorMessage: response.error })
            );

            setIsSaving(false);
            return response;
        },
        [azPortalContext, extendedAgentClient, intl, mode]
    );

    return {
        initialValues,
        validationSchema,
        isSaving,
        save,
    };
};
