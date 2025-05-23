import { useState, memo, useMemo } from "react";
import { getLoopAlertInfo } from "../Services/Request";
import { AlertInfo, IcmTeamInfo } from "../Models/Response";
import { Checkbox, DetailsList, IColumn, Link, mergeStyles, SearchBox, SelectionMode, Stack } from "@fluentui/react";
import { generateAzureAlertConfig } from "../Services/AlertUtilities";
import { useQuery } from "@tanstack/react-query";
import LoadingErrorWrapper from "./LoadingErrorWrapper";
import { AlertEditorProps } from "./AlertEditor";

const AzureAlertsOverview = (props: { icmTeamInfo: IcmTeamInfo, onGetAlertConfig: (params: AlertEditorProps) => void }) => {
    const icmTeamId = `${props.icmTeamInfo.icmTeamId}`;
    const [showSev3Alerts, setShowSev3Alerts] = useState<boolean>(false);
    const [searchText, setSearchText] = useState<string>("");

    const { data: loopAlertInfo = [], status, error } = useQuery({
        queryKey: ["getLoopAlertInfo", icmTeamId],
        // order by title
        queryFn: async () => {
            const res = await getLoopAlertInfo(icmTeamId);
            res.sort((a, b) => {
                if (a.title && b.title) {
                    return b.title.localeCompare(a.title);
                } else {
                    return 0;
                }
            });
            return res;
        }
    });

    const searchBoxStyles = mergeStyles({
        maxWidth: "400px",
        width: "60%",
    });

    const onItemClick = (item: AlertInfo) => {
        const alertConfig = generateAzureAlertConfig(item)
        props.onGetAlertConfig({
            alertConfig: alertConfig
        });
    }

    const columns: IColumn[] = [
        {
            key: 'severity',
            name: 'Severity',
            fieldName: 'severity',
            minWidth: 80,
            maxWidth: 120,
            isMultiline: false,
        },
        {
            key: 'id',
            name: 'Alerting ID',
            fieldName: 'id',
            minWidth: 150,
            maxWidth: 250,
            isMultiline: false,
            onRender: (item: AlertInfo) => {
                return (
                    <Link onClick={() => onItemClick(item)}>{item.id}</Link>
                )
            }
        },
        {
            key: 'title',
            name: 'Title',
            fieldName: 'title',
            minWidth: 200,
            isMultiline: false,
        }
    ]

    const displayAlerts = useMemo(() => {
        let alerts: AlertInfo[] = [];
        if (showSev3Alerts) {
            alerts = loopAlertInfo.filter(alert => alert?.severity === 2 || alert?.severity === 3)
        } else {
            alerts = loopAlertInfo.filter(alert => alert?.severity === 2);
        }
        return alerts.filter(alert => {
            return alert?.title.toLowerCase().includes(searchText.toLowerCase()) || alert?.id.toLowerCase().includes(searchText.toLowerCase());
        }).sort((a, b) => {
            if (a.severity && b.severity) {
                return a.severity > b.severity ? 1 : -1;
            } else {
                return 0;
            }
        });
    }, [loopAlertInfo, showSev3Alerts, searchText]);

    const onSearchBoxUpdate = (newValue?: string) => {
        if (newValue === undefined || newValue === null) return;
        setSearchText(newValue);
    }

    const pageStyles = mergeStyles({
        marginTop: "20px",
        height:"100%" 
    });

    const detailsListStyles = mergeStyles({
        marginTop: "-20px",
        width: "60vw",
        minWidth: "600px",
        overflowY: "auto",
        overflowX: "hidden",
        height: "100%",
        minHeight: "150px",
    });

    const controllerStyles = mergeStyles({ 
        width: "60vw", minWidth: "600px"
    });

    return (
        <>
            <LoadingErrorWrapper status={status} error={error}>
                <Stack tokens={{ childrenGap: 20 }} className={pageStyles} horizontalAlign="center">
                    <Stack horizontal tokens={{ childrenGap: 20 }} verticalAlign="center" className={controllerStyles}>
                        <SearchBox placeholder='Filter alerts' onChange={(_, newValue) => onSearchBoxUpdate(newValue)} className={searchBoxStyles} />
                        <Checkbox label="Show Sev3 Alerts" checked={showSev3Alerts} onChange={(_, checked) => setShowSev3Alerts(!!checked)} />
                    </Stack>
                    <DetailsList columns={columns} items={displayAlerts} selectionMode={SelectionMode.none} className={detailsListStyles} />
                </Stack>
            </LoadingErrorWrapper>
        </>
    );
}

export default memo(AzureAlertsOverview);