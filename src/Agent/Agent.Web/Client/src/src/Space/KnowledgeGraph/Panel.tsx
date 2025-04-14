import { Button, DrawerBody, DrawerHeader, DrawerHeaderTitle, OverlayDrawer, Field, Label, Image, Spinner, Link } from "@fluentui/react-components";
import { memo, useEffect, useState } from "react";
import { GraphNode, ResourceExtended, ScoreCardObject } from "../Hooks/useGraph";
import { Dismiss24Regular } from "@fluentui/react-icons";
import { Guid } from "../../Common/Helpers/Guid";
import axios from "axios";

interface IPanelProps {
    node?: GraphNode;
    setSelectedNode: (node?: GraphNode) => void;
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

const getSafeDateTime = (dateTime: Date | string): Date => {
    const stringFormat = getSafeDateString(dateTime);

    const dateString = stringFormat.toLowerCase();
    if (dateString.indexOf('z') === dateString.length - 1) {
        return new Date(stringFormat);
    } else {
        return new Date(stringFormat + 'Z');
    }
}

const getSafeDateString = (dateTime: Date | string): string => {
    if (dateTime instanceof Date && dateTime.toISOString && dateTime.toISOString()) {
        return dateTime.toISOString();
    } else {
        return dateTime.toString();
    }
}

const isNullOrUndefined = (input?: unknown): boolean => {
    return input === undefined || input === null;
}


type FieldSummary = { label: string, value: JSX.Element | string }

const Panel = ({ node, setSelectedNode, transferDataToActivities }: IPanelProps) => {
    const [isOpen, setIsOpen] = useState(false);
    const [summary, setSummary] = useState<FieldSummary[]>([]);
    const [isLoading, setIsLoading] = useState<boolean>(false);
    const [sendingReport, setSendingReport] = useState<boolean>(false);

    useEffect(() => {
        setIsOpen(!!node && node.isVisible);

    }, [node]);

    useEffect(() => {
        let isSubscribed = true;
        if (node) {
            if (node.type === 'subscription') {
                setSummary([
                    { label: 'Name', value: node.name },
                    { label: 'Type', value: 'Subscription' },
                ]);
                setIsLoading(false);
            } else {
                setIsLoading(true);
                getResource(node.id).then((resource) => {
                    if (resource && isSubscribed) {
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

                            let healthIconSrc = "";
                            let isNodeUnhealthy = false;
                            switch (Health?.toLowerCase()) {
                                case "unhealthy":
                                    healthIconSrc = "./failed.svg";
                                    isNodeUnhealthy = true;
                                    break;
                                case "healthy":
                                    healthIconSrc = "./success.svg";
                                    break;
                                case "degraded":
                                    healthIconSrc = "./warning.svg";
                                    break;
                            }

                            const healthValue = <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-start', alignItems: 'center', gap: '5px' }}>
                                {healthIconSrc && <Image src={healthIconSrc} width={16} height={16} />}
                                <span>{Health}</span>
                                {
                                    isNodeUnhealthy && (
                                        sendingReport ?
                                            <div style={{ display: 'flex', flexDirection: 'row', justifyContent: 'flex-start', alignItems: 'center', gap: '5px' }}>
                                                <Spinner size={'small'} />
                                                <span>{'Sending a report...'}</span>
                                            </div> :
                                            <Link onClick={async () => {
                                                setSendingReport(true);
                                                const thread = await createThread(resourceId[0]);
                                                setSendingReport(false);
                                                transferDataToActivities(thread?.id);
                                            }}>{'Report unhealthy node'}</Link>
                                    )
                                }
                            </div>

                            summary.push(
                                { label: 'Health', value: healthValue },
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
                                            <div style={{ display: 'flex', flexDirection: 'row', alignItems: 'center', gap: '5px' }}>
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

    }, [node, sendingReport, transferDataToActivities]);

    return <OverlayDrawer
        modalType="non-modal"
        open={isOpen}
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
                        onClick={() => setSelectedNode(undefined)}
                    />
                }
            >
                {node?.name ?? ''}
            </DrawerHeaderTitle>
        </DrawerHeader>

        <DrawerBody>
            {isLoading ?
                <Spinner size={'large'} style={{ position: 'fixed', top: '50%', left: '50%' }} /> :
                <div style={{ marginTop: '20px', display: 'flex', flexDirection: 'column', gap: '15px', justifyContent: 'center', alignItems: 'flex-start', flexWrap: 'wrap' }}>
                    {summary.map(({ label, value }) => {
                        return <SummaryField key={Guid.newGuid()} label={label} value={value} />
                    })}
                </div>}
        </DrawerBody>
    </OverlayDrawer>
}

const SummaryField = ({ label, value }: { label: string, value: JSX.Element | string }) => {

    return <Field
        key={Guid.newGuid()}
        label={<Label weight={"semibold"}>{label}</Label>}
        orientation="vertical"
    >
        <div style={{
            lineHeight: '20px',
            padding: '6px 0px'
        }}>{value}</div>
    </Field>
}

export default memo(Panel)
