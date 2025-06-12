import { IDropdownOption, Selection } from '@fluentui/react';
import { useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import { useIntl } from 'react-intl';
import { AzPortalContext } from '../../../Common/AzPortalProxy/Providers/AzPortalProxyContext';
import { EnvironmentContext } from '../../../Common/AzPortalProxy/Providers/StartupInfoContext';
import { IncidentHandlerClient } from '../../../Common/Clients/IncidentHandlerClient';
import {
    IIncidentDocumentWithKeyAndSelection,
    IncidentHandler,
    ToolInfoWithKeyAndSelection,
} from '../../../Common/Contracts/Azure/IncidentHandler';
import { Guid } from '../../../Common/Helpers/Guid';
import { ArmResourceDescriptor } from '../../../Common/Helpers/ResourceDescriptors';
import { IncidentHandlerCreateResources } from '../../../Strings/SREAgentResources';
import { IncidentHandlerCreateSteps } from './IncidentHandlerCreateContext';

enum TimeDuration {
    Last15Days = 15,
    Last30Days = 30,
    Last60Days = 60,
    Last90Days = 90,
}

export const useCreateIncidentHandler = (
    incidentFilterId: string,
    exitToHome: () => void,
    createHandler: (handler: IncidentHandler) => void
) => {
    const intl = useIntl();
    const [currentStep, setCurrentStep] = useState<IncidentHandlerCreateSteps>(IncidentHandlerCreateSteps.GenerateHandler);
    const [name, setName] = useState<string>('');
    const [description, setDescription] = useState<string>('');
    const [incidentProcessingGuide, setIncidentProcessingGuide] = useState<string>('');

    const [toolInfos, setToolInfos] = useState<ToolInfoWithKeyAndSelection[]>([]);
    const [loadingTools, setLoadingTools] = useState<boolean>(true);
    const [selectedTools, setSelectedTools] = useState<ToolInfoWithKeyAndSelection[]>([]);

    const toolsSelection = useRef(
        new Selection<ToolInfoWithKeyAndSelection>({
            getKey: (item: ToolInfoWithKeyAndSelection) => item.name,
            onSelectionChanged: () => {
                setSelectedTools(toolsSelection.current.getSelection() as ToolInfoWithKeyAndSelection[]);
            },
        })
    );

    const [incidentDocuments, setIncidentDocuments] = useState<IIncidentDocumentWithKeyAndSelection[]>();
    const [loadingIncidents, setLoadingIncidents] = useState<boolean>(true);
    const [selectedIncidents, setSelectedIncidents] = useState<IIncidentDocumentWithKeyAndSelection[]>([]);

    const incidentsSelection = useRef(
        new Selection<IIncidentDocumentWithKeyAndSelection>({
            getKey: (item: IIncidentDocumentWithKeyAndSelection) => item.id,
            onSelectionChanged: () => {
                setSelectedIncidents(incidentsSelection.current.getSelection() as IIncidentDocumentWithKeyAndSelection[]);
            },
        })
    );

    const [customInstructions, setCustomInstructions] = useState<string>('');

    const { resourceId, sreAgentEndpoint } = useContext(EnvironmentContext);
    const agentName = useMemo(() => (resourceId ? (new ArmResourceDescriptor(resourceId).resourceName ?? '') : ''), [resourceId]);
    const incidentHandlerClient = useMemo(() => IncidentHandlerClient.getInstance(sreAgentEndpoint), [sreAgentEndpoint]);

    const timespanDropdownOptions: IDropdownOption<{ numberOfDays: number; isDefault?: boolean }>[] = useMemo(() => {
        return [
            {
                key: TimeDuration.Last15Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last15days),
                data: { numberOfDays: TimeDuration.Last15Days },
            },
            {
                key: TimeDuration.Last30Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last30days),
                data: { numberOfDays: TimeDuration.Last30Days },
            },
            {
                key: TimeDuration.Last60Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last60days),
                data: { numberOfDays: TimeDuration.Last60Days, isDefault: true },
            },
            {
                key: TimeDuration.Last90Days,
                text: intl.formatMessage(IncidentHandlerCreateResources.last90days),
                data: { numberOfDays: TimeDuration.Last90Days },
            },
        ];
    }, [intl]);

    const [selectedTimespanOption, setSelectedTimespanOption] = useState(
        timespanDropdownOptions.find(option => option.data?.isDefault === true)
    );

    const [isGeneratingInstructions, setIsGeneratingInstructions] = useState<boolean>(false);

    const handleGenerateInstructions = useCallback(() => {
        setIsGeneratingInstructions(true);
        // TODO (andimarc): Add logging and error handling. Surface errors to the user.
        return incidentHandlerClient
            .generateInstructions({
                agentName,
                incidents: selectedIncidents?.map(incident => incident.id) ?? [],
                tools: selectedTools?.map(tool => tool.name) ?? [],
                customInstructions: customInstructions,
            })
            .then(res => {
                setIsGeneratingInstructions(false);

                if (res.isSuccessful && res.content) {
                    setIncidentProcessingGuide(res.content.generatedInstructions);
                    setCurrentStep(IncidentHandlerCreateSteps.ReviewAndEdit);
                }
            });
    }, [
        setIsGeneratingInstructions,
        incidentHandlerClient.generateInstructions,
        selectedIncidents,
        selectedTools,
        customInstructions,
        setIncidentProcessingGuide,
    ]);

    const [editorDisplayValue, setEditorDisplayValue] = useState<string>();
    const azPortalContext = useContext(AzPortalContext);

    const onEditorValueChange = useCallback((value: string | undefined) => setEditorDisplayValue(value), []);

    const save = useCallback(() => {
        if (editorDisplayValue) {
            let configObject: IncidentHandler | undefined;

            try {
                configObject = JSON.parse(editorDisplayValue) as IncidentHandler;
            } catch (error) {
                return;
                // TODO (andimarc): Handle JSON parse error. Surface error to the user.
            }

            configObject.id = Guid.newShortGuid();

            createHandler(configObject);
        }
    }, [
        incidentHandlerClient,
        editorDisplayValue,
        resourceId,
        azPortalContext.startNotification,
        azPortalContext.stopNotification,
        intl.formatMessage,
    ]);

    const initializeEditorDisplayValue = useCallback(() => {
        const config = {
            name,
            description,
            incidentFilterId,
            incidentProcessingGuide: incidentProcessingGuide.split('\r\n'),
            tools: selectedTools.map(tool => tool.name),
            incidents: selectedIncidents.map(incident => incident.id),
            customInstructions: customInstructions,
        };
        setEditorDisplayValue(JSON.stringify(config, null, 4));
    }, [name, description, incidentFilterId, incidentProcessingGuide, selectedTools, selectedIncidents, customInstructions]);

    useEffect(() => {
        let subscribed = true;

        if (selectedTimespanOption) {
            const queryPayload = {
                filter: {
                    id: incidentFilterId,
                },
                durationInDays: selectedTimespanOption.data?.numberOfDays,
            };
            setIncidentDocuments([]);
            setLoadingIncidents(true);
            incidentHandlerClient.queryIncidents(queryPayload).then(response => {
                if (subscribed) {
                    if (response.isSuccessful && response.content) {
                        const sortedIncidents = response.content.sort((a, b) => a.title.localeCompare(b.title));
                        setIncidentDocuments(
                            sortedIncidents.map(incident => ({ ...incident, selected: false }) as IIncidentDocumentWithKeyAndSelection)
                        );
                    }
                    setLoadingIncidents(false);
                }
            });
        } else {
            setIncidentDocuments([]);
            setLoadingIncidents(false);
        }

        return () => {
            subscribed = false;
        };
    }, [incidentHandlerClient, incidentFilterId, selectedTimespanOption]);

    useEffect(() => {
        let subscribed = true;

        setToolInfos([]);
        setLoadingTools(true);
        incidentHandlerClient.listTools().then(response => {
            if (subscribed) {
                if (response.isSuccessful && response.content) {
                    const sortedTools = response.content.sort((a, b) => a.name.localeCompare(b.name));
                    setToolInfos(sortedTools.map(tool => ({ ...tool, selected: false }) as ToolInfoWithKeyAndSelection));
                }
                setLoadingTools(false);
            }
        });

        return () => {
            subscribed = false;
        };
    }, []);

    return {
        intl,
        exitToHome,
        agentName,
        incidentFilterId,
        currentStep,
        setCurrentStep,
        name,
        setName,
        description,
        setDescription,
        incidentProcessingGuide,
        setIncidentProcessingGuide,
        selectedTools,
        toolsSelection,
        selectedIncidents,
        incidentsSelection,
        customInstructions,
        setCustomInstructions,
        incidentDocuments,
        loadingIncidents,
        toolInfos,
        loadingTools,
        timespanDropdownOptions,
        selectedTimespanOption,
        setSelectedTimespanOption,
        isGeneratingInstructions,
        handleGenerateInstructions,
        initializeEditorDisplayValue,
        editorDisplayValue,
        setEditorDisplayValue,
        onEditorValueChange,
        save,
    };
};
