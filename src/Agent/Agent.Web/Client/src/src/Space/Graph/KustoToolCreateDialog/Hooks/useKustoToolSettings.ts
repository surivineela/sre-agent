import { useCallback, useContext, useMemo, useState } from 'react';
import { useIntl } from 'react-intl';
import { array, mixed, object, string } from 'yup';
import { AzPortalContext } from '../../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { getErrorMessageOrStringify } from '../../../../Common/Clients/ArmClient';
import { ExtendedAgentClient } from '../../../../Common/Clients/ExtendedAgentClient';
import { ExtendedAgentsGraphResources, SreAgentResources } from '../../../../Strings/SREAgentResources';
import { ExtendedTool } from '../../../Contracts/ExtendedAgentGraph';
import { ENTITY_NAME_MAX_LENGTH } from '../../ExtendedAgentCreationDialog/utils/nameValidation';
import { KustoToolFormProps } from '../KustoToolUtilities';

export const useKustoToolSettings = () => {
    const intl = useIntl();
    const [isSaving, setIsSaving] = useState<boolean>(false);
    const azPortalContext = useContext(AzPortalContext);
    const { sreAgentEndpoint } = useContext(EnvironmentContext);
    const extendedAgentClient = ExtendedAgentClient.getInstance(sreAgentEndpoint);

    const initialValues: KustoToolFormProps = useMemo(() => {
        return {
            name: '',
            description: '',
            connector: '',
            database: '',
            query: '',
            parameters: [],
        };
    }, []);

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
                        function (name: string) {
                            // Name can only contain letters, numbers, or hyphens and must be MAX_LENGTH characters or fewer.
                            return new RegExp(`^[a-zA-Z0-9-]{1,${ENTITY_NAME_MAX_LENGTH}}$`).test(name);
                        }
                    ),
                description: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                connector: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                database: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                query: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                parameters: array().of(
                    object({
                        name: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                        type: string().required(intl.formatMessage(SreAgentResources.fieldRequired)),
                        required: mixed(),
                        value: string().when('required', {
                            is: true,
                            then: schema => schema.required(intl.formatMessage(SreAgentResources.fieldRequired)),
                        }),
                    })
                ),
            }),
        [intl]
    );

    const save = useCallback(
        async (values: KustoToolFormProps) => {
            setIsSaving(true);

            const body: ExtendedTool = {
                name: values.name,
                type: 'KustoTool',
                description: values.description,
                connector: values.connector,
                database: values.database,
                query: values.query,
                parameters: values.parameters,
            };
            const notificationId = azPortalContext.startNotification(
                intl.formatMessage(ExtendedAgentsGraphResources.createToolTitle),
                intl.formatMessage(ExtendedAgentsGraphResources.createToolInProgress)
            );
            try {
                const response = await extendedAgentClient.applyEntity(body, 'tool');
                if (response.isSuccessful) {
                    azPortalContext.stopNotification(
                        notificationId,
                        true,
                        intl.formatMessage(ExtendedAgentsGraphResources.toolCreatedSuccessfully)
                    );
                    return response;
                } else {
                    azPortalContext.stopNotification(
                        notificationId,
                        false,
                        intl.formatMessage(ExtendedAgentsGraphResources.failedToCreateTool, { errorMessage: response.error })
                    );
                }
                return response;
            } catch (error) {
                azPortalContext.stopNotification(
                    notificationId,
                    false,
                    intl.formatMessage(ExtendedAgentsGraphResources.failedToCreateTool, { errorMessage: getErrorMessageOrStringify(error) })
                );
            } finally {
                setIsSaving(false);
            }
        },
        [azPortalContext, extendedAgentClient, intl]
    );

    return {
        initialValues,
        validationSchema,
        isSaving,
        save,
    };
};
