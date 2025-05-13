import { IChartProps, LineChart } from '@fluentui/react-charting';
import { Badge, Button, Card, CardHeader, Text, tokens } from '@fluentui/react-components';
import {
    AlertUrgentFilled,
    ChatRegular,
    CheckmarkCircleRegular,
    ChevronDownRegular,
    ChevronUpRegular,
    ErrorCircleRegular,
    ShieldRegular,
    TaskListLtrRegular,
} from '@fluentui/react-icons';
import React, { useContext, useState } from 'react';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../Helpers/ResourceDescriptors';

interface AppHealthInfo {
    LastDataCaptureTimeStampInUTC: string;
    Health: string;
    Availability: number;
    Transactions: number; // including transactions in interface, but not displaying for now since not all resources have this
    Costs?: number;
    AvgMemoryUsage: number;
    AvgCpuUsage: number;
    AdditionalMetrics?: Record<string, any>;
    HistoricalData?: HistoricalDataPoint[];
}

interface HistoricalDataPoint {
    Timestamp: string;
    Availability: number;
    CpuUsage: number;
    MemoryUsage: number;
}

interface AppGroupResourceInfo {
    Name: string;
    Type: string;
    AppHealthInfo: AppHealthInfo;
}

interface AppGroupResourceSummary {
    SubscriptionId: string;
    SubscriptionName: string;
    AppGroups?: AppGroupResourceInfo[];
}

interface IncidentInfo {
    IncidentId: string;
    Name: string;
    CreateTime: string | null;
    Duration: string | null;
    Status: string;
    Impact: string;
    Resolution: string;
    InvestigationDetails: string;
    ThreadLink: string;
}

interface IncidentSummary {
    PagerDuty: IncidentInfo[];
    AzureMonitor: IncidentInfo[];
}

interface CVEInfo {
    RepoUrl: string;
    Number: number;
    State: string;
    Title: string;
    Description: string;
    Severity: string;
    CreatedAt: string;
    UpdatedAt: string | null;
    FixedAt: string | null;
}

interface CVESummary {
    Vulnerabilities: CVEInfo[];
    VulnerabilitiesByRepo: Record<string, string[]>;
    TotalVulnerabilities: number;
    CriticalVulnerabilities: number;
    HighVulnerabilities: number;
    ModerateVulnerabilities: number;
    LowVulnerabilities: number;
}

interface ActionItem {
    Priority: string;
    Description: string;
    ETA: string;
    Assignee?: string;
}

interface RecommendedActionsAndObservations {
    Actions: ActionItem[];
    Observations: string[];
}

interface SecurityOverview {
    Critical: number;
    High: number;
    Moderate: number;
    Low: number;
    TotalCount: number;
}

interface IncidentsOverview {
    Active: number;
    Mitigated: number;
    Resolved: number;
    TotalCount: number;
}

interface HealthPerformanceOverview {
    Healthy: number;
    Degraded: number;
    Unhealthy: number;
    TotalCount: number;
}

interface ReportOverview {
    SecurityFindings: SecurityOverview;
    Incidents: IncidentsOverview;
    HealthAndPerformance: HealthPerformanceOverview;
}

interface DailyReportData {
    ReportType: string;
    MetricsDescription: string;
    Timespan: string;
    Overview: ReportOverview;
    CVESummary: CVESummary | null;
    IncidentsSummary: IncidentSummary;
    AppGroupResourceSummary: AppGroupResourceSummary[];
    RecommendedActionsAndObservations: RecommendedActionsAndObservations | null;
}

interface SREDailyFormatProps {
    data: DailyReportData;
    timestamp?: string;
}

type SectionKey =
    | 'overview'
    | 'resources'
    | 'incidents'
    | 'actions'
    | 'security'
    | 'unhealthyResources'
    | 'degradedResources'
    | 'healthyResources';

// Status colors from design system
const STATUS_COLORS = {
    // Light theme solid colors
    CRITICAL: '#6E0811', // Deep red/burgundy for critical
    HIGH: '#C50F1F', // Bright red for high
    MODERATE: '#F7630C', // Orange for moderate/warning
    LOW: '#107C10', // Green for low/success

    // Status color map for semantic understanding
    UNHEALTHY: '#C50F1F', // Same as HIGH - bright red
    DEGRADED: '#F7630C', // Same as MODERATE - orange
    HEALTHY: '#107C10', // Same as LOW - green

    // Incident status colors
    ACTIVE: '#C50F1F', // Same as HIGH - bright red
    MITIGATED: '#F7630C', // Same as MODERATE - orange
    RESOLVED: '#107C10', // Same as LOW - green
};

// Helper function to get color for security severity
const getSecuritySeverityColor = (severity: string): string => {
    switch (severity.toLowerCase()) {
        case 'critical':
            return STATUS_COLORS.CRITICAL;
        case 'high':
            return STATUS_COLORS.HIGH;
        case 'moderate':
            return STATUS_COLORS.MODERATE;
        case 'low':
            return STATUS_COLORS.LOW;
        default:
            return 'inherit';
    }
};

// Helper function to get color for incident status
const getIncidentStatusColor = (status: string): string => {
    const lowerStatus = status.toLowerCase();
    switch (lowerStatus) {
        case 'active':
        case 'acknowledged':
            return STATUS_COLORS.ACTIVE;
        case 'mitigated':
            return STATUS_COLORS.MITIGATED;
        case 'resolved':
            return STATUS_COLORS.RESOLVED;
        default:
            return 'inherit';
    }
};

// Helper function to get color for health status
const getHealthStatusColor = (status: string): string => {
    switch (status.toLowerCase()) {
        case 'unhealthy':
            return STATUS_COLORS.UNHEALTHY;
        case 'degraded':
            return STATUS_COLORS.DEGRADED;
        case 'healthy':
            return STATUS_COLORS.HEALTHY;
        default:
            return 'inherit';
    }
};

const SecurityIcon = ({ color }: { color: string }) => <ShieldRegular style={{ color: color, fontSize: 20 }} />;

const IncidentsIcon = ({ color }: { color: string }) => (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ color }}>
        <path
            d="M12.0004 2.00195C9.86006 2.00195 8.125 3.73701 8.125 5.87732C8.125 8.79606 9.33243 12.4289 9.93776 14.0759C10.2606 14.9542 11.097 15.4995 12.0025 15.4995C12.9057 15.4995 13.7409 14.957 14.0646 14.0809C14.6705 12.4413 15.8757 8.827 15.8757 5.87732C15.8757 3.73701 14.1407 2.00195 12.0004 2.00195ZM12.0011 17.001C10.6198 17.001 9.5 18.1208 9.5 19.5021C9.5 20.8834 10.6198 22.0032 12.0011 22.0032C13.3825 22.0032 14.5022 20.8834 14.5022 19.5021C14.5022 18.1208 13.3825 17.001 12.0011 17.001Z"
            fill="currentColor"
        />
    </svg>
);

const HealthPerformanceIcon = ({ color }: { color: string }) => (
    <svg width="20" height="20" viewBox="0 0 24 24" fill="none" xmlns="http://www.w3.org/2000/svg" style={{ color }}>
        <path
            d="M8.46238 6.80905L11.746 20.426C11.9236 21.1626 12.957 21.2011 13.1891 20.4798L16.4456 10.3575L17.0318 12.4532C17.1224 12.7772 17.4176 13.0012 17.7541 13.0012H21.2477C21.6619 13.0012 21.9977 12.6654 21.9977 12.2512C21.9977 11.837 21.6619 11.5012 21.2477 11.5012H18.3231L17.2181 7.55053C17.0179 6.83439 16.0096 6.81496 15.7819 7.52284L12.5785 17.4797L9.22531 3.57419C9.04279 2.81728 7.97039 2.80542 7.77117 3.5581L5.66883 11.5012H2.75C2.33579 11.5012 2 11.837 2 12.2512C2 12.6654 2.33579 13.0012 2.75 13.0012H6.24614C6.58645 13.0012 6.88411 12.7721 6.97118 12.4431L8.46238 6.80905Z"
            fill="currentColor"
        />
    </svg>
);

// Main component that renders the entire dashboard
const DailyReport: React.FC<SREDailyFormatProps> = ({ data, timestamp }) => {
    // Get resource information at component level
    const { resourceId } = useContext(EnvironmentContext);
    // Create formatted path for incident links
    const formattedResourcePath = resourceId
        ? 'subscriptions%2F' +
          new ArmResourceDescriptor(resourceId).subscription +
          '%2FresourceGroups%2F' +
          new ArmResourceDescriptor(resourceId).resourceGroup +
          '%2Fproviders%2FMicrosoft.App%2Fagents%2F' +
          new ArmResourceDescriptor(resourceId).resourceName
        : '';

    // Helper function to format bytes
    const formatBytes = (bytes: number) => {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return `${parseFloat((bytes / Math.pow(k, i)).toFixed(2))} ${sizes[i]}`;
    };

    // Resource health status counts
    const healthStatusCounts: Record<string, number> = {
        Healthy: 0,
        Warning: 0,
        Degraded: 0,
        Unhealthy: 0,
        Critical: 0,
    };

    // Count resources by health status
    data.AppGroupResourceSummary.forEach(sub => {
        (sub.AppGroups || []).forEach(app => {
            const status = app.AppHealthInfo.Health;
            if (status in healthStatusCounts) {
                healthStatusCounts[status]++;
            }
        });
    });

    // Accordion state
    const [openSections, setOpenSections] = useState<Record<SectionKey, boolean>>({
        overview: true,
        resources: false,
        incidents: false,
        actions: true,
        security: false,
        unhealthyResources: false,
        degradedResources: false,
        healthyResources: false,
    });

    // Track expanded incidents
    const [expandedIncidents, setExpandedIncidents] = useState<Record<string, boolean>>({});

    // Toggle incident expansion
    const toggleIncident = (incidentId: string) => {
        setExpandedIncidents(prev => ({
            ...prev,
            [incidentId]: !prev[incidentId],
        }));
    };

    // Toggle accordion sections
    const toggleSection = (section: SectionKey) => {
        setOpenSections({
            ...openSections,
            [section]: !openSections[section],
        });
    };

    // Function to render resource cards
    const renderResourceCards = (resources: AppGroupResourceInfo[]) => {
        return resources.map((resource, resIndex) => (
            <Card
                key={resIndex}
                style={{
                    overflow: 'visible',
                    border: `1px solid ${tokens.colorNeutralStroke1}`,
                    marginBottom: '16px',
                    borderRadius: '8px',
                    boxShadow: '0px 2px 4px rgba(0, 0, 0, 0.05)',
                    padding: '16px',
                    width: '100%',
                }}
            >
                <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '16px' }}>
                    <div style={{ width: '50%' }}>
                        <Text size={300} style={{ color: tokens.colorNeutralForeground3, marginBottom: '4px' }}>
                            App group resource name
                        </Text>
                        <br />
                        <Text style={{ marginTop: '8px' }}>{resource.Name}</Text>
                    </div>
                    <div>
                        <Text size={300} style={{ color: tokens.colorNeutralForeground3, marginBottom: '4px' }}>
                            App group type
                        </Text>
                        <br />
                        <Text style={{ marginTop: '8px' }}>{resource.Type}</Text>
                    </div>
                </div>

                <div style={{ display: 'flex' }}>
                    <div style={{ width: '25%', paddingRight: '20px' }}>
                        {/* Availability */}
                        <div style={{ marginBottom: '16px', display: 'flex' }}>
                            <div
                                style={{
                                    width: '4px',
                                    backgroundColor: tokens.colorPaletteBlueForeground2,
                                    borderRadius: '2px',
                                    marginRight: '8px',
                                }}
                            ></div>
                            <div>
                                <Text style={{ display: 'block', marginBottom: '4px' }}>Availability</Text>
                                <Text
                                    size={600}
                                    weight="semibold"
                                    style={{
                                        color:
                                            resource.AppHealthInfo.Availability < 99.5
                                                ? tokens.colorPaletteDarkOrangeForeground1
                                                : 'inherit',
                                    }}
                                >
                                    {resource.AppHealthInfo.Availability.toFixed(4)}%
                                </Text>
                            </div>
                        </div>

                        {/* CPU Usage */}
                        <div style={{ marginBottom: '16px', display: 'flex' }}>
                            <div
                                style={{
                                    width: '4px',
                                    backgroundColor: tokens.colorPalettePinkForeground2,
                                    borderRadius: '2px',
                                    marginRight: '8px',
                                }}
                            ></div>
                            <div>
                                <Text style={{ display: 'block', marginBottom: '4px' }}>CPU usage</Text>
                                <Text
                                    size={600}
                                    weight="semibold"
                                    style={{
                                        color:
                                            resource.AppHealthInfo.AvgCpuUsage > 80 ? tokens.colorPaletteDarkOrangeForeground1 : 'inherit',
                                    }}
                                >
                                    {resource.AppHealthInfo.AvgCpuUsage.toFixed(4)}%
                                </Text>
                            </div>
                        </div>

                        {/* Memory */}
                        <div style={{ marginBottom: '16px', display: 'flex' }}>
                            <div
                                style={{
                                    width: '4px',
                                    backgroundColor: tokens.colorPaletteTealForeground2,
                                    borderRadius: '2px',
                                    marginRight: '8px',
                                }}
                            ></div>
                            <div>
                                <Text style={{ display: 'block', marginBottom: '4px' }}>Memory</Text>
                                <Text size={600} weight="semibold">
                                    {formatBytes(resource.AppHealthInfo.AvgMemoryUsage)}
                                </Text>
                            </div>
                        </div>
                    </div>

                    <div style={{ width: '75%' }}>
                        {resource.AppHealthInfo.Health.toLowerCase() === 'unhealthy' &&
                        resource.AppHealthInfo.HistoricalData &&
                        resource.AppHealthInfo.HistoricalData.length > 0 ? (
                            renderHistoricalDataChart(resource.AppHealthInfo.HistoricalData)
                        ) : resource.AppHealthInfo.Health.toLowerCase() === 'unhealthy' ? (
                            <div
                                style={{
                                    height: '100%',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    color: tokens.colorNeutralForeground3,
                                    border: `1px dashed ${tokens.colorNeutralStroke2}`,
                                    borderRadius: '4px',
                                    padding: '16px',
                                }}
                            >
                                <Text>No historical data available</Text>
                            </div>
                        ) : null}
                    </div>
                </div>
            </Card>
        ));
    };

    // Function to render a chart using Fluent UI LineChart
    const renderHistoricalDataChart = (historicalData: HistoricalDataPoint[]) => {
        if (!historicalData || historicalData.length < 2) {
            return (
                <div
                    style={{
                        height: '100%',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        color: tokens.colorNeutralForeground3,
                        border: `1px dashed ${tokens.colorNeutralStroke2}`,
                        borderRadius: '4px',
                        padding: '16px',
                    }}
                >
                    <Text>Insufficient data points for chart</Text>
                </div>
            );
        }

        // Sort data by timestamp to ensure correct x-axis ordering
        const sortedData = [...historicalData].sort((a, b) => new Date(a.Timestamp).getTime() - new Date(b.Timestamp).getTime());

        // Format data for FluentUI LineChart
        const chartData: IChartProps = {
            chartTitle: 'Resource Metrics',
            lineChartData: [
                {
                    legend: 'Availability',
                    data: sortedData.map(d => ({ x: new Date(d.Timestamp), y: d.Availability })),
                    color: tokens.colorPaletteBlueForeground2,
                    lineOptions: {
                        strokeWidth: 2,
                    },
                },
                {
                    legend: 'CPU Usage',
                    data: sortedData.map(d => ({ x: new Date(d.Timestamp), y: d.CpuUsage })),
                    color: tokens.colorPalettePinkForeground2,
                    lineOptions: {
                        strokeWidth: 2,
                    },
                },
                {
                    legend: 'Memory Usage',
                    data: sortedData.map(d => ({ x: new Date(d.Timestamp), y: d.MemoryUsage })),
                    color: tokens.colorPaletteTealForeground2,
                    lineOptions: {
                        strokeWidth: 2,
                    },
                },
            ],
        };

        return (
            <div style={{ width: '100%', height: '100%' }}>
                <LineChart
                    data={chartData}
                    height={200}
                    width={600}
                    margins={{ left: 30, top: 20, bottom: 50, right: 10 }}
                    hideLegend={true}
                    yAxisTickFormat={(value: number) => `${value}K`}
                    yMaxValue={100}
                />
                <div
                    style={{
                        display: 'flex',
                        flexWrap: 'wrap',
                        justifyContent: 'center',
                        gap: '10px',
                        marginTop: '4px',
                        fontSize: '10px',
                        position: 'relative',
                        bottom: '5px',
                    }}
                >
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <div
                            style={{
                                width: '10px',
                                height: '2px',
                                backgroundColor: tokens.colorPaletteBlueForeground2,
                                marginRight: '2px',
                            }}
                        ></div>
                        <span>Availability</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <div
                            style={{
                                width: '10px',
                                height: '2px',
                                backgroundColor: tokens.colorPalettePinkForeground2,
                                marginRight: '2px',
                            }}
                        ></div>
                        <span>CPU Usage</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center' }}>
                        <div
                            style={{
                                width: '10px',
                                height: '2px',
                                backgroundColor: tokens.colorPaletteTealForeground2,
                                marginRight: '2px',
                            }}
                        ></div>
                        <span>Memory Usage</span>
                    </div>
                </div>
            </div>
        );
    };

    const formatDateTime = (dateTimeString: string): string => {
        const date = new Date(dateTimeString);

        // Format time: 3:27 PM
        const timeOptions: Intl.DateTimeFormatOptions = {
            hour: 'numeric',
            minute: '2-digit',
            hour12: true,
        };
        const timeStr = date.toLocaleTimeString('en-US', timeOptions);

        // Format date: 9/10/25
        const dateOptions: Intl.DateTimeFormatOptions = {
            month: 'numeric',
            day: 'numeric',
            year: '2-digit',
        };
        const dateStr = date.toLocaleDateString('en-US', dateOptions);

        return `${timeStr}, ${dateStr}`;
    };

    const formatDuration = (durationString: string | null): string => {
        if (!durationString) return 'N/A';

        // If it already has the right format (HH:MM:SS), just return it
        if (/^\d{2}:\d{2}:\d{2}/.test(durationString)) {
            return durationString.split('.')[0]; // Remove any milliseconds if present
        }

        // For other formats, try to parse and convert
        try {
            // If it's a timespan format like "11:53:03.0645798"
            if (durationString.includes('.')) {
                return durationString.split('.')[0]; // Just keep HH:MM:SS part
            }

            return durationString; // Return as is if nothing else matches
        } catch (e) {
            return durationString; // Return original if parsing fails
        }
    };

    return (
        <div className="dashboard-container" style={{ backgroundColor: 'white' }}>
            {/* Header */}
            <div
                className="header"
                style={{
                    padding: '16px 0',
                    borderBottom: `1px solid ${tokens.colorNeutralStroke1}`,
                    backgroundColor: tokens.colorNeutralBackground1,
                }}
            >
                <div className="header-container" style={{ maxWidth: '1400px', margin: '0 auto', padding: '0 24px' }}>
                    <div className="report-header">
                        <Text as="h1" size={600} weight="semibold" style={{ margin: '0' }}>
                            {timestamp
                                ? new Date(timestamp)
                                      .toLocaleDateString('en-US', {
                                          month: 'numeric',
                                          day: 'numeric',
                                          year: 'numeric',
                                      })
                                      .replace(/\//g, '/')
                                : new Date()
                                      .toLocaleDateString('en-US', {
                                          month: 'numeric',
                                          day: 'numeric',
                                          year: 'numeric',
                                      })
                                      .replace(/\//g, '/')}{' '}
                            Resource Report
                        </Text>
                    </div>
                </div>
            </div>

            {/* Main content */}
            <div className="main-content" style={{ maxWidth: '1400px', margin: '0 auto', padding: '24px', backgroundColor: 'white' }}>
                {/* Overview Cards */}
                <div
                    style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(3, 1fr)',
                        gap: '16px',
                        marginBottom: '32px',
                    }}
                >
                    {/* Respository Insights OverviewCard */}
                    <Card
                        style={{
                            boxShadow: '0px 4px 8px rgba(0, 0, 0, 0.15)',
                            overflow: 'hidden',
                            borderRadius: '8px',
                            padding: '24px',
                            border: 'none',
                            display: 'flex',
                            flexDirection: 'column',
                        }}
                    >
                        <div style={{ marginBottom: '24px', height: '24px' }}>
                            <Text size={400} weight="semibold">
                                Repository insights
                            </Text>
                        </div>

                        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'auto' }}>
                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Critical
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getSecuritySeverityColor('Critical') }}>
                                    {data.Overview.SecurityFindings.Critical}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    High
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getSecuritySeverityColor('High') }}>
                                    {data.Overview.SecurityFindings.High}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Moderate
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getSecuritySeverityColor('Moderate') }}>
                                    {data.Overview.SecurityFindings.Moderate}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Low
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getSecuritySeverityColor('Low') }}>
                                    {data.Overview.SecurityFindings.Low}
                                </Text>
                            </div>
                        </div>
                    </Card>

                    {/* Incidents Overview Card */}
                    <Card
                        style={{
                            boxShadow: '0px 4px 8px rgba(0, 0, 0, 0.15)',
                            overflow: 'hidden',
                            borderRadius: '8px',
                            padding: '24px',
                            border: 'none',
                            display: 'flex',
                            flexDirection: 'column',
                        }}
                    >
                        <div style={{ marginBottom: '24px', height: '24px' }}>
                            <Text size={400} weight="semibold">
                                Incidents summary
                            </Text>
                        </div>

                        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'auto' }}>
                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Active
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getIncidentStatusColor('Active') }}>
                                    {data.Overview.Incidents.Active}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Mitigated
                                </Text>
                                <Text
                                    weight="semibold"
                                    size={700}
                                    style={{
                                        color:
                                            data.Overview.Incidents.Mitigated > 0
                                                ? getIncidentStatusColor('Active')
                                                : getIncidentStatusColor('Mitigated'),
                                    }}
                                >
                                    {data.Overview.Incidents.Mitigated}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Resolved
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getIncidentStatusColor('Resolved') }}>
                                    {data.Overview.Incidents.Resolved}
                                </Text>
                            </div>
                        </div>
                    </Card>

                    {/* Health + Performance Overview Card */}
                    <Card
                        style={{
                            boxShadow: '0px 4px 8px rgba(0, 0, 0, 0.15)',
                            overflow: 'hidden',
                            borderRadius: '8px',
                            padding: '24px',
                            border: 'none',
                            display: 'flex',
                            flexDirection: 'column',
                        }}
                    >
                        <div style={{ marginBottom: '24px', height: '24px', display: 'flex', alignItems: 'center' }}>
                            <Text size={400} weight="semibold">
                                App group health + performance
                            </Text>
                        </div>

                        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: 'auto' }}>
                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Unhealthy
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getHealthStatusColor('Unhealthy') }}>
                                    {data.Overview.HealthAndPerformance.Unhealthy}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Degraded
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getHealthStatusColor('Degraded') }}>
                                    {data.Overview.HealthAndPerformance.Degraded}
                                </Text>
                            </div>

                            <div style={{ textAlign: 'center' }}>
                                <Text size={300} style={{ display: 'block', marginBottom: '4px' }}>
                                    Healthy
                                </Text>
                                <Text weight="semibold" size={700} style={{ color: getHealthStatusColor('Healthy') }}>
                                    {data.Overview.HealthAndPerformance.Healthy}
                                </Text>
                            </div>
                        </div>
                    </Card>
                </div>

                {/* Repository Insights Section */}
                <div style={{ marginBottom: '16px' }} data-section="security">
                    <Card
                        onClick={() => toggleSection('security')}
                        style={{
                            backgroundColor: tokens.colorNeutralBackground2,
                            padding: '12px 16px',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            boxShadow: 'none',
                            border: 'none',
                        }}
                    >
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '12px',
                            }}
                        >
                            {openSections.security ? (
                                <ChevronDownRegular
                                    style={{
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            ) : (
                                <ChevronUpRegular
                                    style={{
                                        transform: 'rotate(90deg)',
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            )}
                            <SecurityIcon
                                color={
                                    data.Overview.SecurityFindings.Critical > 0
                                        ? getSecuritySeverityColor('Critical')
                                        : data.Overview.SecurityFindings.High > 0
                                          ? getSecuritySeverityColor('High')
                                          : data.Overview.SecurityFindings.Moderate > 0
                                            ? getSecuritySeverityColor('Moderate')
                                            : getSecuritySeverityColor('Low')
                                }
                            />
                            <Text size={400} weight="semibold">
                                Repository insights ({data.Overview.SecurityFindings.TotalCount})
                            </Text>
                        </div>
                    </Card>

                    {openSections.security && (
                        <div style={{ padding: '16px 0' }}>
                            {!data.CVESummary || data.CVESummary.TotalVulnerabilities === 0 ? (
                                <Card style={{ textAlign: 'center', padding: '32px 16px' }}>
                                    <div style={{ color: tokens.colorPaletteGreenForeground2, fontSize: '24px', marginBottom: '16px' }}>
                                        <CheckmarkCircleRegular fontSize={32} />
                                    </div>
                                    <Text weight="semibold" style={{ display: 'block', textAlign: 'center' }}>
                                        <Text weight="semibold">0</Text> repository alerts found
                                    </Text>
                                </Card>
                            ) : (
                                <div>
                                    {data.CVESummary.Vulnerabilities.map((vuln, idx) => {
                                        // Determine severity and color
                                        const severity = (vuln.Severity || 'Low').toLowerCase();
                                        const badgeColor = getSecuritySeverityColor(vuln.Severity || 'Low');
                                        let badgeBgColor = tokens.colorPaletteGreenBackground2;

                                        if (severity === 'critical') {
                                            badgeBgColor = tokens.colorPaletteRedBackground2;
                                        } else if (severity === 'high') {
                                            badgeBgColor = tokens.colorPaletteDarkOrangeBackground2;
                                        } else if (severity === 'moderate') {
                                            badgeBgColor = tokens.colorPaletteYellowBackground2;
                                        }

                                        return (
                                            <Card
                                                key={idx}
                                                style={{
                                                    marginBottom: '16px',
                                                    boxShadow: tokens.shadow4,
                                                    borderLeft: `4px solid ${badgeColor}`,
                                                }}
                                            >
                                                <CardHeader
                                                    header={
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
                                                            <ShieldRegular style={{ color: badgeColor }} />
                                                            <Text weight="semibold" size={400}>
                                                                {vuln.Title}
                                                            </Text>
                                                        </div>
                                                    }
                                                    action={
                                                        <Badge
                                                            appearance="filled"
                                                            style={{
                                                                backgroundColor: badgeBgColor,
                                                                color: badgeColor,
                                                                minWidth: 60,
                                                                textAlign: 'center',
                                                                fontWeight: 600,
                                                            }}
                                                        >
                                                            {vuln.Severity || 'Low'}
                                                        </Badge>
                                                    }
                                                />
                                                <div style={{ padding: tokens.spacingVerticalM }}>
                                                    <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '8px' }}>
                                                        <Text block>
                                                            <Text weight="semibold">Description:</Text> {vuln.Description}
                                                        </Text>
                                                        <Text block>
                                                            <Text weight="semibold">Repository:</Text>{' '}
                                                            <a
                                                                href={vuln.RepoUrl}
                                                                target="_blank"
                                                                rel="noopener noreferrer"
                                                                style={{
                                                                    color: tokens.colorBrandForeground1,
                                                                    textDecoration: 'none',
                                                                }}
                                                            >
                                                                {vuln.RepoUrl}
                                                            </a>
                                                        </Text>
                                                        <Text block>
                                                            <Text weight="semibold">State:</Text> {vuln.State}
                                                        </Text>
                                                    </div>
                                                </div>
                                            </Card>
                                        );
                                    })}
                                </div>
                            )}
                        </div>
                    )}
                </div>

                {/* Incidents Section */}
                <div style={{ marginBottom: '16px' }} data-section="incidents">
                    <Card
                        onClick={() => toggleSection('incidents')}
                        style={{
                            backgroundColor: tokens.colorNeutralBackground2,
                            padding: '12px 16px',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            boxShadow: 'none',
                            border: 'none',
                        }}
                    >
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '12px',
                            }}
                        >
                            {openSections.incidents ? (
                                <ChevronDownRegular
                                    style={{
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            ) : (
                                <ChevronUpRegular
                                    style={{
                                        transform: 'rotate(90deg)',
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            )}
                            <IncidentsIcon
                                color={
                                    data.Overview.Incidents.Active > 0 || data.Overview.Incidents.Mitigated > 0
                                        ? getIncidentStatusColor('Active')
                                        : getIncidentStatusColor('Resolved')
                                }
                            />
                            <Text size={400} weight="semibold">
                                Incidents Summary ({data.Overview.Incidents.Active})
                            </Text>
                        </div>
                    </Card>

                    {openSections.incidents && (
                        <div style={{ padding: '16px 0' }}>
                            {(data.IncidentsSummary.PagerDuty?.length || 0) === 0 &&
                            (data.IncidentsSummary.AzureMonitor?.length || 0) === 0 ? (
                                <Card style={{ textAlign: 'center', padding: '32px 16px' }}>
                                    <div style={{ color: tokens.colorPaletteGreenForeground2, fontSize: '24px', marginBottom: '16px' }}>
                                        <CheckmarkCircleRegular fontSize={32} />
                                    </div>
                                    <Text weight="semibold" style={{ display: 'block', textAlign: 'center' }}>
                                        <Text weight="semibold">0</Text> incidents reported
                                    </Text>
                                </Card>
                            ) : (
                                <>
                                    {data.IncidentsSummary.PagerDuty?.map((incident, index) => (
                                        <Card
                                            key={index}
                                            style={{
                                                marginBottom: '16px',
                                                boxShadow: 'none',
                                                border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                borderRadius: '8px',
                                                overflow: 'hidden',
                                            }}
                                        >
                                            <div
                                                onClick={() => toggleIncident(incident.IncidentId)}
                                                style={{
                                                    padding: '16px',
                                                    cursor: 'pointer',
                                                    backgroundColor: tokens.colorNeutralBackground2,
                                                    borderBottom: expandedIncidents[incident.IncidentId]
                                                        ? `1px solid ${tokens.colorNeutralStroke1}`
                                                        : 'none',
                                                }}
                                            >
                                                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                                                    <div style={{ display: 'flex', flexDirection: 'column' }}>
                                                        <Text weight="semibold">{incident.Name}</Text>
                                                        <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>
                                                            {incident.IncidentId}
                                                        </Text>
                                                    </div>
                                                    <div style={{ display: 'flex', alignItems: 'center' }}>
                                                        <Badge
                                                            appearance="filled"
                                                            style={{
                                                                backgroundColor: 'transparent',
                                                                color: getIncidentStatusColor(incident.Status),
                                                                borderRadius: '16px',
                                                                padding: '2px 12px',
                                                                marginRight: '8px',
                                                            }}
                                                        >
                                                            {incident.Status}
                                                        </Badge>
                                                        {expandedIncidents[incident.IncidentId] ? (
                                                            <ChevronDownRegular style={{ color: tokens.colorNeutralForeground2 }} />
                                                        ) : (
                                                            <ChevronUpRegular
                                                                style={{
                                                                    transform: 'rotate(90deg)',
                                                                    color: tokens.colorNeutralForeground2,
                                                                }}
                                                            />
                                                        )}
                                                    </div>
                                                </div>
                                            </div>

                                            {expandedIncidents[incident.IncidentId] && (
                                                <div style={{ padding: '16px' }}>
                                                    {incident.Impact && (
                                                        <div
                                                            style={{
                                                                backgroundColor: '#FFF4CE',
                                                                border: '1px solid #F9E5A7',
                                                                borderRadius: '4px',
                                                                padding: '12px 16px',
                                                                marginBottom: '16px',
                                                                display: 'flex',
                                                                alignItems: 'flex-start',
                                                                gap: '8px',
                                                            }}
                                                        >
                                                            <AlertUrgentFilled style={{ color: '#D83B01', marginTop: '2px' }} />
                                                            <div>
                                                                <Text weight="semibold">Impact</Text> {incident.Impact}
                                                            </div>
                                                        </div>
                                                    )}

                                                    <div style={{ marginBottom: '16px' }}>
                                                        <div
                                                            style={{
                                                                display: 'flex',
                                                                marginBottom: '8px',
                                                                color: tokens.colorNeutralForeground3,
                                                            }}
                                                        >
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text size={300}>Incident ID</Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text size={300}>Created</Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text size={300}>Duration</Text>
                                                            </div>
                                                        </div>
                                                        <div
                                                            style={{
                                                                display: 'flex',
                                                                color: tokens.colorNeutralForeground1,
                                                                marginBottom: '20px',
                                                            }}
                                                        >
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text>{incident.IncidentId}</Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text>
                                                                    {incident.CreateTime ? formatDateTime(incident.CreateTime) : 'N/A'}
                                                                </Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text>{formatDuration(incident.Duration)}</Text>
                                                            </div>
                                                        </div>
                                                    </div>

                                                    {(incident.InvestigationDetails || incident.Resolution) && (
                                                        <div style={{ marginBottom: '16px' }}>
                                                            <Text>{incident.InvestigationDetails || incident.Resolution}</Text>
                                                        </div>
                                                    )}

                                                    {incident.ThreadLink && (
                                                        <div style={{ marginTop: '16px' }}>
                                                            <Button
                                                                appearance="outline"
                                                                icon={<ChatRegular />}
                                                                onClick={() =>
                                                                    window.open(`${incident.ThreadLink}${formattedResourcePath}`, '_blank')
                                                                }
                                                                style={{
                                                                    display: 'flex',
                                                                    alignItems: 'center',
                                                                    gap: '8px',
                                                                    border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                                    borderRadius: '4px',
                                                                    padding: '6px 12px',
                                                                    color: tokens.colorNeutralForeground1,
                                                                }}
                                                            >
                                                                Go to incident thread
                                                            </Button>
                                                        </div>
                                                    )}
                                                </div>
                                            )}
                                        </Card>
                                    ))}

                                    {data.IncidentsSummary.AzureMonitor?.map((incident, index) => (
                                        <Card
                                            key={`azure-${index}`}
                                            style={{
                                                marginBottom: '16px',
                                                boxShadow: 'none',
                                                border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                borderRadius: '8px',
                                                overflow: 'hidden',
                                            }}
                                        >
                                            <div
                                                onClick={() => toggleIncident(incident.IncidentId)}
                                                style={{
                                                    padding: '16px',
                                                    cursor: 'pointer',
                                                    backgroundColor: tokens.colorNeutralBackground2,
                                                    borderBottom: expandedIncidents[incident.IncidentId]
                                                        ? `1px solid ${tokens.colorNeutralStroke1}`
                                                        : 'none',
                                                }}
                                            >
                                                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                                                    <div style={{ display: 'flex', flexDirection: 'column' }}>
                                                        <Text weight="semibold">{incident.Name}</Text>
                                                        <Text size={200} style={{ color: tokens.colorNeutralForeground2 }}>
                                                            {incident.IncidentId}
                                                        </Text>
                                                    </div>
                                                    <div style={{ display: 'flex', alignItems: 'center' }}>
                                                        <Badge
                                                            appearance="filled"
                                                            style={{
                                                                backgroundColor: 'transparent',
                                                                color: getIncidentStatusColor(incident.Status),
                                                                borderRadius: '16px',
                                                                padding: '2px 12px',
                                                                marginRight: '8px',
                                                            }}
                                                        >
                                                            {incident.Status}
                                                        </Badge>
                                                        {expandedIncidents[incident.IncidentId] ? (
                                                            <ChevronDownRegular style={{ color: tokens.colorNeutralForeground2 }} />
                                                        ) : (
                                                            <ChevronUpRegular
                                                                style={{
                                                                    transform: 'rotate(90deg)',
                                                                    color: tokens.colorNeutralForeground2,
                                                                }}
                                                            />
                                                        )}
                                                    </div>
                                                </div>
                                            </div>

                                            {expandedIncidents[incident.IncidentId] && (
                                                <div style={{ padding: '16px' }}>
                                                    {incident.Impact && (
                                                        <div
                                                            style={{
                                                                backgroundColor: '#FFF4CE',
                                                                border: '1px solid #F9E5A7',
                                                                borderRadius: '4px',
                                                                padding: '12px 16px',
                                                                marginBottom: '16px',
                                                                display: 'flex',
                                                                alignItems: 'flex-start',
                                                                gap: '8px',
                                                            }}
                                                        >
                                                            <AlertUrgentFilled style={{ color: '#D83B01', marginTop: '2px' }} />
                                                            <div>
                                                                <Text weight="semibold">Impact</Text> {incident.Impact}
                                                            </div>
                                                        </div>
                                                    )}

                                                    <div style={{ marginBottom: '16px' }}>
                                                        <div
                                                            style={{
                                                                display: 'flex',
                                                                marginBottom: '8px',
                                                                color: tokens.colorNeutralForeground3,
                                                            }}
                                                        >
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text size={300}>Incident ID</Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text size={300}>Created</Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text size={300}>Duration</Text>
                                                            </div>
                                                        </div>
                                                        <div
                                                            style={{
                                                                display: 'flex',
                                                                color: tokens.colorNeutralForeground1,
                                                                marginBottom: '20px',
                                                            }}
                                                        >
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text>{incident.IncidentId}</Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text>
                                                                    {incident.CreateTime ? formatDateTime(incident.CreateTime) : 'N/A'}
                                                                </Text>
                                                            </div>
                                                            <div style={{ flex: '1', marginRight: '8px' }}>
                                                                <Text>{formatDuration(incident.Duration)}</Text>
                                                            </div>
                                                        </div>
                                                    </div>

                                                    {(incident.InvestigationDetails || incident.Resolution) && (
                                                        <div style={{ marginBottom: '16px' }}>
                                                            <Text>{incident.InvestigationDetails || incident.Resolution}</Text>
                                                        </div>
                                                    )}

                                                    {incident.ThreadLink && (
                                                        <div style={{ marginTop: '16px' }}>
                                                            <Button
                                                                appearance="outline"
                                                                icon={<ChatRegular />}
                                                                onClick={() =>
                                                                    window.open(`${incident.ThreadLink}${formattedResourcePath}`, '_blank')
                                                                }
                                                                style={{
                                                                    display: 'flex',
                                                                    alignItems: 'center',
                                                                    gap: '8px',
                                                                    border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                                    borderRadius: '4px',
                                                                    padding: '6px 12px',
                                                                    color: tokens.colorNeutralForeground1,
                                                                }}
                                                            >
                                                                Go to incident thread
                                                            </Button>
                                                        </div>
                                                    )}
                                                </div>
                                            )}
                                        </Card>
                                    ))}
                                </>
                            )}
                        </div>
                    )}
                </div>

                {/* Resources Section */}
                <div style={{ marginBottom: '16px' }} data-section="resources">
                    <Card
                        onClick={() => toggleSection('resources')}
                        style={{
                            backgroundColor: tokens.colorNeutralBackground2,
                            padding: '12px 16px',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            boxShadow: 'none',
                            border: 'none',
                        }}
                    >
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '12px',
                            }}
                        >
                            {openSections.resources ? (
                                <ChevronDownRegular
                                    style={{
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            ) : (
                                <ChevronUpRegular
                                    style={{
                                        transform: 'rotate(90deg)',
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            )}
                            <HealthPerformanceIcon
                                color={
                                    data.Overview.HealthAndPerformance.Unhealthy > 0
                                        ? getHealthStatusColor('Unhealthy')
                                        : data.Overview.HealthAndPerformance.Degraded > 0
                                          ? getHealthStatusColor('Degraded')
                                          : getHealthStatusColor('Healthy')
                                }
                            />
                            <Text size={400} weight="semibold">
                                App group health + performance ({data.Overview.HealthAndPerformance.Unhealthy})
                            </Text>
                        </div>
                    </Card>

                    {openSections.resources && (
                        <div style={{ padding: '16px 0' }}>
                            {data.AppGroupResourceSummary.length === 0 ? (
                                <Card style={{ textAlign: 'center', padding: '32px 16px' }}>
                                    <div style={{ color: tokens.colorPaletteGreenForeground2, fontSize: '24px', marginBottom: '16px' }}>
                                        <CheckmarkCircleRegular fontSize={32} />
                                    </div>
                                    <Text weight="semibold">
                                        <Text weight="semibold">0</Text> resources available in this period
                                    </Text>
                                </Card>
                            ) : (
                                <>
                                    {/* Health + Performance Subfolders */}
                                    {/* Unhealthy App Groups Subfolder */}
                                    <div style={{ marginBottom: '16px' }}>
                                        <Card
                                            onClick={() => {
                                                setOpenSections(prev => ({ ...prev, unhealthyResources: !prev.unhealthyResources }));
                                            }}
                                            style={{
                                                backgroundColor: tokens.colorNeutralBackground2,
                                                padding: '12px 16px 12px 36px',
                                                borderRadius: '4px',
                                                cursor: 'pointer',
                                                boxShadow: 'none',
                                                border: 'none',
                                            }}
                                        >
                                            <div
                                                style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '12px',
                                                }}
                                            >
                                                {openSections.unhealthyResources ? (
                                                    <ChevronDownRegular
                                                        style={{
                                                            color: tokens.colorNeutralForeground2,
                                                            fontSize: '16px',
                                                        }}
                                                    />
                                                ) : (
                                                    <ChevronUpRegular
                                                        style={{
                                                            transform: 'rotate(90deg)',
                                                            color: tokens.colorNeutralForeground2,
                                                            fontSize: '16px',
                                                        }}
                                                    />
                                                )}
                                                <ErrorCircleRegular style={{ color: tokens.colorPaletteRedForeground1, fontSize: 20 }} />
                                                <Text size={300} weight="semibold">
                                                    Unhealthy app groups ({data.Overview.HealthAndPerformance.Unhealthy})
                                                </Text>
                                            </div>
                                        </Card>

                                        {openSections.unhealthyResources && data.Overview.HealthAndPerformance.Unhealthy > 0 && (
                                            <div
                                                style={{
                                                    padding: '8px 0 8px 16px',
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    alignItems: 'stretch',
                                                    width: '100%',
                                                }}
                                            >
                                                {data.AppGroupResourceSummary.map((subscription, subIndex) => {
                                                    const unhealthyApps = subscription.AppGroups?.filter(
                                                        app => app.AppHealthInfo.Health.toLowerCase() === 'unhealthy'
                                                    );

                                                    if (!unhealthyApps || unhealthyApps.length === 0) return null;

                                                    return (
                                                        <div key={`unhealthy-${subIndex}`} style={{ marginBottom: '16px' }}>
                                                            <Text weight="semibold" style={{ marginBottom: '8px' }}>
                                                                {subscription.SubscriptionName}
                                                            </Text>
                                                            {renderResourceCards(unhealthyApps)}
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        )}
                                    </div>

                                    {/* Degraded App Groups Subfolder */}
                                    <div style={{ marginBottom: '16px' }}>
                                        <Card
                                            onClick={() => {
                                                setOpenSections(prev => ({ ...prev, degradedResources: !prev.degradedResources }));
                                            }}
                                            style={{
                                                backgroundColor: tokens.colorNeutralBackground2,
                                                padding: '12px 16px 12px 36px',
                                                borderRadius: '4px',
                                                cursor: 'pointer',
                                                boxShadow: 'none',
                                                border: 'none',
                                            }}
                                        >
                                            <div
                                                style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '12px',
                                                }}
                                            >
                                                {openSections.degradedResources ? (
                                                    <ChevronDownRegular
                                                        style={{
                                                            color: tokens.colorNeutralForeground2,
                                                            fontSize: '16px',
                                                        }}
                                                    />
                                                ) : (
                                                    <ChevronUpRegular
                                                        style={{
                                                            transform: 'rotate(90deg)',
                                                            color: tokens.colorNeutralForeground2,
                                                            fontSize: '16px',
                                                        }}
                                                    />
                                                )}
                                                <ErrorCircleRegular
                                                    style={{ color: tokens.colorPaletteDarkOrangeForeground1, fontSize: 20 }}
                                                />
                                                <Text size={300} weight="semibold">
                                                    Degraded app groups ({data.Overview.HealthAndPerformance.Degraded})
                                                </Text>
                                            </div>
                                        </Card>

                                        {openSections.degradedResources && data.Overview.HealthAndPerformance.Degraded > 0 && (
                                            <div
                                                style={{
                                                    padding: '8px 0 8px 16px',
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    alignItems: 'stretch',
                                                    width: '100%',
                                                }}
                                            >
                                                {data.AppGroupResourceSummary.map((subscription, subIndex) => {
                                                    const degradedApps = subscription.AppGroups?.filter(
                                                        app => app.AppHealthInfo.Health.toLowerCase() === 'degraded'
                                                    );

                                                    if (!degradedApps || degradedApps.length === 0) return null;

                                                    return (
                                                        <div key={`degraded-${subIndex}`} style={{ marginBottom: '16px' }}>
                                                            <Text weight="semibold" style={{ marginBottom: '8px' }}>
                                                                {subscription.SubscriptionName}
                                                            </Text>
                                                            {renderResourceCards(degradedApps)}
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        )}
                                    </div>

                                    {/* Healthy App Groups Subfolder */}
                                    <div style={{ marginBottom: '16px' }}>
                                        <Card
                                            onClick={() => {
                                                setOpenSections(prev => ({ ...prev, healthyResources: !prev.healthyResources }));
                                            }}
                                            style={{
                                                backgroundColor: tokens.colorNeutralBackground2,
                                                padding: '12px 16px 12px 36px',
                                                borderRadius: '4px',
                                                cursor: 'pointer',
                                                boxShadow: 'none',
                                                border: 'none',
                                            }}
                                        >
                                            <div
                                                style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '12px',
                                                }}
                                            >
                                                {openSections.healthyResources ? (
                                                    <ChevronDownRegular
                                                        style={{
                                                            color: tokens.colorNeutralForeground2,
                                                            fontSize: '16px',
                                                        }}
                                                    />
                                                ) : (
                                                    <ChevronUpRegular
                                                        style={{
                                                            transform: 'rotate(90deg)',
                                                            color: tokens.colorNeutralForeground2,
                                                            fontSize: '16px',
                                                        }}
                                                    />
                                                )}
                                                <CheckmarkCircleRegular
                                                    style={{ color: tokens.colorPaletteGreenForeground1, fontSize: 20 }}
                                                />
                                                <Text size={300} weight="semibold">
                                                    Healthy app groups ({data.Overview.HealthAndPerformance.Healthy})
                                                </Text>
                                            </div>
                                        </Card>

                                        {openSections.healthyResources && data.Overview.HealthAndPerformance.Healthy > 0 && (
                                            <div
                                                style={{
                                                    padding: '8px 0 8px 16px',
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    alignItems: 'stretch',
                                                    width: '100%',
                                                }}
                                            >
                                                {data.AppGroupResourceSummary.map((subscription, subIndex) => {
                                                    const healthyApps = subscription.AppGroups?.filter(
                                                        app => app.AppHealthInfo.Health.toLowerCase() === 'healthy'
                                                    );

                                                    if (!healthyApps || healthyApps.length === 0) return null;

                                                    return (
                                                        <div key={`healthy-${subIndex}`} style={{ marginBottom: '16px' }}>
                                                            <Text weight="semibold" style={{ marginBottom: '8px' }}>
                                                                {subscription.SubscriptionName}
                                                            </Text>
                                                            {renderResourceCards(healthyApps)}
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        )}
                                    </div>
                                </>
                            )}
                        </div>
                    )}
                </div>

                {/* Actions Section */}
                <div style={{ marginBottom: '16px' }} data-section="actions">
                    <Card
                        onClick={() => toggleSection('actions')}
                        style={{
                            backgroundColor: tokens.colorNeutralBackground2,
                            padding: '12px 16px',
                            borderRadius: '4px',
                            cursor: 'pointer',
                            boxShadow: 'none',
                            border: 'none',
                        }}
                    >
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '12px',
                            }}
                        >
                            {openSections.actions ? (
                                <ChevronDownRegular
                                    style={{
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            ) : (
                                <ChevronUpRegular
                                    style={{
                                        transform: 'rotate(90deg)',
                                        color: tokens.colorNeutralForeground2,
                                        fontSize: '16px',
                                    }}
                                />
                            )}
                            <TaskListLtrRegular style={{ color: tokens.colorPalettePurpleForeground2 }} />
                            <Text size={400} weight="semibold">
                                Action summary
                            </Text>
                        </div>
                    </Card>

                    {openSections.actions && (
                        <div style={{ padding: '16px 0' }}>
                            {!data.RecommendedActionsAndObservations ||
                            (!data.RecommendedActionsAndObservations.Actions?.length &&
                                !data.RecommendedActionsAndObservations.Observations?.length) ? (
                                <Card style={{ textAlign: 'center', padding: '32px 16px' }}>
                                    <div style={{ color: tokens.colorPaletteGreenForeground2, fontSize: '24px', marginBottom: '16px' }}>
                                        <CheckmarkCircleRegular fontSize={32} />
                                    </div>
                                    <Text weight="semibold">
                                        <Text weight="semibold">0</Text> actions
                                    </Text>
                                </Card>
                            ) : (
                                <div>
                                    {data.RecommendedActionsAndObservations.Actions &&
                                        data.RecommendedActionsAndObservations.Actions.length > 0 && (
                                            <div style={{ marginBottom: '24px' }}>
                                                {data.RecommendedActionsAndObservations.Actions.map((action, idx) => (
                                                    <Card
                                                        key={idx}
                                                        style={{
                                                            marginBottom: '16px',
                                                            boxShadow: 'none',
                                                            border: `1px solid ${tokens.colorNeutralStroke1}`,
                                                            borderRadius: '8px',
                                                        }}
                                                    >
                                                        <div style={{ padding: '16px' }}>
                                                            <div
                                                                style={{
                                                                    display: 'flex',
                                                                    alignItems: 'flex-start',
                                                                    gap: '12px',
                                                                    marginBottom: '16px',
                                                                }}
                                                            >
                                                                <div
                                                                    style={{
                                                                        color:
                                                                            action.Priority.toLowerCase() === 'high'
                                                                                ? tokens.colorPaletteRedForeground1
                                                                                : tokens.colorPaletteDarkOrangeForeground1,
                                                                        marginTop: '3px',
                                                                    }}
                                                                >
                                                                    <ErrorCircleRegular />
                                                                </div>
                                                                <Text style={{ flex: 1 }}>{action.Description}</Text>
                                                            </div>
                                                            <div
                                                                style={{
                                                                    display: 'flex',
                                                                    borderTop: `1px solid ${tokens.colorNeutralStroke1}`,
                                                                    paddingTop: '12px',
                                                                    color: tokens.colorNeutralForeground2,
                                                                }}
                                                            >
                                                                <div style={{ marginRight: '24px' }}>
                                                                    <Text
                                                                        size={200}
                                                                        weight="semibold"
                                                                        style={{ color: tokens.colorNeutralForeground2 }}
                                                                    >
                                                                        Priority:{' '}
                                                                    </Text>
                                                                    <Text size={200}>{action.Priority}</Text>
                                                                </div>
                                                                <div>
                                                                    <Text
                                                                        size={200}
                                                                        weight="semibold"
                                                                        style={{ color: tokens.colorNeutralForeground2 }}
                                                                    >
                                                                        Urgency:{' '}
                                                                    </Text>
                                                                    <Text size={200}>{action.ETA}</Text>
                                                                </div>
                                                            </div>
                                                        </div>
                                                    </Card>
                                                ))}
                                            </div>
                                        )}
                                </div>
                            )}
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
};

export default DailyReport;
