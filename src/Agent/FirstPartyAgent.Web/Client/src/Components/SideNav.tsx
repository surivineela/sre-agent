import { memo, useMemo, useState } from 'react'
import { getLoopAlertConfigs } from '../Services/Request';
import { INavLink, INavLinkGroup, INavStyles, Nav, PrimaryButton, SearchBox, Stack } from '@fluentui/react';
import { AlertEditorProps } from './AlertEditor';
import { useQuery } from '@tanstack/react-query';
import LoadingErrorWrapper from './LoadingErrorWrapper';
import { ICMAlertConfig } from '../Models/ICMAlertConfig';
import { useQueryParams } from '../Hooks/UseQueryParams';

const SideNav = (props: { onGetAlertConfig: (params: AlertEditorProps) => void, onCreateNewAlertHandler: () => void, selectedSideNavItemId: string, defaultIcmTeamId?: number }) => {
    const [searchText, setSearchText] = useState<string>("");
    const { isPlayground } = useQueryParams();
    const { status, error, data: allLoopAlertConfigs = [] } = useQuery({
        queryKey: ["getLoopAlertConfigs", props.defaultIcmTeamId],
        queryFn: () => {
            if (!isPlayground && props.defaultIcmTeamId) {
                return getLoopAlertConfigs(props.defaultIcmTeamId);
            } else {
                return getLoopAlertConfigs();
            }
        },
    });

    const navStyles: Partial<INavStyles> = {
        groupContent: {
            marginBottom: "10px"
        },
        link: {
            whiteSpace: "normal",
            lineHeight: "normal",
            height: "auto",
            minHeight: "44px",
            padding: "0 0 0 5px",
        },
        chevronButton: {
            whiteSpace: "normal",
            lineHeight: "normal",
            height: "auto",
            margin: "0px",
            fontSize: "15px",
            fontWeight: 600,
            paddingBottom: "8px",
            display: "flex",
            alignItems: "center",
        },
        chevronIcon: {
            fontSize: "10px",
            fontWeight: 600,
        },
        compositeLink: {
            marginTop: "3px",
            marginBottom: "3px",
            lineHeight: "normal",
        }
    };

    const navLinkGroups = useMemo(() => {
        const alertConfigs = allLoopAlertConfigs.filter(config => {
            if (!config.incidentTitle || !config.alertingId) return false;
            return config.incidentTitle.toLowerCase().includes(searchText.toLowerCase()) ||
                config.alertingId.toLowerCase().includes(searchText.toLowerCase())
        });

        const map = new Map<string, ICMAlertConfig[]>();
        for (const config of alertConfigs) {
            const key = config.defaultHumanInterventionLoop ?? "default";
            if (!map.has(key)) {
                map.set(key, []);
            }
            map.get(key).push(config);
        }

        const groups: INavLinkGroup[] = [];
        for (const [defaultInterventionLoop, configs] of map.entries()) {
            const links: INavLink[] = [];
            for (const config of configs) {
                const link: INavLink = {
                    name: config.incidentTitle,
                    onClick: () => {
                        props.onGetAlertConfig({
                            alertId: config.alertingId,
                            icmTeamId: `${config.teamId}`,
                        });
                    },
                    url: '',
                    key: config.alertingId,
                    forceAnchor: true,
                };
                links.push(link);
            }
            const group: INavLinkGroup = {
                links: links,
                name: defaultInterventionLoop
            };
            groups.push(group);
        }
        return groups;

    }, [allLoopAlertConfigs, searchText]);


    const onSearchBoxUpdate = (event: any, newValue?: string) => {
        if (newValue === undefined || newValue === null) return;
        setSearchText(newValue);
    }





    return (
        <Stack tokens={{ childrenGap: 10 }} styles={{ root: { maxHeight: "100vh",overflowY: "auto" } }}>
            <LoadingErrorWrapper status={status} error={error} renderLoading="Loading alerts...">
                <SearchBox placeholder='Filter alerts' onChange={onSearchBoxUpdate} />
                <PrimaryButton onClick={(e) => { props.onCreateNewAlertHandler() }}>Create New Alert Handler</PrimaryButton>
                <Nav groups={navLinkGroups} styles={navStyles} selectedKey={props.selectedSideNavItemId} />
            </LoadingErrorWrapper>
        </Stack>
    )
}

export default memo(SideNav);