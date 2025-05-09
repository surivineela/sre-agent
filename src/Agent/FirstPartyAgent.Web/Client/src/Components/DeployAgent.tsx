import { Checkbox, DefaultButton, Dialog, DialogContent, DialogFooter, DialogType, Dropdown, IDialogContentProps, IDropdownOption, IModalProps, PrimaryButton, Stack, TextField } from "@fluentui/react";
import { useBoolean } from "@fluentui/react-hooks";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useSearchParams } from "react-router-dom";
import { createAgent, getAgentDeployments, getLocations, getResourceGroups, getSubscriptions } from "../Services/Request";
import { AgentDeployment, DeployAgentPostBody, Location, ResourceGroup, Subscription } from "../Models/Response";
import { useEffect, useMemo, useState } from "react";


const defaultFirstPartySubOptions: IDropdownOption<Subscription>[] =
    [
        {
            'key': 'SRE Agent 1P',
            'text': 'SRE Agent 1P',
            'data': {
                subscriptionId: 'ab32b825-51f2-41b0-8d25-85f7a0071a6f',
                displayName: 'SRE Agent 1P'
            }
        },
    ]


const defaultFirstPartyResourceGroupOptions: IDropdownOption<ResourceGroup>[] = [
    {
        'key': 'sreagent1p-rg',
        'text': 'sreagent1p-rg',
        'data': {
            name: 'sreagent1p-rg',
            location: ''
        }
    },
    {
        'key': 'sreagent1p-logicapps-rg',
        'text': 'sreagent1p-logicapps-rg',
        'data': {
            name: 'sreagent1p-logicapps-rg',
            location: ''
        }
    },
    {
        'key': 'sreagent1p-redis-rg',
        'text': 'sreagent1p-redis-rg',
        'data': {
            name: 'sreagent1p-redis-rg',
            location: ''
        }
    }
];

const defaultFirstPartyLocationOptions: IDropdownOption<Location>[] = [
    {
        "key": "Central US EUAP",
        "text": "Central US EUAP",
        'data': {
            name: 'centraluseuap',
            displayName: 'Central US EUAP'
        }
    },
    {
        "key": "Australia East",
        "text": "Australia East",
        'data': {
            name: 'australiaeast',
            displayName: 'Australia East'
        }
    },
    {
        "key": "Sweden Central",
        "text": "Sweden Central",
        'data': {
            name: 'swedencentral',
            displayName: 'Sweden Central'
        }
    }
]

// Todo: fill in with actual options
const DeployAgent = (props: { teamId: number }) => {
    const [searchParams] = useSearchParams();
    const [isDialogVisible, { setTrue: displayDialog, setFalse: hideDialog }] = useBoolean(false);

    const [inputsReadOnly, { setTrue: enableInputReadOnly, setFalse: disableInputsReadOnly }] = useBoolean(true);
    const [selectedSubscription, setSelectedSubscription] = useState<IDropdownOption<Subscription> | null>(null);
    const [selectedResourceGroup, setSelectedResourceGroup] = useState<IDropdownOption<ResourceGroup> | null>(null);
    const [selectedLocation, setSelectedLocation] = useState<IDropdownOption<Location> | null>(null);
    const [agentName, setAgentName] = useState<string>("");

    const isDeployAgentDisabled = searchParams.get("deployAgentEnabled") !== "true";
    const dialogContentProps: IDialogContentProps = {
        type: DialogType.largeHeader,
        title: 'Deploy Agent',
    };

    const modalProps: IModalProps = {
        isBlocking: false,
        styles: { main: { minWidth: 400, maxWidth: 450 } },
    };

    const { status: getAgentDeploymentStatus, error: getAgentDeploymentError, data: agentDeploymentsData = null, refetch: getAgentDeploymentsAsync } = useQuery({
        queryFn: async () => {
            const deployments = await getAgentDeployments(props.teamId);
            if (Array.isArray(deployments) && deployments.length > 0) {
                return deployments[0];
            }
            return null;
        },
        queryKey: ["getAgentDeployments", props.teamId],
        enabled: false
    });

    const { status: getSubscriptionsStatus, error: getSubscriptionsError, data: subscriptions = [], refetch: getSubscriptionsAsync } = useQuery({
        queryFn: () => getSubscriptions(),
        queryKey: ["getSubscriptions"],
        enabled: false
    });

    const { status: getResourceGroupsStatus, error: getResourceGroupError, data: resourceGroups = [], refetch: getResourceGroupAsync } = useQuery({
        queryFn: () => getResourceGroups(selectedSubscription.data.subscriptionId),
        queryKey: ["getResourceGroups", selectedSubscription],
        enabled: false
    });

    const { status: getLocationsStatus, error: getLocationsError, data: locations = [], refetch: getLocationsAsync } = useQuery({
        queryFn: () => getLocations(selectedSubscription.data.subscriptionId),
        queryKey: ["getLocations", selectedSubscription],
        enabled: false
    });

    const { status: createAgentStatus, error: createAgentError, mutateAsync: createAgentAsync } = useMutation({
        mutationFn: (postBody: DeployAgentPostBody) => createAgent(postBody),
        mutationKey: ["createAgent"],
    });

    useEffect(() => {
        (async () => {
            let defaultAgentDeployment: AgentDeployment | null = null;
            defaultAgentDeployment = (await getAgentDeploymentsAsync()).data;
            if (defaultAgentDeployment) {
                disableInputsReadOnly();
            } else {
                enableInputReadOnly();
                await getSubscriptionsAsync();
            }
        })();
    }, []);

    useEffect(() => {
        (async () => {
            if (!selectedSubscription || inputsReadOnly) return;
            // await Promise.allSettled([
            //     getResourceGroupAsync(),
            //     getLocationsAsync()
            // ]);
        })();
    }, [selectedSubscription, inputsReadOnly]);

    const subscriptionOptions: IDropdownOption<Subscription>[] = useMemo(() => {
        if (agentDeploymentsData != null) {
            return [
                {
                    key: agentDeploymentsData.subscriptionId,
                    text: agentDeploymentsData.subscriptionId,
                    data: {
                        subscriptionId: agentDeploymentsData.subscriptionId,
                        displayName: agentDeploymentsData.subscriptionId
                    }
                }
            ]
        }
        if (subscriptions.length > 0) {
            return subscriptions.map((subscription) => {
                return {
                    key: subscription.subscriptionId,
                    text: subscription.subscriptionId,
                    data: { ...subscription }
                }
            });
        }
        // return [];
        return defaultFirstPartySubOptions;

    }, [agentDeploymentsData, subscriptions]);

    const resourceGroupOptions: IDropdownOption<ResourceGroup>[] = useMemo(() => {
        if (agentDeploymentsData != null) {
            return [
                {
                    key: agentDeploymentsData.resourceGroup,
                    text: agentDeploymentsData.resourceGroup,
                    data: {
                        name: agentDeploymentsData.resourceGroup,
                        location: ''
                    }
                }
            ]
        }
        if (resourceGroups.length > 0) {
            return resourceGroups.map((resourceGroup) => {
                return {
                    key: resourceGroup.name,
                    text: resourceGroup.name,
                    data: { ...resourceGroup }
                }
            });
        }
        // return [];
        return defaultFirstPartyResourceGroupOptions;

    }, [agentDeploymentsData, resourceGroups]);

    const locationOptions: IDropdownOption<Location>[] = useMemo(() => {
        if (agentDeploymentsData != null) {
            return [
                {
                    key: agentDeploymentsData.location,
                    text: agentDeploymentsData.location,
                    data: {
                        name: agentDeploymentsData.location,
                        displayName: agentDeploymentsData.location
                    }
                }
            ]
        }
        if (locations?.length > 0) {
            return locations.map((location) => {
                return {
                    key: location.name,
                    text: location.displayName,
                    data: { ...location }
                }
            });
        }
        // return [];
        return defaultFirstPartyLocationOptions;


    }, [agentDeploymentsData, locations]);

    const onUpdateSubscription = (event: any, item?: IDropdownOption) => {
        if (!item) return;
        setSelectedSubscription(item);
    }

    const onUpdateResourceGroup = (event: any, item?: IDropdownOption) => {
        if (!item) return;
        setSelectedResourceGroup(item);
    }

    const onUpdateLocation = (event: any, item?: IDropdownOption) => {
        if (!item) return;
        setSelectedLocation(item);
    }

    const onUpdateAgentName = (event: any, newValue?: string) => {
        setAgentName(newValue || "");
    }

    const onCreateAgent = async () => {
        if (!selectedSubscription || !selectedResourceGroup || !selectedLocation) return;
        const postBody: DeployAgentPostBody = {
            subscriptionId: selectedSubscription.data.subscriptionId,
            resourceGroup: selectedResourceGroup.data.name,
            location: selectedLocation.data.name,
            resourceName: agentName
        }
        await createAgentAsync(postBody);
    }

    return (
        <>
            <PrimaryButton text="Deploy to agent" disabled={isDeployAgentDisabled} onClick={displayDialog} />
            <Dialog hidden={!isDialogVisible} onDismiss={hideDialog} dialogContentProps={dialogContentProps} modalProps={modalProps}>
                <DialogContent>
                    <Stack tokens={{ childrenGap: 5 }}>
                        <Dropdown options={subscriptionOptions} label="Subscription" disabled={inputsReadOnly} onChange={onUpdateSubscription} defaultSelectedKey={inputsReadOnly ? subscriptionOptions[0].key : ""}></Dropdown>
                        <Dropdown options={resourceGroupOptions} label="Resource Group" disabled={inputsReadOnly} onChange={onUpdateResourceGroup} defaultSelectedKey={inputsReadOnly ? resourceGroupOptions[0].key : ""}></Dropdown>
                        <Checkbox label="Create new resource group" disabled={inputsReadOnly} />
                        <Dropdown options={locationOptions} label="Location" disabled={inputsReadOnly} onChange={onUpdateLocation} defaultSelectedKey={inputsReadOnly ? locationOptions[0].key : ""}></Dropdown>
                        <TextField label="Agent Name" defaultValue={inputsReadOnly ? agentDeploymentsData?.name : ""} />
                    </Stack>
                </DialogContent>
                <DialogFooter>
                    <PrimaryButton text="Update Agent" />
                    <DefaultButton text="Cancel" onClick={hideDialog} />
                </DialogFooter>
            </Dialog>
        </>
    );

}

export default DeployAgent;