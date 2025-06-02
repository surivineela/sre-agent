import { Checkbox, PrimaryButton, Stack } from "@fluentui/react";
import { Autocomplete, TextField } from "@mui/material";
import { useMutation, useQuery } from "@tanstack/react-query";
import { createAgent, getLocations, getResourceGroups, getSubscriptions } from "../Services/Request";
import { DeployAgentPostBody, Location, ResourceGroup, Subscription } from "../Models/Response";
import { useEffect, useMemo, useState } from "react";

interface IAutoCompleteOption<T> {
    label: string;
    data: T;
}

// Todo: fill in with actual options
const DeployAgent = ({ teamId: _teamId }: { teamId: number }) => {

    const [selectedSubscription, setSelectedSubscription] = useState<IAutoCompleteOption<Subscription> | null>(null);

    const [selectedResourceGroup, setSelectedResourceGroup] = useState<IAutoCompleteOption<ResourceGroup> | null>(null);
    const [selectedLocation, setSelectedLocation] = useState<IAutoCompleteOption<Location> | null>(null);
    const [agentName, setAgentName] = useState<string>("");
    const [createNewResourceGroup, setCreateNewResourceGroup] = useState<boolean>(false);
    const [newResourceGroupName, setNewResourceGroupName] = useState<string>("");

    const { data: subscriptions = [], refetch: getSubscriptionsAsync, isLoading: isLoadingSubscriptions } = useQuery({
        queryFn: () => getSubscriptions(),
        queryKey: ["getSubscriptions"],
        enabled: false
    });

    const { data: resourceGroups = [], isLoading: isLoadingResourceGroups } = useQuery({
        queryFn: () => getResourceGroups(selectedSubscription!.data.subscriptionId),
        queryKey: ["getResourceGroups", selectedSubscription],
        enabled: !!selectedSubscription
    });

    const { data: locations = [], isLoading: isLoadingLocations } = useQuery({
        queryFn: () => getLocations(selectedSubscription!.data.subscriptionId),
        queryKey: ["getLocations", selectedSubscription],
        enabled: !!selectedSubscription
    });

    const { mutateAsync: createAgentAsync } = useMutation({
        mutationFn: (postBody: DeployAgentPostBody) => createAgent(postBody),
        mutationKey: ["createAgent"],
    });

    useEffect(() => {
        (async () => {
            await getSubscriptionsAsync();
        })();
    }, [getSubscriptionsAsync]); const subscriptionOptions: IAutoCompleteOption<Subscription>[] = useMemo(() => {
        // order the subscriptions by displayName
        return subscriptions
            .sort((a, b) => a.displayName.localeCompare(b.displayName))
            .map((subscription) => {
                return {
                    label: subscription.displayName, // Use displayName as label
                    data: { ...subscription }
                }
            });
    }, [subscriptions]);

    const resourceGroupOptions: IAutoCompleteOption<ResourceGroup>[] = useMemo(() => {
        return resourceGroups
            .sort((a, b) => a.name.localeCompare(b.name))
            .map((resourceGroup) => {
                return {
                    label: resourceGroup.name, // Use name as label
                    data: { ...resourceGroup }
                }
            });
    }, [resourceGroups]);

    const locationOptions: IAutoCompleteOption<Location>[] = useMemo(() => {
        return locations
            .sort((a, b) => a.displayName.localeCompare(b.displayName))
            .map((location) => {
                return {
                    label: location.displayName, // Use displayName as label
                    data: { ...location }
                }
            });
    }, [locations]); const onUpdateSubscription = (_event: any, item?: IAutoCompleteOption<Subscription> | null) => {
        if (!item) return;
        setSelectedSubscription(item);
        // Clear dependent selections when subscription changes
        setSelectedResourceGroup(null);
        setSelectedLocation(null);
        setNewResourceGroupName(""); // Clear new resource group name as well
    }

    const onUpdateResourceGroup = (_event: any, item?: IAutoCompleteOption<ResourceGroup> | null) => {
        if (!item) return;
        setSelectedResourceGroup(item);
        // Clear location selection when resource group changes
        setSelectedLocation(null);
    }

    const onUpdateLocation = (_event: any, item?: IAutoCompleteOption<Location> | null) => {
        if (!item) return;
        setSelectedLocation(item);
    }

    const onUpdateAgentName = (_event: any, newValue?: string) => {
        setAgentName(newValue || "");
    }

    const onToggleCreateNewResourceGroup = (_event: any, checked?: boolean) => {
        setCreateNewResourceGroup(checked || false);
        if (checked) {
            setSelectedResourceGroup(null); // Clear existing selection when switching to create new
        } else {
            setNewResourceGroupName(""); // Clear new name when switching back to selection
        }
    }

    const onUpdateNewResourceGroupName = (_event: any, newValue?: string) => {
        setNewResourceGroupName(newValue || "");
    }

    const onCreateAgent = async () => {
        if (!selectedSubscription || !selectedLocation) return;

        // Validate resource group selection or new name
        const resourceGroupName = createNewResourceGroup
            ? newResourceGroupName.trim()
            : selectedResourceGroup?.data.name;

        if (!resourceGroupName) return;

        const postBody: DeployAgentPostBody = {
            subscriptionId: selectedSubscription.data.subscriptionId,
            resourceGroup: resourceGroupName,
            location: selectedLocation.data.name,
            resourceName: agentName
        };
        await createAgentAsync(postBody);
    }

    return (<Stack tokens={{ childrenGap: 20 }}>        <Autocomplete
        options={subscriptionOptions}
        onChange={onUpdateSubscription}
        value={selectedSubscription}
        renderInput={(params) => <TextField {...params} label="Subscription" />}
        size="medium"
        disablePortal
        disabled={isLoadingSubscriptions}
    />

        <Checkbox
            label="Create new resource group"
            checked={createNewResourceGroup}
            onChange={onToggleCreateNewResourceGroup}
        />

        {createNewResourceGroup ? (
            <TextField
                label="Resource Group"
                placeholder="Enter new resource group name"
                value={newResourceGroupName}
                onChange={onUpdateNewResourceGroupName}
                disabled={!selectedSubscription}
            />) : (<Autocomplete
                options={resourceGroupOptions}
                onChange={onUpdateResourceGroup}
                value={selectedResourceGroup}
                renderInput={(params) => <TextField {...params} label="Resource Group" />}
                size="medium"
                disablePortal
                disabled={!selectedSubscription || isLoadingResourceGroups}
            />
        )}        <Autocomplete
            options={locationOptions}
            onChange={onUpdateLocation}
            value={selectedLocation}
            renderInput={(params) => <TextField {...params} label="Location" />}
            size="medium"
            disablePortal
            disabled={!selectedSubscription || isLoadingLocations}
        />
        <TextField
            label="Agent Name"
            defaultValue=""
            onChange={onUpdateAgentName}
        />            <Stack horizontal tokens={{ childrenGap: 10 }}>
            <PrimaryButton
                text="Create Agent"
                onClick={onCreateAgent}
                disabled={
                    !selectedSubscription ||
                    !selectedLocation ||
                    (createNewResourceGroup ? !newResourceGroupName.trim() : !selectedResourceGroup)
                }
            />
        </Stack>
    </Stack>
    );

}

export default DeployAgent;