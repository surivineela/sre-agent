import { memo, useMemo, useState } from "react";
import { getIcmTeams } from "../Services/Request";
import { mergeStyles, PrimaryButton, Stack, Text } from "@fluentui/react";
import { IcmTeamInfo } from "../Models/Response";
import { useQuery } from "@tanstack/react-query";
import LoadingErrorWrapper from "./LoadingErrorWrapper";
import { Autocomplete, TextField } from "@mui/material";

const processServiceTeamMap = async (): Promise<Promise<{ [key: string]: IcmTeamInfo[] }>> => {
    const teams = await getIcmTeams();
    if (!teams) return {};
    const map = {} as { [key: string]: IcmTeamInfo[] };
    for (const team of teams) {
        map[team.icmServiceName] = map[team.icmServiceName] || [];
        map[team.icmServiceName].push({ ...team });
    }

    for (const key of Object.keys(map)) {
        map[key].sort((a, b) => a.icmTeamName.localeCompare(b.icmTeamName));
    }
    return map;
}


interface IAutoCompleteOption<T> {
    label: string;
    data: T;
}

const Landing = (props: { defaultSelectedIcmInfo: IcmTeamInfo | undefined, onSelectTeam: (team: IcmTeamInfo) => void }) => {
    const { status, error, data: serviceTeamMap = {} } = useQuery({
        queryKey: ["getIcmTeams"],
        queryFn: processServiceTeamMap,
    });

    const defaultSelectService: IAutoCompleteOption<string> = props.defaultSelectedIcmInfo ? { label: props.defaultSelectedIcmInfo.icmServiceName, data: props.defaultSelectedIcmInfo.icmServiceName } : null;

    const [selectedService, setSelectedService] = useState<IAutoCompleteOption<string> | null>(defaultSelectService);

    let defaultSelectedTeam: IAutoCompleteOption<IcmTeamInfo> | null = null;
    if (props.defaultSelectedIcmInfo) {
        defaultSelectedTeam = { label: props.defaultSelectedIcmInfo.icmTeamName, data: props.defaultSelectedIcmInfo };
    }
    const [selectedTeam, setSelectedTeam] = useState<IAutoCompleteOption<IcmTeamInfo> | null>(defaultSelectedTeam);

    const serviceTeamOptions: IAutoCompleteOption<string>[] = useMemo(() => {
        return Object.keys(serviceTeamMap).sort().map(i => {
            return { label: i, data: i };
        });
    }, [serviceTeamMap]);

    const teamOptions: IAutoCompleteOption<IcmTeamInfo>[] = useMemo(() => {
        if (!selectedService) return [];
        const teams: IcmTeamInfo[] = serviceTeamMap[selectedService.data] || [];
        return teams.map(i => {
            return { label: i.icmTeamName, data: i };
        });
    }, [serviceTeamMap, selectedService]);

    const navigateToContent = () => {
        props.onSelectTeam(selectedTeam.data);
    }

    const contentStyles = mergeStyles({
        width: "40%"
    });

    const titleStyles = mergeStyles({
        margin: "0px auto"
    });

    const onUpdateSelectedService = (event: any, newValue: IAutoCompleteOption<string> | null) => {
        if (!newValue) return;
        setSelectedService(newValue);
        // reset selected team when service changes
        setSelectedTeam(null);
    }
    const onUpdateSelectedTeam = (event: any, newValue: IAutoCompleteOption<IcmTeamInfo> | null) => {
        if (!newValue) return;
        setSelectedTeam(newValue);
    }

    return (
        <>
            <LoadingErrorWrapper status={status} error={error} renderLoading="Loading ICM teams..." renderError="Error loading ICM teams. Please try again.">
                <Stack verticalFill horizontalAlign="center" verticalAlign="center" >
                    <Stack tokens={{ childrenGap: 20 }} className={contentStyles}>
                        <Text block variant="xxLarge" className={titleStyles}>Select an ICM team to create a new alert handler</Text>
                        <Autocomplete
                            options={serviceTeamOptions}
                            onChange={onUpdateSelectedService}
                            onInputChange={(event, newInputValue) => { setSelectedTeam(null) }}
                            value={selectedService}
                            renderInput={(parmas) => <TextField {...parmas} label="Owning Service" />}
                        />
                        <Autocomplete
                            options={teamOptions}
                            onChange={onUpdateSelectedTeam}
                            value={selectedTeam}
                            renderInput={(parmas) => <TextField {...parmas} label="Owning Team" />}
                        />
                        <PrimaryButton text="Continue" disabled={!selectedTeam} onClick={navigateToContent} />
                    </Stack>
                </Stack>
            </LoadingErrorWrapper>
        </>
    );
}

export default memo(Landing);