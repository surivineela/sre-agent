import { Button, DrawerBody, DrawerHeader, DrawerHeaderTitle, OverlayDrawer, Field, Label, Image, Spinner, Link, makeStyles, Text, Textarea, TextareaOnChangeData, Caption1, mergeClasses } from "@fluentui/react-components";
import { memo, useContext, useEffect, useState } from "react";
import { GraphContext, ResourceExtended } from "../Contracts/Graph";
import { Dismiss24Regular } from "@fluentui/react-icons";
import { Guid } from "../../Common/Helpers/Guid";
import axios from "axios";
import HealthStatus from "./HealthStatus";
import { getSafeDateTime } from "../../Common/Helpers/Date";
import { getPropertyValue, usePanel } from "../Hooks/usePanel";
import { getAppHealthInfo } from "./Utility";

export interface IPanelProps {
    transferDataToActivities: (threadId?: string | null) => void
}

const isNullOrUndefined = (input?: unknown): boolean => {
    return input === undefined || input === null;
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

const useStyles = makeStyles({
    spinner: {
        position: 'fixed',
        top: '50%',
        left: '50%'
    },
    root: {
        margin: '20px 0px',
    },
    textarea: {
        display: 'block',
        width: '100%',
    },
    textareaInner: {
        width: '100%'
    },
    section: {
        margin: '20px 0px'
    },
    sectionTitle: {
        lineHeight: '50px'
    },
    sectionField: {
        borderBottom: '1px solid rgba(204,204,204,.8)',
        padding: '10px 5px'
    },
    sectionFieldText: {
        wordBreak: 'break-word'
    },
    sectionFieldValueText: {
        padding: '0px 6px',
        gridRowStart: '1',
        gridRowEnd: '-1',
        lineHeight: '20px',
        display: 'flex',
        flexDirection: 'column',
        justifyContent: 'center',
    },
    dashboard: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        gap: '5px'
    }
})

const Panel = ({ transferDataToActivities }: IPanelProps) => {

    const { isLoading, initialRemarks, resource } = usePanel();

    const properties = resource?.properties;

    const [remarks, setRemarks] = useState<string>('');

    const { isPanelOpen, closePanel, selectedNode } = useContext(GraphContext);

    const { spinner, textarea, textareaInner, root, dashboard } = useStyles();

    useEffect(() => {
        setRemarks(initialRemarks);
    }, [initialRemarks])

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
                <div className={root}>
                    <Section title={'Annotation'}>
                        <Textarea
                            textarea={{
                                className: textareaInner,
                            }}
                            className={textarea}
                            placeholder="Add annotations to your resource"
                            value={remarks}
                            onChange={(_, data: TextareaOnChangeData) => {
                                setRemarks(data.value);
                            }} />
                    </Section>
                    <Section title={'Resource details'}>
                        <SummaryField label={'Name'} value={getPropertyValue(properties?.resourceName)} />
                        <SummaryField label={'Type'} value={getPropertyValue(properties?.resourceType)} />
                        <SummaryField label={'Resource group'} value={getPropertyValue(properties?.resourceGroupName)} />
                        <SummaryField label={'Subscription ID'} value={getPropertyValue(properties?.subscriptionId)} />
                        <AppHealthInfo resource={resource} transferDataToActivities={transferDataToActivities} />
                        <SummaryField label={'Dashboard URL'} >
                            {
                                resource?.dashboardUrl ?
                                    <div className={dashboard}>
                                        <Image src="./grafana-logo.svg" width={16} height={16} alt="Grafana logo" />
                                        <Link href={resource.dashboardUrl} target="_blank">View here</Link>
                                    </div> :
                                    null
                            }
                        </SummaryField>
                    </Section>
                </div>}
        </DrawerBody>
    </OverlayDrawer>
}

const AppHealthInfo = memo(({
    resource,
    transferDataToActivities
}: {
    resource?: ResourceExtended,
    transferDataToActivities: (threadId?: string | null) => void
}) => {
    const appHealthInfo = getAppHealthInfo(resource)

    const [isSendingReport, setSendingReport] = useState(false);

    return appHealthInfo && <div>
        <SummaryField
            label={'Costs for the past 7 days'}
            value={isNullOrUndefined(appHealthInfo.Costs) || appHealthInfo.Costs === 0 ? "Cost calculation pending" : `${appHealthInfo.Costs} USD`} />
        <SummaryField label={'Availability'} value={!isNullOrUndefined(appHealthInfo.Availability) ? `${appHealthInfo.Availability ?? '0'}%` : undefined} />
        <SummaryField label={'Health'}>
            <HealthStatus
                health={appHealthInfo.Health}
                showReportButton={true}
                onClickReportButton={async () => {
                    setSendingReport(true);
                    const thread = await createThread(getPropertyValue(resource?.properties?.resourceId));
                    setSendingReport(false);
                    transferDataToActivities(thread?.id);
                }}
                isSendingReport={isSendingReport} />
        </SummaryField>
        <SummaryField label={'Number of transactions for the past 30 minutes'} value={appHealthInfo.Transactions.toString()} />
        <SummaryField label={'Average latency'} value={!isNullOrUndefined(appHealthInfo.AvgLatencyInMs) ? `${(appHealthInfo.AvgLatencyInMs ?? 0) / 1000} seconds` : undefined} />
        <SummaryField label={'Average memory usage'} value={!isNullOrUndefined(appHealthInfo.AvgMemoryUsage) ? `${appHealthInfo.AvgMemoryUsage} bytes` : undefined} />
        <SummaryField label={'Average CPU usage'} value={!isNullOrUndefined(appHealthInfo.AvgCpuUsage) ? `${appHealthInfo.AvgCpuUsage}%` : undefined} />
        <SummaryField
            label={'Last data capture time'}
            value={!isNullOrUndefined(appHealthInfo.LastDataCaptureTimeStampInUTC) && appHealthInfo.LastDataCaptureTimeStampInUTC ? getSafeDateTime(appHealthInfo.LastDataCaptureTimeStampInUTC).toLocaleString() : undefined} />
    </div >
});

const Section = memo(({ title, children }: { title: string, children: JSX.Element | JSX.Element[] }) => {
    const { section, sectionTitle } = useStyles();

    return <div className={section}>
        <Text weight={'semibold'} size={400} className={sectionTitle}>{title}</Text>
        {children}
    </div>
})

const SummaryField = memo(({ label, value, children }: { label: string, value?: string, children?: JSX.Element | null }) => {
    const { sectionField, sectionFieldText, sectionFieldValueText } = useStyles();

    return (value || children) &&
        <div className={sectionField}>
            <Field
                key={Guid.newGuid()}
                label={<Label className={sectionFieldText}><Caption1>{label}</Caption1></Label>}
                orientation={"horizontal"}
            >
                <div className={mergeClasses(sectionFieldText, sectionFieldValueText)}>{value ?? children}</div>
            </Field>
        </div>
})

AppHealthInfo.displayName = 'AppHealthInfo';
Section.displayName = 'Section';
SummaryField.displayName = 'SummaryField';

export default memo(Panel)
