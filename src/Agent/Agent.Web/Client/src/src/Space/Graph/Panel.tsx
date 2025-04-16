import { Button, DrawerBody, DrawerHeader, DrawerHeaderTitle, OverlayDrawer, Field, Label, Image, Spinner, Link, makeStyles } from "@fluentui/react-components";
import { memo, useContext, useEffect, useState } from "react";
import { GraphContext, ResourceExtended, ScoreCardObject } from "../Contracts/Graph";
import { Dismiss24Regular } from "@fluentui/react-icons";
import { Guid } from "../../Common/Helpers/Guid";
import axios from "axios";
import HealthStatus from "./HealthStatus";
import { getSafeDateTime } from "../../Common/Helpers/Date";

interface IPanelProps {
    transferDataToActivities: (threadId?: string | null) => void
}

const getResource = async (resourceId: string): Promise<ResourceExtended | undefined> => {
    try {
        const { data } = await axios.get(`../api/v1/graph/resource/${resourceId}`);
        return (data ?? [])?.[0];
    } catch {
        return undefined;
    }
}

const createThread = async (resourceId: string) => {
    const url = `../api/v1/threads`;

    const response = await axios.post(url, {
        startMessage: {
            text: `Resource ${resourceId} is unhealthy could you help diagnose what is wrong?`,
            userId: 'web-client-user',
            displayName: 'Web Client User',
        }
    });
    return response?.data;
}


const isNullOrUndefined = (input?: unknown): boolean => {
    return input === undefined || input === null;
}


type FieldSummary = { label: string, value: JSX.Element | string }

const useStyles = makeStyles({
    spinner: {
        position: 'fixed',
        top: '50%',
        left: '50%'
    },
    summaries: {
        marginTop: '20px',
        display: 'flex',
        flexDirection: 'column',
        gap: '15px',
        justifyContent: 'center',
        alignItems: 'flex-start',
        flexWrap: 'wrap'
    },
    summaryField: {
        lineHeight: '20px',
        padding: '6px 0px'
    },
    dashboard: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '5px'
    }
})

const Panel = ({ transferDataToActivities }: IPanelProps) => {

    const [summary, setSummary] = useState<FieldSummary[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [resource, setResource] = useState<ResourceExtended>();
    const [sendingReport, setSendingReport] = useState<boolean>(false);

    const { isPanelOpen, closePanel, selectedNode } = useContext(GraphContext);

    const { spinner, summaries, dashboard } = useStyles();

    useEffect(() => {
        let isSubscribed = true;

        if (selectedNode) {
            setIsLoading(true);
            if (selectedNode.type === 'subscription') {
                setSummary([
                    { label: 'Name', value: selectedNode.name },
                    { label: 'Type', value: 'Subscription' },
                ]);
                setIsLoading(false);
            } else {
                getResource(selectedNode.id)
                    .then((resource) => {
                        if (isSubscribed) {
                            setResource(resource);
                        }
                    }).finally(() => {
                        if (isSubscribed) {
                            setIsLoading(false);
                        }
                    })
            }
        }

        return () => {
            isSubscribed = false;
        }
    }, [selectedNode])

    useEffect(() => {
        if (resource && selectedNode?.type !== 'subscription') {
            const { dashboardUrl, properties: { resourceType, resourceName, resourceGroupName, subscriptionId, resourceId, appHealthInfo: appHealthInfoResponse } } = resource;
            const summary: FieldSummary[] = [];

            if (resourceName && resourceName.length > 0) {
                summary.push({ label: 'Name', value: resourceName[0] });
            }
            if (resourceType && resourceType.length > 0) {
                summary.push({ label: 'Type', value: resourceType[0] });
            }
            if (resourceGroupName && resourceGroupName.length > 0) {
                summary.push({ label: 'Resource group', value: resourceGroupName[0] });
            }
            if (subscriptionId && subscriptionId.length > 0) {
                summary.push({ label: 'Subscription ID', value: subscriptionId[0] });
            }

            let scoreCard: ScoreCardObject | null = null;

            try {
                scoreCard = appHealthInfoResponse?.[0] ? JSON.parse(appHealthInfoResponse[0]) : null;
            } catch {
                scoreCard = null;
            }

            if (scoreCard) {
                const {
                    Costs,
                    Availability,
                    Health,
                    Transactions,
                    AvgLatencyInMs,
                    AvgMemoryUsage,
                    AvgCpuUsage,
                    LastDataCaptureTimeStampInUTC,
                    IsActive
                } = scoreCard;


                summary.push(
                    {
                        label: 'Costs for the past 7 days',
                        value: isNullOrUndefined(Costs) || Costs === 0 ? "Cost calculation pending" : `${Costs} USD`
                    },
                );

                if (!isNullOrUndefined(Availability)) {
                    summary.push(
                        { label: 'Availability', value: `${(Availability ?? 0).toString()}%` },
                    );
                }

                summary.push(
                    {
                        label: 'Health',
                        value: <HealthStatus
                            health={Health}
                            showReportButton={true}
                            onClickReportButton={async () => {
                                setSendingReport(true);
                                const thread = await createThread(resourceId[0]);
                                setSendingReport(false);
                                transferDataToActivities(thread?.id);
                            }}
                            isSendingReport={sendingReport} />
                    },
                    { label: 'Number of transactions for the past 30 minutes', value: Transactions.toString() },
                );

                if (!isNullOrUndefined(AvgLatencyInMs)) {
                    summary.push(
                        { label: 'Average latency', value: `${(AvgLatencyInMs ?? 0) / 1000} seconds` },
                    );
                }

                if (!isNullOrUndefined(AvgMemoryUsage)) {
                    summary.push(
                        { label: 'Average memory usage', value: `${AvgMemoryUsage} bytes` },
                    );
                }

                if (!isNullOrUndefined(AvgCpuUsage)) {
                    summary.push(
                        { label: 'Average CPU usage', value: `${AvgCpuUsage}%` },
                    );
                }

                if (!isNullOrUndefined(LastDataCaptureTimeStampInUTC)) {
                    summary.push(
                        { label: 'Lastest data capture time', value: LastDataCaptureTimeStampInUTC ? getSafeDateTime(LastDataCaptureTimeStampInUTC).toLocaleString() : 'N/A' },
                    );
                }

                summary.push(
                    { label: 'Active status', value: IsActive ? 'Active' : 'Inactive' },
                );

                if (dashboardUrl && dashboardUrl.length > 0) {
                    summary.push(
                        {
                            label: 'Dashboard URL',
                            value: (
                                <div className={dashboard}>
                                    <Image src="./grafana-logo.svg" width={16} height={16} alt="Grafana logo" />
                                    <Link href={dashboardUrl} target="_blank">View here</Link>
                                </div>
                            )
                        },
                    );
                }
            }

            setSummary(summary);
        }

        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [sendingReport, resource, selectedNode?.type, transferDataToActivities]);

    return <OverlayDrawer
        modalType="non-modal"
        open={isPanelOpen}
        position="end"
        size={'medium'}
    >
        <DrawerHeader>
            <DrawerHeaderTitle
                action={
                    <Button
                        appearance="subtle"
                        aria-label="Close"
                        icon={<Dismiss24Regular />}
                        onClick={() => closePanel()}
                    />
                }
            >
                {selectedNode?.name ?? ''}
            </DrawerHeaderTitle>
        </DrawerHeader>

        <DrawerBody>
            {isLoading ?
                <Spinner size={'large'} className={spinner} /> :
                <div className={summaries}>
                    {summary.map(({ label, value }) => {
                        return <SummaryField key={Guid.newGuid()} label={label} value={value} />
                    })}
                </div>}
        </DrawerBody>
    </OverlayDrawer>
}

const SummaryField = ({ label, value }: { label: string, value: JSX.Element | string }) => {
    const { summaryField } = useStyles();

    return <Field
        key={Guid.newGuid()}
        label={<Label weight={"semibold"}>{label}</Label>}
        orientation="vertical"
    >
        <div className={summaryField}>{value}</div>
    </Field>
}

export default memo(Panel)
