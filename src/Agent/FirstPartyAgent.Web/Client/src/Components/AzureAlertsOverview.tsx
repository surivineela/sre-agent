import { useEffect, useState, memo, useMemo, Suspense } from "react";
import { getLoopAlertInfo } from "../Services/Request";
import { AlertInfo, IcmTeamInfo } from "../Models/Response";
import { Checkbox, DetailsList, IColumn, Link, mergeStyles, SearchBox, SelectionMode, Stack } from "@fluentui/react";
import { AlertEditorProps } from "./AlertEditor";
import { generateAzureAlertConfig } from "../Services/AlertUtilities";
import { useQuery } from "@tanstack/react-query";
import LoadingErrorWrapper from "./LoadingErrorWrapper";

const AzureAlertsOverview = (props: { icmTeamInfo: IcmTeamInfo, onGetAlertConfig: (params: AlertEditorProps) => void }) => {
    const icmTeamId = `${props.icmTeamInfo.icmTeamId}`;
    const [showSev3Alerts, setShowSev3Alerts] = useState<boolean>(false);
    const [searchText, setSearchText] = useState<string>("");

    const { data: loopAlertInfo = [], status, error } = useQuery({
        queryKey: ["getLoopAlertInfo", icmTeamId],
        queryFn: () => getLoopAlertInfo(icmTeamId),
    });

    const searchBoxStyles = mergeStyles({
        maxWidth: "400px",
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
    });

    return (
        <>
            <LoadingErrorWrapper status={status} error={error}>
                <Stack tokens={{ childrenGap: 20 }} className={pageStyles}>
                    <Checkbox label="Show Sev3 Alerts" checked={showSev3Alerts} onChange={(e, checked) => setShowSev3Alerts(!!checked)} />
                    <SearchBox placeholder='Filter alerts' onChange={(e, newValue) => onSearchBoxUpdate(newValue)} className={searchBoxStyles} />
                    <DetailsList columns={columns} items={displayAlerts} selectionMode={SelectionMode.none} />
                </Stack>
            </LoadingErrorWrapper>
        </>
    );
}

export default memo(AzureAlertsOverview);