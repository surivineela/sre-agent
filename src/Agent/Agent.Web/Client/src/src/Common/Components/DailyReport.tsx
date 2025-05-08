import React, { useContext, useState } from 'react';
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, TooltipProps, XAxis, YAxis } from 'recharts';
import { NameType, ValueType } from 'recharts/types/component/DefaultTooltipContent';
import { EnvironmentContext } from '../AzPortalProxy/Providers/StartupInfoContext';
import { ArmResourceDescriptor } from '../Helpers/ResourceDescriptors';
import './sre-dashboard.css'; // Import the CSS file we created

interface AppHealthInfo {
    LastDataCaptureTimeStampInUTC: string;
    Health: string;
    Availability: number;
    Transactions: number;
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

type SectionKey = 'overview' | 'resources' | 'incidents' | 'actions' | 'security';

const ActionsIcon = () => (
    <svg
        xmlns="http://www.w3.org/2000/svg"
        className="accordion-icon actions"
        width="20"
        height="20"
        viewBox="0 0 20 20"
        fill="currentColor"
    >
        <path
            fillRule="evenodd"
            d="M11.49 3.17c-.38-1.56-2.6-1.56-2.98 0a1.532 1.532 0 01-2.286.948c-1.372-.836-2.942.734-2.106 2.106.54.886.061 2.042-.947 2.287-1.561.379-1.561 2.6 0 2.978a1.532 1.532 0 01.947 2.287c-.836 1.372.734 2.942 2.106 2.106a1.532 1.532 0 012.287.947c.379 1.561 2.6 1.561 2.978 0a1.533 1.533 0 012.287-.947c1.372.836 2.942-.734 2.106-2.106a1.533 1.533 0 01.947-2.287c1.561-.379 1.561-2.6 0-2.978a1.532 1.532 0 01-.947-2.287c.836-1.372-.734-2.942-2.106-2.106a1.532 1.532 0 01-2.287-.947zM10 13a3 3 0 100-6 3 3 0 000 6z"
            clipRule="evenodd"
        />
    </svg>
);

const ChevronIcon = ({ isOpen }: { isOpen: boolean }) => (
    <svg className={`chevron-icon ${isOpen ? 'open' : ''}`} xmlns="http://www.w3.org/2000/svg" viewBox="0 0 20 20" fill="currentColor">
        <path
            fillRule="evenodd"
            d="M5.293 7.293a1 1 0 011.414 0L10 10.586l3.293-3.293a1 1 0 111.414 1.414l-4 4a1 1 0 01-1.414 0l-4-4a1 1 0 010-1.414z"
            clipRule="evenodd"
        />
    </svg>
);

const CheckIcon = () => (
    <svg className="check-icon" xmlns="http://www.w3.org/2000/svg" fill="none" viewBox="0 0 24 24" stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M9 12l2 2 4-4m6 2a9 9 0 11-18 0 9 9 0 0118 0z" />
    </svg>
);

const SecurityIcon = ({ color = '#F59E0B' }) => (
    <svg
        xmlns="http://www.w3.org/2000/svg"
        className="accordion-icon security-icon"
        width="20"
        height="20"
        viewBox="0 0 20 20"
        fill={color}
    >
        <path
            fillRule="evenodd"
            d="M10 1.944A11.954 11.954 0 012.166 5C2.056 5.649 2 6.319 2 7c0 5.225 3.34 9.67 8 11.317C14.66 16.67 18 12.225 18 7c0-.682-.057-1.35-.166-2.001A11.954 11.954 0 0110 1.944zM11 14a1 1 0 11-2 0 1 1 0 012 0zm0-7a1 1 0 10-2 0v3a1 1 0 102 0V7z"
            clipRule="evenodd"
        />
    </svg>
);

const IncidentsIcon = ({ color = '#EF4444' }) => (
    <svg
        xmlns="http://www.w3.org/2000/svg"
        className="accordion-icon incidents-icon"
        width="20"
        height="20"
        viewBox="0 0 24 24"
        fill={color}
    >
        <path
            fillRule="evenodd"
            d="M9.401 3.003c1.155-2 4.043-2 5.197 0l7.355 12.748c1.154 2-.29 4.5-2.599 4.5H4.645c-2.309 0-3.752-2.5-2.598-4.5L9.4 3.003zM12 8.25a.75.75 0 01.75.75v3.75a.75.75 0 01-1.5 0V9a.75.75 0 01.75-.75zm0 8.25a.75.75 0 100-1.5.75.75 0 000 1.5z"
            clipRule="evenodd"
        />
    </svg>
);

const HealthPerformanceIcon = ({ color = '#3B82F6' }) => (
    <svg
        xmlns="http://www.w3.org/2000/svg"
        className="accordion-icon health-performance"
        width="20"
        height="20"
        viewBox="0 0 24 24"
        fill={color}
    >
        <path
            fillRule="evenodd"
            d="M2.25 13.5a8.25 8.25 0 018.25-8.25.75.75 0 01.75.75v6.75H18a.75.75 0 01.75.75 8.25 8.25 0 01-16.5 0z"
            clipRule="evenodd"
        />
        <path
            fillRule="evenodd"
            d="M12.75 3a.75.75 0 01.75-.75 8.25 8.25 0 018.25 8.25.75.75 0 01-.75.75h-7.5a.75.75 0 01-.75-.75V3z"
            clipRule="evenodd"
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
    });

    // Toggle accordion sections
    const toggleSection = (section: SectionKey) => {
        setOpenSections({
            ...openSections,
            [section]: !openSections[section],
        });
    };

    // Get status badge color
    const getStatusColor = (status: string) => {
        switch (status.toLowerCase()) {
            case 'healthy':
                return '#10B981'; // green
            case 'warning':
                return '#F59E0B'; // amber
            case 'degraded':
                return '#F97316'; // orange
            case 'unhealthy':
                return '#EF4444'; // red
            case 'critical':
                return '#EF4444'; // red
            default:
                return '#6B7280'; // gray
        }
    };

    // Get priority class
    const getPriorityClass = (priority: string) => {
        switch (priority.toLowerCase()) {
            case 'high':
                return 'priority-high';
            case 'medium':
                return 'priority-medium';
            case 'low':
                return 'priority-low';
            default:
                return '';
        }
    };

    // Function to render resource cards
    const renderResourceCards = (resources: AppGroupResourceInfo[], showHistoricalData: boolean) => {
        return resources.map((resource, resIndex) => (
            <div
                key={resIndex}
                className={`resource-card ${resource.AppHealthInfo.Health.toLowerCase() === 'unhealthy' && showHistoricalData ? 'full-width-card' : ''}`}
                style={{ overflow: 'visible' }}
            >
                {resource.AppHealthInfo.Health.toLowerCase() === 'unhealthy' &&
                showHistoricalData &&
                resource.AppHealthInfo.HistoricalData &&
                resource.AppHealthInfo.HistoricalData.length > 0 ? (
                    // Special layout for unhealthy resources with historical data
                    <>
                        <div className="resource-header">
                            <div className="resource-name">
                                <span
                                    className="status-indicator"
                                    style={{ backgroundColor: getStatusColor(resource.AppHealthInfo.Health) }}
                                ></span>
                                <div style={{ display: 'flex', flexDirection: 'column' }}>
                                    <span className="resource-title">{resource.Name}</span>
                                    <span style={{ color: '#6b7280', fontSize: '0.75rem' }}>{resource.Type}</span>
                                </div>
                            </div>
                            <span
                                className="resource-status"
                                style={{
                                    backgroundColor: `${getStatusColor(resource.AppHealthInfo.Health)}40`,
                                    color: getStatusColor(resource.AppHealthInfo.Health),
                                    whiteSpace: 'nowrap',
                                    minWidth: '60px',
                                    textAlign: 'center',
                                }}
                            >
                                {resource.AppHealthInfo.Health}
                            </span>
                        </div>

                        <div style={{ display: 'flex', flexDirection: 'column', width: '100%' }}>
                            <div style={{ display: 'flex', padding: '15px 15px' }}>
                                <div style={{ width: '30%', paddingRight: '20px' }}>
                                    <div
                                        className="resource-details"
                                        style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', rowGap: '10px', padding: '5px 0' }}
                                    >
                                        <div className="detail-label">Availability:</div>
                                        <div className={`detail-value ${resource.AppHealthInfo.Availability < 99.5 ? 'warning' : ''}`}>
                                            {resource.AppHealthInfo.Availability}%
                                        </div>
                                        <div className="detail-label">CPU Usage:</div>
                                        <div className={`detail-value ${resource.AppHealthInfo.AvgCpuUsage > 80 ? 'warning' : ''}`}>
                                            {resource.AppHealthInfo.AvgCpuUsage}%
                                        </div>
                                        <div className="detail-label">Memory:</div>
                                        <div className="detail-value">{formatBytes(resource.AppHealthInfo.AvgMemoryUsage)}</div>
                                        <div className="detail-label">Transactions:</div>
                                        <div className="detail-value">{resource.AppHealthInfo.Transactions.toLocaleString()}</div>
                                    </div>
                                </div>
                                <div style={{ width: '70%', height: '250px', padding: '5px 0' }}>
                                    {renderHistoricalDataChart(resource.AppHealthInfo.HistoricalData)}
                                </div>
                            </div>
                        </div>
                    </>
                ) : (
                    // Regular layout for other resources
                    <>
                        <div className="resource-header">
                            <div className="resource-name">
                                <span
                                    className="status-indicator"
                                    style={{ backgroundColor: getStatusColor(resource.AppHealthInfo.Health) }}
                                ></span>
                                <div style={{ display: 'flex', flexDirection: 'column' }}>
                                    <span className="resource-title">{resource.Name}</span>
                                    <span style={{ color: '#6b7280', fontSize: '0.75rem' }}>{resource.Type}</span>
                                </div>
                            </div>
                            <span
                                className="resource-status"
                                style={{
                                    backgroundColor: `${getStatusColor(resource.AppHealthInfo.Health)}40`,
                                    color: getStatusColor(resource.AppHealthInfo.Health),
                                    whiteSpace: 'nowrap',
                                    minWidth: '60px',
                                    textAlign: 'center',
                                }}
                            >
                                {resource.AppHealthInfo.Health}
                            </span>
                        </div>
                        <div className="resource-body">
                            <div className="resource-details">
                                <div className="detail-label">Availability:</div>
                                <div className={`detail-value ${resource.AppHealthInfo.Availability < 99.5 ? 'warning' : ''}`}>
                                    {resource.AppHealthInfo.Availability}%
                                </div>
                                <div className="detail-label">CPU Usage:</div>
                                <div className={`detail-value ${resource.AppHealthInfo.AvgCpuUsage > 80 ? 'warning' : ''}`}>
                                    {resource.AppHealthInfo.AvgCpuUsage}%
                                </div>
                                <div className="detail-label">Memory:</div>
                                <div className="detail-value">{formatBytes(resource.AppHealthInfo.AvgMemoryUsage)}</div>
                                <div className="detail-label">Transactions:</div>
                                <div className="detail-value">{resource.AppHealthInfo.Transactions.toLocaleString()}</div>
                            </div>
                        </div>
                    </>
                )}
            </div>
        ));
    };

    // Function to render a chart using recharts
    const renderHistoricalDataChart = (historicalData: HistoricalDataPoint[]) => {
        if (historicalData.length < 2) return <div>Insufficient historical data</div>;

        // Sort data points by timestamp
        const sortedData = [...historicalData].sort((a, b) => new Date(a.Timestamp).getTime() - new Date(b.Timestamp).getTime());

        // Convert the data into the format expected by recharts
        const chartData = sortedData.map(point => ({
            time: new Date(point.Timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
            timestamp: new Date(point.Timestamp).getTime(),
            availability: point.Availability,
            cpu: point.CpuUsage,
            memory: point.MemoryUsage,
        }));

        // Generate even 30-minute interval ticks
        const timestamps = chartData.map(d => d.timestamp);
        const startTime = Math.min(...timestamps);
        const endTime = Math.max(...timestamps);

        // Round to nearest 30 min intervals
        const startDate = new Date(startTime);
        startDate.setMinutes(Math.floor(startDate.getMinutes() / 30) * 30);
        startDate.setSeconds(0);
        startDate.setMilliseconds(0);

        const endDate = new Date(endTime);
        endDate.setMinutes(Math.ceil(endDate.getMinutes() / 30) * 30);
        endDate.setSeconds(0);
        endDate.setMilliseconds(0);

        // Generate ticks at 30 min intervals
        const ticks = [];
        const currentDate = new Date(startDate);
        while (currentDate <= endDate) {
            ticks.push(currentDate.getTime());
            currentDate.setMinutes(currentDate.getMinutes() + 30);
        }

        // Custom tick formatter
        const formatXAxis = (timestamp: number) => {
            return new Date(timestamp).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        };

        // Custom tooltip component
        const CustomTooltip = ({ active, payload }: TooltipProps<ValueType, NameType>) => {
            if (active && payload && payload.length) {
                const dataPoint = payload[0].payload;
                return (
                    <div className="custom-tooltip">
                        <p className="tooltip-time">{new Date(dataPoint.timestamp).toLocaleString()}</p>
                        <p className="tooltip-metric">
                            <span className="color-dot" style={{ backgroundColor: '#3B82F6' }}></span>
                            CPU: {dataPoint.cpu}%
                        </p>
                        <p className="tooltip-metric">
                            <span className="color-dot" style={{ backgroundColor: '#EC4899' }}></span>
                            Memory: {dataPoint.memory} B
                        </p>
                        <p className="tooltip-metric">
                            <span className="color-dot" style={{ backgroundColor: '#10B981' }}></span>
                            Availability: {dataPoint.availability}%
                        </p>
                    </div>
                );
            }
            return null;
        };

        return (
            <div style={{ width: '100%', height: 300 }}>
                <ResponsiveContainer width="100%" height="100%">
                    <LineChart data={chartData} margin={{ top: 5, right: 30, left: 20, bottom: 25 }}>
                        <CartesianGrid strokeDasharray="3 3" />
                        <XAxis
                            dataKey="timestamp"
                            type="number"
                            domain={[startDate.getTime(), endDate.getTime()]}
                            ticks={ticks}
                            tickFormatter={formatXAxis}
                            padding={{ left: 10, right: 10 }}
                            label={{ value: 'Time', position: 'insideBottomRight', offset: -10 }}
                        />
                        <YAxis label={{ value: 'Value', angle: -90, position: 'insideLeft' }} domain={[0, 100]} />
                        <Tooltip content={<CustomTooltip />} />
                        <Legend
                            verticalAlign="bottom"
                            height={36}
                            iconSize={10}
                            wrapperStyle={{ fontSize: '12px', paddingBottom: '5px' }}
                            align="center"
                        />
                        <Line type="monotone" dataKey="cpu" name="CPU Usage" stroke="#3B82F6" activeDot={{ r: 8 }} strokeWidth={2} />
                        <Line type="monotone" dataKey="memory" name="Memory" stroke="#EC4899" activeDot={{ r: 8 }} strokeWidth={2} />
                        <Line
                            type="monotone"
                            dataKey="availability"
                            name="Availability"
                            stroke="#10B981"
                            activeDot={{ r: 8 }}
                            strokeWidth={2}
                        />
                    </LineChart>
                </ResponsiveContainer>
            </div>
        );
    };

    return (
        <div className="dashboard-container">
            {/* Header */}
            <div className="header">
                <div className="header-container">
                    <div className="report-header">
                        <h1 className="report-title">{'Daily Report'}</h1>
                        <div className="report-subtitle">
                            {timestamp
                                ? new Date(timestamp).toLocaleDateString('en-US', {
                                      weekday: 'long',
                                      month: 'long',
                                      day: 'numeric',
                                      year: 'numeric',
                                  })
                                : new Date().toLocaleDateString('en-US', {
                                      weekday: 'long',
                                      month: 'long',
                                      day: 'numeric',
                                      year: 'numeric',
                                  })}
                            <span className="subtitle-dot">•</span>
                            Daily Report
                        </div>

                        <div className="badge-container">
                            <span
                                className="status-badge"
                                style={{
                                    backgroundColor:
                                        data.Overview.SecurityFindings.Critical > 0
                                            ? 'rgba(239, 68, 68, 0.8)'
                                            : (data.Overview.SecurityFindings.High || data.Overview.SecurityFindings.Moderate) > 0
                                              ? 'rgba(245, 158, 11, 0.8)'
                                              : 'rgba(16, 185, 129, 0.8)',
                                }}
                            >
                                Security: {data.Overview.SecurityFindings.TotalCount}
                            </span>
                            <span
                                className="status-badge"
                                style={{
                                    backgroundColor:
                                        data.Overview.Incidents.Active > 0
                                            ? 'rgba(239, 68, 68, 0.8)'
                                            : data.Overview.Incidents.Mitigated > 0
                                              ? 'rgba(245, 158, 11, 0.8)'
                                              : 'rgba(16, 185, 129, 0.8)',
                                }}
                            >
                                Incidents:{' '}
                                {(data.IncidentsSummary.PagerDuty?.length || 0) + (data.IncidentsSummary.AzureMonitor?.length || 0)}
                            </span>
                            <span
                                className="status-badge"
                                style={{
                                    backgroundColor:
                                        data.Overview.HealthAndPerformance.Unhealthy > 0
                                            ? 'rgba(239, 68, 68, 0.8)'
                                            : data.Overview.HealthAndPerformance.Degraded > 0
                                              ? 'rgba(249, 115, 22, 0.8)'
                                              : 'rgba(16, 185, 129, 0.8)',
                                }}
                            >
                                Resources: {data.AppGroupResourceSummary.reduce((total, sub) => total + (sub.AppGroups?.length || 0), 0)}
                            </span>
                            <span className="status-badge" style={{ backgroundColor: 'rgba(139, 92, 246, 0.8)' }}>
                                Actions: {data.RecommendedActionsAndObservations?.Actions?.length || 0}
                            </span>
                        </div>
                    </div>
                </div>
            </div>

            {/* Main content */}
            <div className="main-content">
                {/* Dashboard Grid - with inline style to force single row */}
                <div
                    className="dashboard-grid"
                    style={{
                        display: 'grid',
                        gridTemplateColumns: 'repeat(3, 1fr)',
                        gap: '16px',
                        marginBottom: '24px',
                    }}
                >
                    {/* Security Findings Card */}
                    <div
                        className="card overview-card"
                        onClick={() => {
                            setOpenSections({ ...openSections, security: true });
                            setTimeout(() => document.querySelector('.security-section')?.scrollIntoView({ behavior: 'smooth' }), 100);
                        }}
                        style={{ cursor: 'pointer', borderRadius: '12px', overflow: 'hidden' }}
                    >
                        <div
                            className="overview-header"
                            style={{
                                backgroundColor:
                                    data.Overview.SecurityFindings.Critical > 0
                                        ? 'rgba(239, 68, 68, 0.05)'
                                        : (data.Overview.SecurityFindings.High || data.Overview.SecurityFindings.Moderate) > 0
                                          ? 'rgba(245, 158, 11, 0.05)'
                                          : 'rgba(16, 185, 129, 0.05)',
                                margin: '-20px -20px 0',
                                padding: '20px',
                                borderTopLeftRadius: '12px',
                                borderTopRightRadius: '12px',
                            }}
                        >
                            <div className="overview-icon security-icon">
                                <SecurityIcon
                                    color={
                                        data.Overview.SecurityFindings.Critical > 0
                                            ? '#EF4444'
                                            : (data.Overview.SecurityFindings.High || data.Overview.SecurityFindings.Moderate) > 0
                                              ? '#F59E0B'
                                              : '#10B981'
                                    }
                                />
                            </div>
                            <div className="overview-title">Security Findings</div>
                            <div className="overview-score">
                                <span>Total</span>
                                <div className="score-value-box" style={{ backgroundColor: 'white' }}>
                                    {data.Overview.SecurityFindings.TotalCount}
                                </div>
                            </div>
                        </div>
                        <div className="overview-body">
                            <div className="status-details">
                                <div className="status-item">
                                    <div className="status-label critical">Critical</div>
                                    <div className="status-value">{data.Overview.SecurityFindings.Critical}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label warning">High</div>
                                    <div className="status-value">{data.Overview.SecurityFindings.High}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label warning">Moderate</div>
                                    <div className="status-value">{data.Overview.SecurityFindings.Moderate}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label success">Low</div>
                                    <div className="status-value">{data.Overview.SecurityFindings.Low}</div>
                                </div>
                            </div>

                            {/* Impact bar visualization */}
                            <div className="impact-visualization">
                                <div className="impact-bar">
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.SecurityFindings.Critical || 0.1, backgroundColor: '#EF4444' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.SecurityFindings.High || 0.1, backgroundColor: '#F59E0B' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.SecurityFindings.Moderate || 0.1, backgroundColor: '#F97316' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.SecurityFindings.Low || 0.1, backgroundColor: '#10B981' }}
                                    ></div>
                                </div>
                                <div className="impact-labels">
                                    <div className="impact-label">Critical</div>
                                    <div className="impact-label">High</div>
                                    <div className="impact-label">Moderate</div>
                                    <div className="impact-label">Low</div>
                                </div>
                            </div>

                            {data.Overview.SecurityFindings.TotalCount === 0 ? (
                                <div className="status-message" style={{ display: 'flex', alignItems: 'center', flexWrap: 'nowrap' }}>
                                    <span className="good-status-icon">✓ </span>
                                    <span className="status-message-text" style={{ marginLeft: '4px' }}>
                                        No security issues found
                                    </span>
                                </div>
                            ) : (
                                <div
                                    className="status-message warning"
                                    style={{ display: 'flex', alignItems: 'center', flexWrap: 'nowrap' }}
                                >
                                    <span className="warning-status-icon">⚠</span>
                                    <span className="status-message-text" style={{ marginLeft: '4px' }}>
                                        Security issues require attention
                                    </span>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Incidents Card */}
                    <div
                        className="card overview-card"
                        onClick={() => {
                            setOpenSections({ ...openSections, incidents: true });
                            setTimeout(
                                () => document.querySelector('.accordion-section:nth-child(2)')?.scrollIntoView({ behavior: 'smooth' }),
                                100
                            );
                        }}
                        style={{ cursor: 'pointer', borderRadius: '12px', overflow: 'hidden' }}
                    >
                        <div
                            className="overview-header"
                            style={{
                                backgroundColor:
                                    data.Overview.Incidents.Active > 0
                                        ? 'rgba(239, 68, 68, 0.05)'
                                        : data.Overview.Incidents.Mitigated > 0
                                          ? 'rgba(245, 158, 11, 0.05)'
                                          : 'rgba(16, 185, 129, 0.05)',
                                margin: '-20px -20px 0',
                                padding: '20px',
                                borderTopLeftRadius: '12px',
                                borderTopRightRadius: '12px',
                            }}
                        >
                            <div className="overview-icon incidents-icon">
                                <IncidentsIcon
                                    color={
                                        data.Overview.Incidents.Active > 0
                                            ? '#EF4444'
                                            : data.Overview.Incidents.Mitigated > 0
                                              ? '#F59E0B'
                                              : '#10B981'
                                    }
                                />
                            </div>
                            <div className="overview-title">Incidents</div>
                            <div className="overview-score">
                                <span>Total</span>
                                <div className="score-value-box" style={{ backgroundColor: 'white' }}>
                                    {data.Overview.Incidents.TotalCount}
                                </div>
                            </div>
                        </div>
                        <div className="overview-body">
                            <div className="status-details">
                                <div className="status-item">
                                    <div className="status-label critical">Active</div>
                                    <div className="status-value">{data.Overview.Incidents.Active}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label warning">Mitigated</div>
                                    <div className="status-value">{data.Overview.Incidents.Mitigated}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label success">Resolved</div>
                                    <div className="status-value">{data.Overview.Incidents.Resolved}</div>
                                </div>
                            </div>

                            {/* Impact bar visualization */}
                            <div className="impact-visualization">
                                <div className="impact-bar">
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.Incidents.Active || 0.1, backgroundColor: '#EF4444' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.Incidents.Mitigated || 0.1, backgroundColor: '#F59E0B' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.Incidents.Resolved || 0.1, backgroundColor: '#10B981' }}
                                    ></div>
                                </div>
                                <div className="impact-labels">
                                    <div className="impact-label">Active</div>
                                    <div className="impact-label">Mitigated</div>
                                    <div className="impact-label">Resolved</div>
                                </div>
                            </div>

                            {data.Overview.Incidents.Active === 0 ? (
                                <div className="status-message" style={{ display: 'flex', alignItems: 'center', flexWrap: 'nowrap' }}>
                                    <span className="good-status-icon">✓ </span>
                                    <span className="status-message-text" style={{ marginLeft: '4px' }}>
                                        {' '}
                                        No active incidents
                                    </span>
                                </div>
                            ) : (
                                <div
                                    className="status-message warning"
                                    style={{ display: 'flex', alignItems: 'center', flexWrap: 'nowrap' }}
                                >
                                    <span className="warning-status-icon">⚠ </span>
                                    <span className="status-message-text" style={{ marginLeft: '4px' }}>
                                        <strong>{data.Overview.Incidents.Active}</strong> active incident(s) require attention
                                    </span>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Health & Performance Card */}
                    <div
                        className="card overview-card"
                        onClick={() => {
                            setOpenSections({ ...openSections, resources: true });
                            setTimeout(
                                () => document.querySelector('.accordion-section:nth-child(3)')?.scrollIntoView({ behavior: 'smooth' }),
                                100
                            );
                        }}
                        style={{ cursor: 'pointer', borderRadius: '12px', overflow: 'hidden' }}
                    >
                        <div
                            className="overview-header"
                            style={{
                                backgroundColor:
                                    data.Overview.HealthAndPerformance.Unhealthy > 0
                                        ? 'rgba(239, 68, 68, 0.05)'
                                        : data.Overview.HealthAndPerformance.Degraded > 0
                                          ? 'rgba(245, 158, 11, 0.05)'
                                          : 'rgba(16, 185, 129, 0.05)',
                                margin: '-20px -20px 0',
                                padding: '20px',
                                borderTopLeftRadius: '12px',
                                borderTopRightRadius: '12px',
                            }}
                        >
                            <div className="overview-icon health-icon">
                                <HealthPerformanceIcon
                                    color={
                                        data.Overview.HealthAndPerformance.Unhealthy > 0
                                            ? '#EF4444'
                                            : data.Overview.HealthAndPerformance.Degraded > 0
                                              ? '#F97316'
                                              : '#10B981'
                                    }
                                />
                            </div>
                            <div className="overview-title">Health & Performance</div>
                            <div className="overview-score">
                                <span>Total</span>
                                <div className="score-value-box" style={{ backgroundColor: 'white' }}>
                                    {data.Overview.HealthAndPerformance.TotalCount}
                                </div>
                            </div>
                        </div>
                        <div className="overview-body">
                            <div className="status-details">
                                <div className="status-item">
                                    <div className="status-label success">Healthy</div>
                                    <div className="status-value">{data.Overview.HealthAndPerformance.Healthy}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label warning">Degraded</div>
                                    <div className="status-value">{data.Overview.HealthAndPerformance.Degraded}</div>
                                </div>
                                <div className="status-item">
                                    <div className="status-label critical">Unhealthy</div>
                                    <div className="status-value">{data.Overview.HealthAndPerformance.Unhealthy}</div>
                                </div>
                            </div>

                            {/* Impact bar visualization */}
                            <div className="impact-visualization">
                                <div className="impact-bar">
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.HealthAndPerformance.Healthy || 0.1, backgroundColor: '#10B981' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.HealthAndPerformance.Degraded || 0.1, backgroundColor: '#F59E0B' }}
                                    ></div>
                                    <div
                                        className="impact-segment"
                                        style={{ flex: data.Overview.HealthAndPerformance.Unhealthy || 0.1, backgroundColor: '#EF4444' }}
                                    ></div>
                                </div>
                                <div className="impact-labels">
                                    <div className="impact-label">Unhealthy</div>
                                    <div className="impact-label">Degraded</div>
                                    <div className="impact-label">Healthy</div>
                                </div>
                            </div>

                            {data.Overview.HealthAndPerformance.Unhealthy === 0 && data.Overview.HealthAndPerformance.Degraded === 0 ? (
                                <div className="status-message" style={{ display: 'flex', alignItems: 'center', flexWrap: 'nowrap' }}>
                                    <span className="good-status-icon">✓ </span>
                                    <span className="status-message-text" style={{ marginLeft: '4px' }}>
                                        {' '}
                                        All logical app are healthy
                                    </span>
                                </div>
                            ) : (
                                <div
                                    className="status-message warning"
                                    style={{ display: 'flex', alignItems: 'center', flexWrap: 'nowrap' }}
                                >
                                    <span className="warning-status-icon">⚠ </span>
                                    <span className="status-message-text" style={{ marginLeft: '4px' }}>
                                        <strong>
                                            {data.Overview.HealthAndPerformance.Unhealthy + data.Overview.HealthAndPerformance.Degraded}
                                        </strong>{' '}
                                        Logical apps require attention
                                    </span>
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                {/* Security Findings Section */}
                <div className="accordion-section security-section">
                    <div
                        className="accordion-header"
                        onClick={() => toggleSection('security')}
                        style={{
                            backgroundColor:
                                data.Overview.SecurityFindings.Critical > 0
                                    ? 'rgba(239, 68, 68, 0.05)'
                                    : (data.Overview.SecurityFindings.High || data.Overview.SecurityFindings.Moderate) > 0
                                      ? 'rgba(245, 158, 11, 0.05)'
                                      : 'rgba(16, 185, 129, 0.05)',
                        }}
                    >
                        <h2 className="accordion-title">
                            <SecurityIcon
                                color={
                                    data.Overview.SecurityFindings.Critical > 0
                                        ? '#EF4444'
                                        : (data.Overview.SecurityFindings.High || data.Overview.SecurityFindings.Moderate) > 0
                                          ? '#F59E0B'
                                          : '#10B981'
                                }
                            />
                            Security Findings
                            {data.CVESummary?.TotalVulnerabilities != null && data.CVESummary?.TotalVulnerabilities > 0 && (
                                <span className="incident-count">{data.CVESummary?.TotalVulnerabilities}</span>
                            )}
                        </h2>
                        <ChevronIcon isOpen={openSections.security} />
                    </div>

                    {openSections.security && (
                        <div className="accordion-content">
                            {!data.CVESummary || data.CVESummary.TotalVulnerabilities === 0 ? (
                                <div className="no-incidents">
                                    <CheckIcon />
                                    <p className="no-incidents-text"> No security vulnerabilities found in this period</p>
                                </div>
                            ) : (
                                <div>
                                    <h3>Vulnerabilities by Severity</h3>
                                    <div className="status-details" style={{ marginBottom: '20px' }}>
                                        <div className="status-item">
                                            <div className="status-label critical">Critical</div>
                                            <div className="status-value">{data.CVESummary.CriticalVulnerabilities}</div>
                                        </div>
                                        <div className="status-item">
                                            <div className="status-label warning">High</div>
                                            <div className="status-value">{data.CVESummary.HighVulnerabilities}</div>
                                        </div>
                                        <div className="status-item">
                                            <div className="status-label warning">Moderate</div>
                                            <div className="status-value">{data.CVESummary.ModerateVulnerabilities}</div>
                                        </div>
                                        <div className="status-item">
                                            <div className="status-label success">Low</div>
                                            <div className="status-value">{data.CVESummary.LowVulnerabilities}</div>
                                        </div>
                                    </div>
                                </div>
                            )}
                        </div>
                    )}
                </div>

                {/* Incidents Section */}
                <div className="accordion-section">
                    <div
                        className="accordion-header"
                        onClick={() => toggleSection('incidents')}
                        style={{
                            backgroundColor:
                                data.Overview.Incidents.Active > 0
                                    ? 'rgba(239, 68, 68, 0.05)'
                                    : data.Overview.Incidents.Mitigated > 0
                                      ? 'rgba(245, 158, 11, 0.05)'
                                      : 'rgba(16, 185, 129, 0.05)',
                        }}
                    >
                        <h2 className="accordion-title">
                            <IncidentsIcon
                                color={
                                    data.Overview.Incidents.Active > 0
                                        ? '#EF4444'
                                        : data.Overview.Incidents.Mitigated > 0
                                          ? '#F59E0B'
                                          : '#10B981'
                                }
                            />
                            Incidents
                            {(data.IncidentsSummary.PagerDuty?.length || 0) + (data.IncidentsSummary.AzureMonitor?.length || 0) > 0 && (
                                <span className="incident-count" style={{ backgroundColor: 'white' }}>
                                    {(data.IncidentsSummary.PagerDuty?.length || 0) + (data.IncidentsSummary.AzureMonitor?.length || 0)}
                                </span>
                            )}
                        </h2>
                        <ChevronIcon isOpen={openSections.incidents} />
                    </div>

                    {openSections.incidents && (
                        <div className="accordion-content">
                            {(data.IncidentsSummary.PagerDuty?.length || 0) === 0 &&
                            (data.IncidentsSummary.AzureMonitor?.length || 0) === 0 ? (
                                <div className="no-incidents">
                                    <CheckIcon />
                                    <p className="no-incidents-text">No incidents reported for this period</p>
                                </div>
                            ) : (
                                <>
                                    {data.IncidentsSummary.PagerDuty?.map((incident, index) => (
                                        <div key={index} className="incident-card">
                                            <div className="incident-header">
                                                <h3
                                                    className="incident-title"
                                                    style={{
                                                        flex: '1',
                                                        marginRight: '8px',
                                                        whiteSpace: 'nowrap',
                                                        overflow: 'hidden',
                                                        textOverflow: 'ellipsis',
                                                    }}
                                                >
                                                    {incident.Name}
                                                </h3>
                                                <span
                                                    className={`incident-status ${incident.Status.toLowerCase() === 'resolved' ? 'resolved' : 'active'}`}
                                                    style={{
                                                        whiteSpace: 'nowrap',
                                                        minWidth: 'auto',
                                                        textOverflow: 'ellipsis',
                                                        overflow: 'hidden',
                                                    }}
                                                >
                                                    {incident.Status.charAt(0).toUpperCase() + incident.Status.slice(1)}
                                                </span>
                                            </div>
                                            <div className="incident-body">
                                                <div className="incident-details">
                                                    <div className="detail-group">
                                                        <label>Incident ID</label>
                                                        <div>{incident.IncidentId}</div>
                                                    </div>
                                                    {incident.CreateTime && (
                                                        <div className="detail-group">
                                                            <label>Created</label>
                                                            <div>{new Date(incident.CreateTime).toLocaleString()}</div>
                                                        </div>
                                                    )}
                                                    {incident.Duration && (
                                                        <div className="detail-group">
                                                            <label>Duration</label>
                                                            <div>{incident.Duration}</div>
                                                        </div>
                                                    )}
                                                </div>

                                                {incident.Impact && (
                                                    <div className="impact-box">
                                                        <div className="impact-title">Impact</div>
                                                        <div className="impact-text">
                                                            {incident.Impact.split('\n')
                                                                .map((item, i) => {
                                                                    // Extract bullet point content (handles both - and * bullets)
                                                                    const bulletMatch = item.match(/^[-*]\s+(.*)$/);
                                                                    if (bulletMatch) {
                                                                        return <li key={i}>{bulletMatch[1]}</li>;
                                                                    } else if (item.trim()) {
                                                                        // For non-bullet items that aren't empty
                                                                        return <p key={i}>{item}</p>;
                                                                    }
                                                                    return null;
                                                                })
                                                                .filter(Boolean)}
                                                        </div>
                                                    </div>
                                                )}

                                                {incident.Resolution && (
                                                    <div className="resolution-section">
                                                        <div className="section-title">Resolution</div>
                                                        <div className="section-text">{incident.Resolution}</div>
                                                    </div>
                                                )}

                                                {incident.InvestigationDetails && (
                                                    <div className="investigation-section">
                                                        <div className="section-title">Investigation Details</div>
                                                        <div className="section-text">
                                                            {incident.InvestigationDetails.split('\n')
                                                                .map((item, i) => {
                                                                    // Extract bullet point content (handles both - and * bullets)
                                                                    const bulletMatch = item.match(/^[-*]\s+(.*)$/);
                                                                    if (bulletMatch) {
                                                                        return <li key={i}>{bulletMatch[1]}</li>;
                                                                    } else if (item.trim()) {
                                                                        // For non-bullet items that aren't empty
                                                                        return <p key={i}>{item}</p>;
                                                                    }
                                                                    return null;
                                                                })
                                                                .filter(Boolean)}
                                                        </div>
                                                    </div>
                                                )}

                                                {incident.ThreadLink && (
                                                    <div className="thread-link-section">
                                                        <div className="section-title">Thread Link</div>
                                                        <a
                                                            href={`${incident.ThreadLink}${formattedResourcePath}`}
                                                            target="_blank"
                                                            rel="noopener noreferrer"
                                                            className="thread-link"
                                                        >
                                                            View incident thread
                                                        </a>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    ))}

                                    {data.IncidentsSummary.AzureMonitor?.map((incident, index) => (
                                        <div key={`azure-${index}`} className="incident-card">
                                            <div className="incident-header azure">
                                                <h3
                                                    className="incident-title"
                                                    style={{
                                                        flex: '1',
                                                        marginRight: '8px',
                                                        whiteSpace: 'nowrap',
                                                        overflow: 'hidden',
                                                        textOverflow: 'ellipsis',
                                                    }}
                                                >
                                                    {incident.Name}
                                                </h3>
                                                <span
                                                    className={`incident-status ${incident.Status.toLowerCase() === 'resolved' ? 'resolved' : 'active'}`}
                                                    style={{
                                                        whiteSpace: 'nowrap',
                                                        minWidth: 'auto',
                                                        textOverflow: 'ellipsis',
                                                        overflow: 'hidden',
                                                    }}
                                                >
                                                    {incident.Status.charAt(0).toUpperCase() + incident.Status.slice(1)}
                                                </span>
                                            </div>
                                            <div className="incident-body">
                                                <div className="incident-details">
                                                    <div className="detail-group">
                                                        <label>Incident ID</label>
                                                        <div>{incident.IncidentId}</div>
                                                    </div>
                                                    {incident.CreateTime && (
                                                        <div className="detail-group">
                                                            <label>Created</label>
                                                            <div>{new Date(incident.CreateTime).toLocaleString()}</div>
                                                        </div>
                                                    )}
                                                    {incident.Duration && (
                                                        <div className="detail-group">
                                                            <label>Duration</label>
                                                            <div>{incident.Duration}</div>
                                                        </div>
                                                    )}
                                                </div>

                                                {incident.Impact && (
                                                    <div className="impact-box">
                                                        <div className="impact-title">Impact</div>
                                                        <div className="impact-text">
                                                            {incident.Impact.split('\n')
                                                                .map((item, i) => {
                                                                    // Extract bullet point content (handles both - and * bullets)
                                                                    const bulletMatch = item.match(/^[-*]\s+(.*)$/);
                                                                    if (bulletMatch) {
                                                                        return <li key={i}>{bulletMatch[1]}</li>;
                                                                    } else if (item.trim()) {
                                                                        // For non-bullet items that aren't empty
                                                                        return <p key={i}>{item}</p>;
                                                                    }
                                                                    return null;
                                                                })
                                                                .filter(Boolean)}
                                                        </div>
                                                    </div>
                                                )}

                                                {incident.Resolution && (
                                                    <div className="resolution-section">
                                                        <div className="section-title">Resolution</div>
                                                        <div className="section-text">{incident.Resolution}</div>
                                                    </div>
                                                )}

                                                {incident.InvestigationDetails && (
                                                    <div className="investigation-section">
                                                        <div className="section-title">Investigation Details</div>
                                                        <div className="section-text">
                                                            {incident.InvestigationDetails.split('\n')
                                                                .map((item, i) => {
                                                                    // Extract bullet point content (handles both - and * bullets)
                                                                    const bulletMatch = item.match(/^[-*]\s+(.*)$/);
                                                                    if (bulletMatch) {
                                                                        return <li key={i}>{bulletMatch[1]}</li>;
                                                                    } else if (item.trim()) {
                                                                        // For non-bullet items that aren't empty
                                                                        return <p key={i}>{item}</p>;
                                                                    }
                                                                    return null;
                                                                })
                                                                .filter(Boolean)}
                                                        </div>
                                                    </div>
                                                )}

                                                {incident.ThreadLink && (
                                                    <div className="thread-link-section">
                                                        <div className="section-title">Thread Link</div>
                                                        <a
                                                            href={`${incident.ThreadLink}${formattedResourcePath}`}
                                                            target="_blank"
                                                            rel="noopener noreferrer"
                                                            className="thread-link"
                                                        >
                                                            View incident thread
                                                        </a>
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    ))}
                                </>
                            )}
                        </div>
                    )}
                </div>

                {/* Resources Section */}
                <div className="accordion-section">
                    <div
                        className="accordion-header"
                        onClick={() => toggleSection('resources')}
                        style={{
                            backgroundColor:
                                data.Overview.HealthAndPerformance.Unhealthy > 0
                                    ? 'rgba(239, 68, 68, 0.05)'
                                    : data.Overview.HealthAndPerformance.Degraded > 0
                                      ? 'rgba(245, 158, 11, 0.05)'
                                      : 'rgba(16, 185, 129, 0.05)',
                        }}
                    >
                        <h2 className="accordion-title">
                            <HealthPerformanceIcon
                                color={
                                    data.Overview.HealthAndPerformance.Unhealthy > 0
                                        ? '#EF4444'
                                        : data.Overview.HealthAndPerformance.Degraded > 0
                                          ? '#F97316'
                                          : '#10B981'
                                }
                            />
                            Health & Performance
                        </h2>
                        <ChevronIcon isOpen={openSections.resources} />
                    </div>

                    {openSections.resources && (
                        <div className="accordion-content">
                            <div
                                className="explanation-note"
                                style={{
                                    padding: '10px 15px',
                                    marginBottom: '15px',
                                    backgroundColor: 'rgba(59, 130, 246, 0.1)',
                                    borderRadius: '6px',
                                    fontSize: '14px',
                                }}
                            >
                                <i>Note: All metrics shown below represent averages collected over the last 24 hours.</i>
                            </div>
                            {data.AppGroupResourceSummary.map((subscription, subIndex) => {
                                // Group resources by health state
                                const healthyResources = (subscription.AppGroups || []).filter(
                                    r => r.AppHealthInfo.Health.toLowerCase() === 'healthy'
                                );
                                const degradedResources = (subscription.AppGroups || []).filter(
                                    r => r.AppHealthInfo.Health.toLowerCase() === 'degraded'
                                );
                                const unhealthyResources = (subscription.AppGroups || []).filter(
                                    r => r.AppHealthInfo.Health.toLowerCase() === 'unhealthy'
                                );

                                return (
                                    <div key={subIndex} className="subscription-section">
                                        <h3 className="subscription-title">{subscription.SubscriptionName}</h3>

                                        {/* Unhealthy Resources Section */}
                                        {unhealthyResources.length > 0 && (
                                            <div className="health-section" style={{ marginBottom: '24px' }}>
                                                <h4
                                                    style={{
                                                        color: '#EF4444',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        padding: '8px 12px',
                                                        backgroundColor: 'rgba(239, 68, 68, 0.1)',
                                                        borderRadius: '4px',
                                                        marginBottom: '16px',
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            width: '10px',
                                                            height: '10px',
                                                            backgroundColor: '#EF4444',
                                                            borderRadius: '50%',
                                                            display: 'inline-block',
                                                            marginRight: '8px',
                                                        }}
                                                    ></span>
                                                    Unhealthy Logical App ({unhealthyResources.length})
                                                </h4>
                                                <div className="resources-grid">{renderResourceCards(unhealthyResources, true)}</div>
                                            </div>
                                        )}

                                        {/* Degraded Resources Section */}
                                        {degradedResources.length > 0 && (
                                            <div className="health-section" style={{ marginBottom: '24px' }}>
                                                <h4
                                                    style={{
                                                        color: '#F97316',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        padding: '8px 12px',
                                                        backgroundColor: 'rgba(249, 115, 22, 0.1)',
                                                        borderRadius: '4px',
                                                        marginBottom: '16px',
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            width: '10px',
                                                            height: '10px',
                                                            backgroundColor: '#F97316',
                                                            borderRadius: '50%',
                                                            display: 'inline-block',
                                                            marginRight: '8px',
                                                        }}
                                                    ></span>
                                                    Degraded Logical Apps ({degradedResources.length})
                                                </h4>
                                                <div className="resources-grid">{renderResourceCards(degradedResources, true)}</div>
                                            </div>
                                        )}

                                        {/* Healthy Resources Section */}
                                        {healthyResources.length > 0 && (
                                            <div className="health-section">
                                                <h4
                                                    style={{
                                                        color: '#10B981',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        padding: '8px 12px',
                                                        backgroundColor: 'rgba(16, 185, 129, 0.1)',
                                                        borderRadius: '4px',
                                                        marginBottom: '16px',
                                                    }}
                                                >
                                                    <span
                                                        style={{
                                                            width: '10px',
                                                            height: '10px',
                                                            backgroundColor: '#10B981',
                                                            borderRadius: '50%',
                                                            display: 'inline-block',
                                                            marginRight: '8px',
                                                        }}
                                                    ></span>
                                                    Healthy Logical Apps ({healthyResources.length})
                                                </h4>
                                                <div className="resources-grid" style={{ gridTemplateColumns: 'repeat(2, 1fr)' }}>
                                                    {renderResourceCards(healthyResources, false)}
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                );
                            })}
                        </div>
                    )}
                </div>

                {/* Actions Section */}
                <div className="accordion-section">
                    <div className="accordion-header" onClick={() => toggleSection('actions')}>
                        <h2 className="accordion-title">
                            <ActionsIcon />
                            Actions & Key Insights
                        </h2>
                        <ChevronIcon isOpen={openSections.actions} />
                    </div>

                    {openSections.actions && data.RecommendedActionsAndObservations && (
                        <div className="accordion-content">
                            {data.RecommendedActionsAndObservations?.Actions &&
                                Array.isArray(data.RecommendedActionsAndObservations.Actions) &&
                                data.RecommendedActionsAndObservations.Actions.length > 0 && (
                                    <div className="actions-section">
                                        <h3 className="section-title">Recommended Actions</h3>
                                        {data.RecommendedActionsAndObservations.Actions.map((action, index) => (
                                            <div
                                                key={index}
                                                className={`action-item ${getPriorityClass(action.Priority)}`}
                                                style={{ borderLeftWidth: '4px' }}
                                            >
                                                <div
                                                    className="priority-badge"
                                                    style={{
                                                        backgroundColor: `${getStatusColor(action.Priority)}20`,
                                                        color: getStatusColor(action.Priority),
                                                    }}
                                                >
                                                    {action.Priority} Priority
                                                </div>
                                                <p className="action-description">{action.Description}</p>
                                                <div className="action-meta">
                                                    {action.Assignee && (
                                                        <div className="assignee-group">
                                                            <span className="meta-label">Assignee:</span>
                                                            <span className="meta-value">{action.Assignee}</span>
                                                        </div>
                                                    )}
                                                    <div className="eta-group">
                                                        <span className="meta-label">ETA:</span>
                                                        <span className="meta-value">{action.ETA}</span>
                                                    </div>
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                )}

                            {data.RecommendedActionsAndObservations?.Observations &&
                                Array.isArray(data.RecommendedActionsAndObservations.Observations) &&
                                data.RecommendedActionsAndObservations.Observations.length > 0 && (
                                    <div className="observations-section">
                                        <h3 className="section-title">Key Insights</h3>
                                        <div className="observations-list">
                                            {data.RecommendedActionsAndObservations.Observations.map((observation, index) => {
                                                const observations = data.RecommendedActionsAndObservations?.Observations;
                                                const isLast = observations ? index === observations.length - 1 : true;
                                                return (
                                                    <div key={index} className={`observation-item ${!isLast ? 'with-border' : ''}`}>
                                                        <span className="observation-bullet">•</span>
                                                        <div className="observation-text">{observation}</div>
                                                    </div>
                                                );
                                            })}
                                        </div>
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
