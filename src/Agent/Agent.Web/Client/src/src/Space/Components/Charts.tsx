import { toPng } from 'html-to-image';
import React, { useMemo, useRef, useState } from 'react';
import {
    Area,
    Bar,
    BarChart,
    CartesianGrid,
    Cell,
    Label,
    Legend,
    Line,
    LineChart,
    Pie,
    PieChart,
    ResponsiveContainer,
    Scatter,
    ScatterChart,
    Tooltip,
    XAxis,
    YAxis,
} from 'recharts';

interface ChartData {
    type: 'line' | 'bar' | 'pie' | 'scatter' | 'heatmap';
    title: string;
    data: any[];
    xAxisLabel?: string;
    yAxisLabel?: string;
    yAxisMin?: number | 'auto';
    yAxisMax?: number | 'auto';
    xField?: string;
    yField?: string;
    valueField?: string;
    colorLabel?: string;
}
interface PieDataPoint {
    label: string;
    value: number;
}
interface ScatterDataPoint {
    x: number;
    y: number;
    label: string;
}
interface AgentChartProps {
    messageText: string;
}

// Determine if text should be white or black based on background color
const getContrastTextColor = (hexColor: string): string => {
    // Convert hex to RGB
    const r = parseInt(hexColor.slice(1, 3), 16);
    const g = parseInt(hexColor.slice(3, 5), 16);
    const b = parseInt(hexColor.slice(5, 7), 16);

    // Calculate luminance - standard formula for perceived brightness
    const luminance = (0.299 * r + 0.587 * g + 0.114 * b) / 255;

    // Use white text on dark backgrounds, black text on light backgrounds
    return luminance > 0.5 ? '#000000' : '#ffffff';
};

const CHART_COLORS = [
    '#4F46E5', // indigo primary
    '#10B981', // emerald
    '#F59E0B', // amber
    '#EF4444', // red
    '#8B5CF6', // violet
    '#EC4899', // pink
    '#06B6D4', // cyan
    '#F97316', // orange
    '#6366F1', // indigo variant
];

const HEAT_MAP_COLORS = [
    '#084081', // dark blue
    '#0868ac', // blue
    '#43a2ca', // light blue
    '#7bccc4', // turquoise
    '#a8ddb5', // light green
    '#ccebc5', // pale green
    '#f0f9b8', // yellow-green
    '#fef0a9', // light yellow
    '#fedda0', // yellow
    '#fdbb84', // orange
    '#fc8d59', // dark orange
    '#e34a33', // red
    '#b30000', // dark red
];

const getAreaColors = (index: number) => {
    const baseColor = CHART_COLORS[index % CHART_COLORS.length];
    return {
        fill: `${baseColor}20`, // 12% opacity
        stroke: baseColor,
        gradient: [
            { offset: '0%', color: `${baseColor}40` }, // 25% opacity
            { offset: '100%', color: `${baseColor}10` }, // 6% opacity
        ],
    };
};

const extractChartData = (text: string): ChartData | null => {
    const chartRegex = /```chart-data\n([\s\S]*?)\n```/;
    const match = text.match(chartRegex);

    if (match && match[1]) {
        try {
            return JSON.parse(match[1]);
        } catch (error) {
            console.error('Failed to parse chart data:', error);
            return null;
        }
    }

    return null;
};

const AgentChart: React.FC<AgentChartProps> = ({ messageText }) => {
    const chartData = useMemo(() => extractChartData(messageText), [messageText]);
    const description = useMemo(() => {
        const descriptionRegex = /```chart-data\n[\s\S]*?\n```\n([\s\S]*)/;
        const match = messageText.match(descriptionRegex);
        return match ? match[1] : '';
    }, [messageText]);

    // Refs for accessing DOM elements
    const chartRef = useRef<HTMLDivElement>(null);
    const zoomedChartRef = useRef<HTMLDivElement>(null);

    // State for zoom modal
    const [isZoomed, setIsZoomed] = useState(false);

    if (!chartData) {
        return null;
    }

    // Function to take screenshot
    const takeScreenshot = (e?: React.MouseEvent) => {
        e?.stopPropagation(); // Prevent triggering zoom

        const refToUse = isZoomed ? zoomedChartRef : chartRef;

        if (refToUse.current) {
            toPng(refToUse.current)
                .then(dataUrl => {
                    const link = document.createElement('a');
                    link.download = `${chartData.title.replace(/\s+/g, '-')}-chart.png`;
                    link.href = dataUrl;
                    link.click();
                })
                .catch(error => {
                    console.error('Error taking screenshot:', error);
                });
        }
    };

    // Function to toggle zoom
    const toggleZoom = () => {
        setIsZoomed(!isZoomed);
    };

    const chartContainerStyle: React.CSSProperties = {
        backgroundColor: '#FAFBFC',
        borderRadius: '0.75rem',
        padding: '1.75rem',
        boxShadow: '0 1px 3px rgba(0, 0, 0, 0.05)',
        marginBottom: '1.5rem',
        border: '1px solid #f0f2f5',
        cursor: isZoomed ? 'default' : 'pointer', // Change cursor to indicate clickability
    };

    const chartTitleStyle: React.CSSProperties = {
        fontSize: '1.25rem',
        fontWeight: '700',
        color: '#111827',
        marginBottom: '1.25rem',
        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
    };

    const screenshotIconStyle: React.CSSProperties = {
        position: 'absolute',
        top: '1rem',
        right: '1rem',
        cursor: 'pointer',
        padding: '6px',
        backgroundColor: 'transparent',
        border: 'none',
        zIndex: 100,
    };

    const modalOverlayStyle: React.CSSProperties = isZoomed
        ? {
              position: 'fixed',
              top: 0,
              left: 0,
              right: 0,
              bottom: 0,
              backgroundColor: 'rgba(0, 0, 0, 0.6)',
              zIndex: 1000,
              display: 'flex',
              justifyContent: 'center',
              alignItems: 'center',
          }
        : { display: 'none' };

    const modalContentStyle: React.CSSProperties = {
        backgroundColor: 'white',
        padding: '1rem',
        borderRadius: '0.75rem',
        width: '95%',
        height: '90%',
        position: 'relative',
        boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.25)',
        display: 'flex',
        flexDirection: 'column',
    };

    const closeButtonStyle: React.CSSProperties = {
        position: 'absolute',
        top: '1rem',
        right: '1rem',
        backgroundColor: 'transparent',
        color: '#4B5563',
        border: 'none',
        width: '32px',
        height: '32px',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        cursor: 'pointer',
        fontSize: '1.5rem',
        fontWeight: 'bold',
        zIndex: 100,
    };

    const renderChart = (isZoomedView = false) => {
        const { type, title, data, xAxisLabel, yAxisLabel, yAxisMin, yAxisMax } = chartData;
        const ref = isZoomedView ? zoomedChartRef : chartRef;

        const tooltipStyle = {
            backgroundColor: '#ffffff',
            border: 'none',
            borderRadius: '0.5rem',
            boxShadow: '0 10px 15px -3px rgba(0, 0, 0, 0.1), 0 4px 6px -2px rgba(0, 0, 0, 0.05)',
            padding: '0.75rem 1rem',
            fontSize: '0.875rem',
            fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
        };

        const axisTickConfig = {
            fontSize: 11.5,
            fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
            fill: '#6B7280',
        };

        const containerStyle = {
            ...chartContainerStyle,
            cursor: isZoomedView ? 'default' : 'pointer',
            height: isZoomedView ? '90%' : 'auto',
            width: '100%',
            position: 'relative' as const,
        };

        // Screenshot icon for normal view
        const screenshotButton = (
            <button style={screenshotIconStyle} onClick={takeScreenshot} aria-label="Take Screenshot">
                <svg
                    width="20"
                    height="20"
                    viewBox="0 0 24 24"
                    fill="none"
                    stroke="#9CA3AF"
                    strokeWidth="2"
                    strokeLinecap="round"
                    strokeLinejoin="round"
                >
                    <rect x="2" y="6" width="20" height="14" rx="2" ry="2" />
                    <circle cx="12" cy="13" r="4" />
                    <line x1="8" y1="4" x2="16" y2="4" />
                </svg>
            </button>
        );

        switch (type) {
            case 'line': {
                const seriesNames = Object.keys(data[0]).filter(key => key !== 'name');
                return (
                    <div ref={ref} style={containerStyle} onClick={!isZoomedView ? toggleZoom : undefined}>
                        {!isZoomedView && screenshotButton}
                        <div style={chartTitleStyle}>{title}</div>
                        <ResponsiveContainer width="100%" height={isZoomedView ? 600 : 400}>
                            <LineChart data={data} margin={{ top: 10, right: 30, left: 25, bottom: 40 }}>
                                <defs>
                                    {seriesNames.map((dataKey, index) => (
                                        <linearGradient
                                            key={`gradient-${dataKey}`}
                                            id={`color-${dataKey}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                            x1="0"
                                            y1="0"
                                            x2="0"
                                            y2="1"
                                        >
                                            {getAreaColors(index).gradient.map((stop, stopIndex) => (
                                                <stop key={`stop-${stopIndex}`} offset={stop.offset} stopColor={stop.color} />
                                            ))}
                                        </linearGradient>
                                    ))}
                                </defs>
                                <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" strokeOpacity={0.4} vertical={false} />
                                <XAxis
                                    dataKey="name"
                                    tick={axisTickConfig}
                                    stroke="#E5E7EB"
                                    tickLine={{ stroke: '#E5E7EB' }}
                                    axisLine={{ stroke: '#E5E7EB' }}
                                    padding={{ left: 5, right: 5 }}
                                >
                                    {xAxisLabel && (
                                        <Label
                                            value={xAxisLabel}
                                            position="insideBottom"
                                            offset={-15}
                                            style={{
                                                fontSize: 13,
                                                fontWeight: 600,
                                                fill: '#374151',
                                                fontFamily:
                                                    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                            }}
                                        />
                                    )}
                                </XAxis>
                                <YAxis
                                    domain={[yAxisMin || 'auto', yAxisMax || 'auto']}
                                    tick={axisTickConfig}
                                    stroke="#E5E7EB"
                                    tickLine={{ stroke: '#E5E7EB' }}
                                    axisLine={{ stroke: '#E5E7EB' }}
                                >
                                    {yAxisLabel && (
                                        <Label
                                            value={yAxisLabel}
                                            angle={-90}
                                            position="insideLeft"
                                            offset={-10}
                                            style={{
                                                fontSize: 13,
                                                fontWeight: 600,
                                                fill: '#374151',
                                                fontFamily:
                                                    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                            }}
                                        />
                                    )}
                                </YAxis>
                                <Tooltip
                                    contentStyle={tooltipStyle}
                                    cursor={{ stroke: '#9CA3AF', strokeWidth: 1, strokeDasharray: '5 5' }}
                                />
                                <Legend
                                    wrapperStyle={{
                                        paddingTop: '20px',
                                        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                        fontSize: '0.875rem',
                                    }}
                                    iconType="circle"
                                    iconSize={8}
                                />

                                {/* First render area fills with low opacity behind the lines */}
                                {seriesNames.map((dataKey, _) => (
                                    <Area
                                        key={`area-${dataKey}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                        type="monotone"
                                        dataKey={dataKey}
                                        strokeWidth={0}
                                        fill={`url(#color-${dataKey}-${isZoomedView ? 'zoomed' : 'normal'})`}
                                        fillOpacity={0.5}
                                        isAnimationActive={true}
                                    />
                                ))}

                                {/* Then render the actual lines on top with greater prominence */}
                                {seriesNames.map((dataKey, index) => (
                                    <Line
                                        key={`line-${dataKey}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                        type="monotone"
                                        dataKey={dataKey}
                                        stroke={CHART_COLORS[index % CHART_COLORS.length]}
                                        strokeWidth={3}
                                        dot={{
                                            r: 0,
                                            strokeWidth: 0,
                                            fill: CHART_COLORS[index % CHART_COLORS.length],
                                        }}
                                        activeDot={{
                                            r: 6,
                                            strokeWidth: 2,
                                            stroke: '#FFFFFF',
                                            fill: CHART_COLORS[index % CHART_COLORS.length],
                                        }}
                                        isAnimationActive={true}
                                    />
                                ))}
                            </LineChart>
                        </ResponsiveContainer>
                    </div>
                );
            }
            case 'bar':
                return (
                    <div ref={ref} style={containerStyle} onClick={!isZoomedView ? toggleZoom : undefined}>
                        {!isZoomedView && screenshotButton}
                        <div style={chartTitleStyle}>{title}</div>
                        <ResponsiveContainer width="100%" height={isZoomedView ? 600 : 400}>
                            <BarChart data={data} margin={{ top: 10, right: 30, left: 25, bottom: 40 }} barSize={48} barGap={2}>
                                <defs>
                                    <linearGradient id={`barGradient-${isZoomedView ? 'zoomed' : 'normal'}`} x1="0" y1="0" x2="0" y2="1">
                                        <stop offset="0%" stopColor={CHART_COLORS[0]} stopOpacity={1} />
                                        <stop offset="100%" stopColor={CHART_COLORS[0]} stopOpacity={0.8} />
                                    </linearGradient>
                                </defs>
                                <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" vertical={false} />
                                <XAxis
                                    dataKey="category"
                                    tick={axisTickConfig}
                                    stroke="#E5E7EB"
                                    tickLine={{ stroke: '#E5E7EB' }}
                                    axisLine={{ stroke: '#E5E7EB' }}
                                    padding={{ left: 10, right: 10 }}
                                >
                                    {xAxisLabel && (
                                        <Label
                                            value={xAxisLabel}
                                            position="insideBottom"
                                            offset={-15}
                                            style={{
                                                fontSize: 13,
                                                fontWeight: 600,
                                                fill: '#374151',
                                                fontFamily:
                                                    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                            }}
                                        />
                                    )}
                                </XAxis>
                                <YAxis
                                    tick={axisTickConfig}
                                    stroke="#E5E7EB"
                                    tickLine={{ stroke: '#E5E7EB' }}
                                    axisLine={{ stroke: '#E5E7EB' }}
                                >
                                    {yAxisLabel && (
                                        <Label
                                            value={yAxisLabel}
                                            angle={-90}
                                            position="insideLeft"
                                            offset={-10}
                                            style={{
                                                fontSize: 13,
                                                fontWeight: 600,
                                                fill: '#374151',
                                                fontFamily:
                                                    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                            }}
                                        />
                                    )}
                                </YAxis>
                                <Tooltip
                                    contentStyle={tooltipStyle}
                                    cursor={{ fill: 'rgba(220, 220, 220, 0.2)' }}
                                    formatter={value => [`${value}`, 'Value']}
                                />
                                <Legend
                                    wrapperStyle={{
                                        paddingTop: '20px',
                                        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                        fontSize: '0.875rem',
                                    }}
                                />
                                <Bar
                                    dataKey="value"
                                    fill={`url(#barGradient-${isZoomedView ? 'zoomed' : 'normal'})`}
                                    radius={[4, 4, 0, 0]}
                                    isAnimationActive={true}
                                    animationDuration={800}
                                >
                                    {data.map((_, index) => (
                                        <Cell
                                            key={`cell-${index}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                            fill={CHART_COLORS[index % CHART_COLORS.length]}
                                            fillOpacity={0.9}
                                            stroke={CHART_COLORS[index % CHART_COLORS.length]}
                                            strokeWidth={1}
                                        />
                                    ))}
                                </Bar>
                            </BarChart>
                        </ResponsiveContainer>
                    </div>
                );

            case 'pie': {
                const typedData = data as PieDataPoint[];

                return (
                    <div ref={ref} style={containerStyle} onClick={!isZoomedView ? toggleZoom : undefined}>
                        {!isZoomedView && screenshotButton}
                        <div style={chartTitleStyle}>{title}</div>
                        <ResponsiveContainer width="100%" height={isZoomedView ? 600 : 400}>
                            <PieChart>
                                <defs>
                                    {CHART_COLORS.map((color: string, index: number) => (
                                        <linearGradient
                                            key={`gradientPie-${index}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                            id={`colorPie-${index}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                            x1="0"
                                            y1="0"
                                            x2="0"
                                            y2="1"
                                        >
                                            <stop offset="0%" stopColor={color} stopOpacity={0.9} />
                                            <stop offset="100%" stopColor={color} stopOpacity={0.7} />
                                        </linearGradient>
                                    ))}
                                </defs>
                                <Pie
                                    data={typedData}
                                    cx="50%"
                                    cy="50%"
                                    labelLine={{
                                        stroke: '#6B7280',
                                        strokeWidth: 1.5,
                                        strokeDasharray: '2 2',
                                    }}
                                    label={({ name, value, percent }: any) => `${name}: ${value} (${(percent * 100).toFixed(0)}%)`}
                                    outerRadius={isZoomedView ? '40%' : 160}
                                    innerRadius={isZoomedView ? '20%' : 60}
                                    paddingAngle={2}
                                    cornerRadius={3}
                                    fill="#8884d8"
                                    dataKey="value"
                                    nameKey="label"
                                    isAnimationActive={true}
                                    animationDuration={800}
                                    animationBegin={100}
                                >
                                    {typedData.map((_: PieDataPoint, index: number) => (
                                        <Cell
                                            key={`cell-${index}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                            fill={`url(#colorPie-${index % CHART_COLORS.length}-${isZoomedView ? 'zoomed' : 'normal'})`}
                                            stroke={CHART_COLORS[index % CHART_COLORS.length]}
                                            strokeWidth={1}
                                        />
                                    ))}
                                </Pie>
                                <Tooltip
                                    formatter={(value: any, _: string, props: any) => [`${value}`, props.payload.label]}
                                    contentStyle={tooltipStyle}
                                />
                                <Legend
                                    layout="horizontal"
                                    verticalAlign="bottom"
                                    align="center"
                                    wrapperStyle={{
                                        paddingTop: '20px',
                                        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                        fontSize: '0.875rem',
                                    }}
                                    iconType="circle"
                                    iconSize={8}
                                    formatter={(_: string, entry: any) => (
                                        <span style={{ color: '#374151', fontWeight: entry.payload.value > 30 ? 'bold' : 'normal' }}>
                                            {entry.payload.label}
                                        </span>
                                    )}
                                />
                            </PieChart>
                        </ResponsiveContainer>
                    </div>
                );
            }

            case 'heatmap': {
                // -------------------- data prep --------------------
                const { title, data, xAxisLabel, yAxisLabel, colorLabel } = chartData;

                const xCategories = [...new Set(data.map(d => d.x))].sort();
                const yCategories = [...new Set(data.map(d => d.y))].sort();

                let minValue = Number.MAX_VALUE;
                let maxValue = Number.MIN_VALUE;
                data.forEach(d => {
                    if (d.value !== null && d.value !== undefined) {
                        minValue = Math.min(minValue, d.value);
                        maxValue = Math.max(maxValue, d.value);
                    }
                });
                if (minValue === maxValue || minValue === Number.MAX_VALUE) {
                    minValue = Math.max(0, minValue - 1);
                    maxValue += 1;
                }

                const getHeatMapColor = (v: number | null | undefined): string => {
                    if (v === null || v === undefined) return '#f5f5f5';
                    const t = (v - minValue) / (maxValue - minValue);
                    const idx = Math.min(Math.floor(t * HEAT_MAP_COLORS.length), HEAT_MAP_COLORS.length - 1);
                    return HEAT_MAP_COLORS[idx];
                };

                // -------------------- layout constants --------------------
                const rowHeight = isZoomedView ? 60 : 40;
                const gridHeight = yCategories.length * rowHeight;

                const chartContainerStyle: React.CSSProperties = {
                    display: 'flex',
                    flexDirection: 'column',
                    position: 'relative',
                    // let it size itself by its content; no minHeight “floor”
                    overflowX: isZoomedView ? 'auto' : undefined,
                };

                return (
                    <div
                        ref={ref}
                        style={{
                            ...containerStyle,
                            display: 'flex',
                            flexDirection: 'column',
                            padding: isZoomedView ? '4rem 3.5rem 1.5rem 3.5rem' : '3.5rem 3rem 1rem 3rem',
                        }}
                        onClick={!isZoomedView ? toggleZoom : undefined}
                    >
                        {!isZoomedView && screenshotButton}

                        {/* title */}
                        <div style={{ ...chartTitleStyle, marginTop: 0, marginBottom: '1.75rem' }}>{title}</div>

                        {/* chart container */}
                        <div style={chartContainerStyle}>
                            {/* y‑axis label (zoom only) */}
                            {yAxisLabel && isZoomedView && (
                                <div
                                    style={{
                                        position: 'absolute',
                                        left: '-30px',
                                        top: '50%',
                                        transform: 'translateY(-50%) rotate(-90deg)',
                                        fontSize: 13,
                                        fontWeight: 600,
                                        color: '#374151',
                                        fontFamily: '-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif',
                                    }}
                                >
                                    {yAxisLabel}
                                </div>
                            )}

                            {/* y‑ticks + grid */}
                            <div style={{ display: 'flex' }}>
                                {/* y‑ticks */}
                                <div
                                    style={{
                                        display: 'flex',
                                        flexDirection: 'column',
                                        marginRight: 12,
                                        justifyContent: 'space-around',
                                        height: gridHeight,
                                    }}
                                >
                                    {yCategories.map(y => (
                                        <div
                                            key={y}
                                            style={{
                                                height: rowHeight,
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'flex-end',
                                                fontSize: 11.5,
                                                color: '#6B7280',
                                                fontFamily: '-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif',
                                                maxWidth: 120,
                                                overflow: 'hidden',
                                                textOverflow: 'ellipsis',
                                                whiteSpace: 'nowrap',
                                            }}
                                        >
                                            {y}
                                        </div>
                                    ))}
                                </div>

                                {/* grid + x‑axis */}
                                <div style={{ flex: 1, display: 'flex', flexDirection: 'column' }}>
                                    {/* grid */}
                                    <div
                                        style={{
                                            display: 'grid',
                                            gridTemplateColumns: `repeat(${xCategories.length}, 1fr)`,
                                            gridTemplateRows: `repeat(${yCategories.length}, ${rowHeight}px)`, // 🔹 NEW
                                            gap: 1,
                                        }}
                                    >
                                        {yCategories.flatMap(y =>
                                            xCategories.map(x => {
                                                const d = data.find(pt => pt.x === x && pt.y === y);
                                                const val = d?.value;
                                                const bg = getHeatMapColor(val);
                                                const fg = getContrastTextColor(bg);
                                                return (
                                                    <div
                                                        key={`${x}-${y}`}
                                                        style={{
                                                            backgroundColor: bg,
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center',
                                                            color: fg,
                                                            fontWeight: 500,
                                                            fontSize: 13,
                                                            fontFamily:
                                                                '-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif',
                                                        }}
                                                        title={`${x}, ${y}: ${val}`}
                                                    >
                                                        {val}
                                                    </div>
                                                );
                                            })
                                        )}
                                    </div>

                                    {/* x‑ticks */}
                                    <div
                                        style={{
                                            display: 'grid',
                                            gridTemplateColumns: `repeat(${xCategories.length}, 1fr)`,
                                            marginTop: 8,
                                            padding: '0 4px',
                                        }}
                                    >
                                        {xCategories.map(x => (
                                            <div
                                                key={x}
                                                style={{
                                                    textAlign: 'center',
                                                    fontSize: 11.5,
                                                    color: '#6B7280',
                                                    fontFamily:
                                                        '-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif',
                                                    overflow: 'hidden',
                                                    textOverflow: 'ellipsis',
                                                    whiteSpace: 'nowrap',
                                                }}
                                            >
                                                {x}
                                            </div>
                                        ))}
                                    </div>

                                    {/* x‑axis label (zoom only) */}
                                    {xAxisLabel && isZoomedView && (
                                        <div
                                            style={{
                                                textAlign: 'center',
                                                marginTop: 12,
                                                fontSize: 13,
                                                fontWeight: 600,
                                                color: '#374151',
                                                fontFamily: '-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif',
                                            }}
                                        >
                                            {xAxisLabel}
                                        </div>
                                    )}
                                </div>
                            </div>
                        </div>

                        {/* legend */}
                        <div
                            style={{
                                display: 'flex',
                                flexDirection: 'column',
                                alignItems: 'center',
                                marginTop: '1rem',
                                marginBottom: '0.5rem',
                                fontFamily: '-apple-system,BlinkMacSystemFont,"Segoe UI",Roboto,Helvetica,Arial,sans-serif',
                            }}
                        >
                            {/* legend title (zoom only) */}
                            {isZoomedView && (
                                <div
                                    style={{
                                        fontSize: '0.8rem',
                                        marginBottom: '0.3rem',
                                        color: '#374151',
                                    }}
                                >
                                    {colorLabel || 'Value'}
                                </div>
                            )}

                            {/* colour bar */}
                            <div
                                style={{
                                    display: 'flex',
                                    width: '80%',
                                    height: 18,
                                    borderRadius: 4,
                                    overflow: 'hidden',
                                }}
                            >
                                {HEAT_MAP_COLORS.map((c, i) => (
                                    <div
                                        key={i}
                                        style={{
                                            flex: 1,
                                            backgroundColor: c,
                                            border: '1px solid rgba(255,255,255,0.3)',
                                            borderLeft: 'none',
                                            borderRight: 'none',
                                        }}
                                    />
                                ))}
                            </div>

                            {/* min / mid / max */}
                            <div
                                style={{
                                    display: 'flex',
                                    width: '80%',
                                    justifyContent: 'space-between',
                                    marginTop: '0.25rem',
                                    fontSize: '0.7rem',
                                    color: '#6B7280',
                                }}
                            >
                                <span>{minValue.toFixed(1)}</span>
                                <span>{((maxValue + minValue) / 2).toFixed(1)}</span>
                                <span>{maxValue.toFixed(1)}</span>
                            </div>
                        </div>
                    </div>
                );
            }

            case 'scatter': {
                // Using the updated interface without isAnomaly
                const typedData = data as ScatterDataPoint[];

                return (
                    <div ref={ref} style={containerStyle} onClick={!isZoomedView ? toggleZoom : undefined}>
                        {!isZoomedView && screenshotButton}
                        <div style={chartTitleStyle}>{title}</div>
                        <ResponsiveContainer width="100%" height={isZoomedView ? 600 : 400}>
                            <ScatterChart margin={{ top: 20, right: 30, left: 25, bottom: 40 }}>
                                <CartesianGrid strokeDasharray="3 3" stroke="#E5E7EB" strokeOpacity={0.4} />
                                <XAxis
                                    type="number"
                                    dataKey="x"
                                    name={xAxisLabel}
                                    tick={axisTickConfig}
                                    stroke="#E5E7EB"
                                    tickLine={{ stroke: '#E5E7EB' }}
                                    axisLine={{ stroke: '#E5E7EB' }}
                                    padding={{ left: 10, right: 10 }}
                                >
                                    {xAxisLabel && (
                                        <Label
                                            value={xAxisLabel}
                                            position="insideBottom"
                                            offset={-15}
                                            style={{
                                                fontSize: 13,
                                                fontWeight: 600,
                                                fill: '#374151',
                                                fontFamily:
                                                    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                            }}
                                        />
                                    )}
                                </XAxis>
                                <YAxis
                                    type="number"
                                    dataKey="y"
                                    name={yAxisLabel}
                                    tick={axisTickConfig}
                                    stroke="#E5E7EB"
                                    tickLine={{ stroke: '#E5E7EB' }}
                                    axisLine={{ stroke: '#E5E7EB' }}
                                >
                                    {yAxisLabel && (
                                        <Label
                                            value={yAxisLabel}
                                            angle={-90}
                                            position="insideLeft"
                                            offset={-10}
                                            style={{
                                                fontSize: 13,
                                                fontWeight: 600,
                                                fill: '#374151',
                                                fontFamily:
                                                    '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                                            }}
                                        />
                                    )}
                                </YAxis>
                                <Tooltip
                                    cursor={{ strokeDasharray: '3 3', stroke: '#9CA3AF', strokeWidth: 1 }}
                                    contentStyle={tooltipStyle}
                                    formatter={(value: any, name: string) => [value, name]}
                                    labelFormatter={(_: any, payload: any[]) => {
                                        if (payload && payload.length > 0) {
                                            return payload[0].payload.label || '';
                                        }
                                        return '';
                                    }}
                                />

                                {/* Render all points with a single color */}
                                <Scatter name="Data Points" data={typedData} fill={CHART_COLORS[0]} isAnimationActive={true}>
                                    {typedData.map((_: ScatterDataPoint, index: number) => (
                                        <Cell
                                            key={`cell-${index}-${isZoomedView ? 'zoomed' : 'normal'}`}
                                            fill={CHART_COLORS[0]}
                                            stroke="#FFFFFF"
                                            strokeWidth={1}
                                        />
                                    ))}
                                </Scatter>

                                {/* Add text labels for all points */}
                                {typedData.map((entry: ScatterDataPoint, index: number) => (
                                    <text
                                        key={`text-${index}`}
                                        x={entry.x}
                                        y={entry.y}
                                        dx={10}
                                        dy={-10}
                                        fontSize={12}
                                        fontFamily="-apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif"
                                        fill="#4B5563"
                                        textAnchor="start"
                                    >
                                        {entry.label}
                                    </text>
                                ))}
                            </ScatterChart>
                        </ResponsiveContainer>
                    </div>
                );
            }

            default:
                return <div>Unsupported chart type: {type}</div>;
        }
    };

    return (
        <div>
            {/* Chart component */}
            {renderChart(false)}

            {/* Description section */}
            {description && (
                <div
                    style={{
                        marginTop: '1rem',
                        fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
                        fontSize: '0.9375rem',
                        lineHeight: '1.5',
                        color: '#4B5563',
                    }}
                >
                    {description}
                </div>
            )}

            {/* Zoom Mohdal */}
            {isZoomed && (
                <div style={modalOverlayStyle as React.CSSProperties} onClick={toggleZoom}>
                    <div
                        style={{
                            ...(modalContentStyle as React.CSSProperties),
                            overflow: 'hidden',
                        }}
                        onClick={e => e.stopPropagation()} // Prevent closing when clicking on content
                    >
                        {/* Simple dark gray cross button */}
                        <button style={closeButtonStyle as React.CSSProperties} onClick={toggleZoom} aria-label="Close">
                            ×
                        </button>

                        {/* Screenshot icon within the modal */}
                        <div
                            style={
                                {
                                    position: 'absolute',
                                    top: '1rem',
                                    right: '3.5rem',
                                    zIndex: 100,
                                } as React.CSSProperties
                            }
                        >
                            <button
                                style={
                                    {
                                        backgroundColor: 'transparent',
                                        border: 'none',
                                        cursor: 'pointer',
                                        padding: '6px',
                                    } as React.CSSProperties
                                }
                                onClick={takeScreenshot}
                                aria-label="Take Screenshot"
                            >
                                <svg
                                    width="20"
                                    height="20"
                                    viewBox="0 0 24 24"
                                    fill="none"
                                    stroke="#9CA3AF"
                                    strokeWidth="2"
                                    strokeLinecap="round"
                                    strokeLinejoin="round"
                                >
                                    <rect x="2" y="6" width="20" height="14" rx="2" ry="2" />
                                    <circle cx="12" cy="13" r="4" />
                                    <line x1="8" y1="4" x2="16" y2="4" />
                                </svg>
                            </button>
                        </div>

                        {/* Chart container that fills the modal */}
                        <div style={{ flex: 1, width: '100%', position: 'relative', height: '90%' } as React.CSSProperties}>
                            {renderChart(true)}
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
};

export default AgentChart;
