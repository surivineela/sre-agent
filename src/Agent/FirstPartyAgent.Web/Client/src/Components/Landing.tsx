import { memo, useMemo, useState } from "react";
import { getIcmServices, getIcmTeamsByServiceId } from "../Services/Request";
import { mergeStyles, PrimaryButton, Stack, Text } from "@fluentui/react";
import { IcmService, IcmTeam, IcmTeamInfo } from "../Models/Response";
import { useQuery } from "@tanstack/react-query";
import LoadingErrorWrapper from "./LoadingErrorWrapper";
import { Autocomplete, TextField } from "@mui/material";
import { ContentStyleSets, ItemPaddingStyles } from "../Styles/Content.Styles";
import { AutoCompleteWithVirtualization, IAutoCompleteOption } from "./AutoCompleteWithVirtualization";

const Landing = (props: { defaultSelectedIcmInfo: IcmTeamInfo | undefined, onSelectTeam: (team: IcmTeamInfo) => void }) => {
    const { status, error, data: services = [] } = useQuery({
        queryKey: ["getIcmServices"],
        queryFn: async () => {
            const services = await getIcmServices();
            if (!services) return [];

            // Sort services by name
            return services.sort((a, b) => a.name.localeCompare(b.name));
        },
    });

    // Find the default selected service based on the default team info
    let defaultSelectedService: IAutoCompleteOption<IcmService> | null = null;
    if (props?.defaultSelectedIcmInfo?.icmServiceId) {
        const defaultService = services.find(s => s.id === props.defaultSelectedIcmInfo?.icmServiceId);
        if (defaultService) {
            defaultSelectedService = { label: defaultService.name, data: defaultService };
        }
    }

    const [selectedService, setSelectedService] = useState<IAutoCompleteOption<IcmService> | null>(defaultSelectedService);

    // Query for teams based on selected service
    const { status: teamsStatus, error: teamsError, data: teamsData } = useQuery({
        queryKey: ["getIcmTeams", selectedService?.data?.id],
        queryFn: async () => {
            if (selectedService?.data?.id) {
                return await getIcmTeamsByServiceId(`${selectedService.data.id}`);
            } else {
                return null;
            }

        },
        enabled: !!selectedService?.data?.id,
    });

    let defaultSelectedTeam: IAutoCompleteOption<IcmTeam> | null = null;
    if (props.defaultSelectedIcmInfo && teamsData) {
        const defaultTeam = teamsData.teams.find(t => t.id === props.defaultSelectedIcmInfo?.icmTeamId);
        if (defaultTeam) {
            defaultSelectedTeam = { label: defaultTeam.name, data: defaultTeam };
        }
    }
    const [selectedTeam, setSelectedTeam] = useState<IAutoCompleteOption<IcmTeam> | null>(defaultSelectedTeam);

    const serviceOptions: IAutoCompleteOption<IcmService>[] = useMemo(() => {
        return services.map(service => ({
            label: service.name,
            data: service
        }));
    }, [services]);

    const teamOptions: IAutoCompleteOption<IcmTeam>[] = useMemo(() => {
        if (!teamsData) return [];
        return teamsData.teams.map(team => ({
            label: team.name,
            data: team
        }));
    }, [teamsData]);

    const navigateToContent = () => {
        if (selectedTeam.data?.id && selectedService.data?.id) {
            // Convert IcmTeam to IcmTeamInfo format expected by parent
            const teamInfo: IcmTeamInfo = {
                icmServiceId: selectedService.data.id,
                icmServiceName: selectedService.data.name,
                icmTeamName: selectedTeam.data.name,
                icmTeamId: selectedTeam.data.id,
                teamPublicId: selectedTeam.data.publicId
            };
            props.onSelectTeam(teamInfo);
        }
    }

    const contentStyles = mergeStyles({
        width: "32%"
    });

    const titleStyles = mergeStyles({
        margin: "0px auto",
        fontSize: "22px",
    }); const onUpdateSelectedService = (_event: any, newValue: IAutoCompleteOption<IcmService> | null) => {
        if (!newValue) return;
        setSelectedService(newValue);
        // reset selected team when service changes
        setSelectedTeam(null);
    }

    const onUpdateSelectedTeam = (_event: any, newValue: IAutoCompleteOption<IcmTeam> | null) => {
        if (!newValue) return;
        setSelectedTeam(newValue);
    }

    const autocompleteGroupBy = <T,>(option: IAutoCompleteOption<T>) => {
        return option.label.charAt(0);
    }

    return (
        <>
            <LoadingErrorWrapper status={status} error={error} renderLoading="Loading ICM services..." renderError="Error loading ICM services. Please try again.">
                <Stack verticalFill horizontalAlign="center" verticalAlign="start" className={ContentStyleSets.container}>
                    <Stack tokens={{ childrenGap: 20 }} className={contentStyles}>
                        <Text block variant="xxLarge" className={`${titleStyles} ${ItemPaddingStyles}`}>Select an ICM team to create a new alert handler</Text>
                        <Autocomplete
                            options={serviceOptions}
                            onChange={onUpdateSelectedService}
                            onInputChange={(_event, _newInputValue) => { setSelectedTeam(null) }}
                            value={selectedService}
                            renderInput={(params) => <TextField {...params} label="Owning Service" />}
                            size="medium"
                            groupBy={autocompleteGroupBy}
                            className={ItemPaddingStyles}
                        />
                        <LoadingErrorWrapper
                            status={selectedService ? teamsStatus : 'success'}
                            error={teamsError}
                            renderLoading="Loading teams..."
                            renderError="Error loading teams. Please try again."
                        >
                            <AutoCompleteWithVirtualization
                                options={teamOptions}
                                onChange={onUpdateSelectedTeam}
                                value={selectedTeam}
                                renderInput={(params) => <TextField {...params} label="Owning Team" />}
                                groupBy={autocompleteGroupBy}
                                className={ItemPaddingStyles}
                                disabled={!selectedService}
                            />
                        </LoadingErrorWrapper>
                        <PrimaryButton text="Continue" disabled={!selectedTeam} onClick={navigateToContent} />
                    </Stack>
                </Stack>
            </LoadingErrorWrapper>
        </>
    );
}

export default memo(Landing);