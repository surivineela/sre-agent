import React, { useMemo, useRef, useState } from 'react';
import { Area, CartesianGrid, Label, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts';

import { toPng } from 'html-to-image';

interface ChartData {
    type: 'line' | 'bar' | 'pie' | 'scatter' | 'heatmap';
    title: string;
    data: any[];
    xAxisLabel?: string;
    yAxisLabel?: string;
    yAxisMin?: number | 'auto';
    yAxisMax?: number | 'auto';
    colorLabel?: string; // Added for heat map color scale label
}

interface AgentChartProps {
    messageText: string;
}

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

// Color scale for heat map - from cool to hot (blue gradient)
const HEAT_MAP_COLORS = [
    '#ebf9ff', // very light blue - lowest intensity
    '#bfe8ff', // light blue
    '#7fcfff', // blue
    '#4db7ff', // medium blue
    '#1fa1ff', // bright blue
    '#008aff', // strong blue
    '#0073d8', // darker blue
    '#0055a3', // dark blue
    '#004080', // very dark blue
    '#052e54', // almost black blue - highest intensity
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

// Normalize value to a 0-1 range for color scaling
const normalizeValue = (value: number, min: number, max: number): number => {
    if (min === max) return 0.5; // If all values are the same
    return (value - min) / (max - min);
};

// Get color based on normalized value
const getHeatMapColor = (normalizedValue: number): string => {
    const colorIndex = Math.min(Math.floor(normalizedValue * HEAT_MAP_COLORS.length), HEAT_MAP_COLORS.length - 1);
    return HEAT_MAP_COLORS[colorIndex];
};

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

// Component for rendering color legend for heat map
const ColorLegend = ({ min, max, label }: { min: number; max: number; label: string }) => {
    return (
        <div
            style={{
                display: 'flex',
                flexDirection: 'column',
                alignItems: 'center',
                marginTop: '1rem',
                fontFamily: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
            }}
        >
            <div style={{ fontSize: '0.875rem', marginBottom: '0.5rem', color: '#374151' }}>{label}</div>
            <div style={{ display: 'flex', width: '80%', height: '24px', borderRadius: '4px', overflow: 'hidden' }}>
                {HEAT_MAP_COLORS.map((color, i) => (
                    <div
                        key={i}
                        style={{
                            flex: 1,
                            backgroundColor: color,
                            border: '1px solid rgba(255,255,255,0.3)',
                            borderLeft: 'none',
                            borderRight: 'none',
                        }}
                    />
                ))}
            </div>
            <div
                style={{
                    display: 'flex',
                    width: '80%',
                    justifyContent: 'space-between',
                    marginTop: '0.25rem',
                    fontSize: '0.75rem',
                    color: '#6B7280',
                }}
            >
                <span>{min.toFixed(1)}</span>
                <span>{((max - min) / 2 + min).toFixed(1)}</span>
                <span>{max.toFixed(1)}</span>
            </div>
        </div>
    );
};

// Prepare heat map data for Recharts
const prepareHeatMapData = (heatMapData: any) => {
    const { xCategories, yCategories, values } = heatMapData;

    // Find min/max values for color scaling
    let minValue = Number.MAX_VALUE;
    let maxValue = Number.MIN_VALUE;

    // If no values have been provided, return empty data
    if (!values || values.length === 0) {
        return {
            data: [],
            xCategories: xCategories || [],
            yCategories: yCategories || [],
            minValue: 0,
            maxValue: 100,
        };
    }

    // Find min and max values
    values.forEach((item: any) => {
        if (item.value !== null && item.value !== undefined) {
            minValue = Math.min(minValue, item.value);
            maxValue = Math.max(maxValue, item.value);
        }
    });

    // If all values are the same, create a small range
    if (minValue === maxValue) {
        minValue = Math.max(0, minValue - 1);
        maxValue = maxValue + 1;
    }

    // Handle edge case where no valid values were found
    if (minValue === Number.MAX_VALUE) {
        minValue = 0;
        maxValue = 100;
    }

    // Create a data structure that Recharts can work with
    const data = [];

    // Create a cell for each x,y combination
    for (let yIndex = 0; yIndex < yCategories.length; yIndex++) {
        const yCategory = yCategories[yIndex];

        for (let xIndex = 0; xIndex < xCategories.length; xIndex++) {
            const xCategory = xCategories[xIndex];

            // Find if there's a value for this x,y pair
            const valueObj = values.find((v: any) => v.x === xCategory && v.y === yCategory);

            const value = valueObj ? valueObj.value : null;

            data.push({
                x: xCategory,
                y: yCategory,
                xIndex,
                yIndex,
                value: value,
            });
        }
    }

    return {
        data,
        xCategories,
        yCategories,
        minValue,
        maxValue,
    };
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
        const { type, title, data, xAxisLabel, yAxisLabel, yAxisMin, yAxisMax, colorLabel } = chartData;
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
                // Line chart implementation (unchanged)
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

                                {/* Area fills with low opacity behind the lines */}
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

                                {/* Lines on top with greater prominence */}
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

            case 'heatmap': {
                // New heat map case with improved compact grid layout
                const { data: heatMapData, xCategories, yCategories, minValue, maxValue } = prepareHeatMapData(data);

                // Calculate the max number of cells to determine appropriate cell size
                const totalCells = xCategories.length * yCategories.length;
                const isLargeGrid = totalCells > 100; // Threshold for compact mode

                // Determine text size based on grid density
                let fontSize = '0.75rem';
                let cellPadding = '6px';
                let showValues = true;

                if (isLargeGrid) {
                    fontSize = '0.6rem';
                    cellPadding = '2px';
                }

                if (xCategories.length > 30 || yCategories.length > 20) {
                    showValues = false; // Hide values if grid is very dense
                }

                return (
                    <div ref={ref} style={containerStyle} onClick={!isZoomedView ? toggleZoom : undefined}>
                        {!isZoomedView && screenshotButton}
                        <div style={chartTitleStyle}>{title}</div>

                        {/* Create a compact grid-based heatmap */}
                        <div
                            style={{
                                display: 'flex',
                                flexDirection: 'column',
                                maxWidth: '100%',
                                overflowX: 'auto',
                            }}
                        >
                            {/* Y-axis labels on the left */}
                            <div
                                style={{
                                    display: 'flex',
                                    flexDirection: 'row',
                                    width: 'fit-content',
                                }}
                            >
                                {/* Y-axis label column */}
                                <div
                                    style={{
                                        display: 'flex',
                                        flexDirection: 'column',
                                        marginRight: '8px',
                                        minWidth: '80px',
                                    }}
                                >
                                    {/* Empty top-left corner cell */}
                                    <div
                                        style={{
                                            height: '24px',
                                            display: 'flex',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            fontWeight: 'bold',
                                            fontSize: '0.75rem',
                                            color: '#374151',
                                            padding: '4px',
                                        }}
                                    >
                                        {yAxisLabel || ''}
                                    </div>

                                    {/* Y-axis labels */}
                                    {yCategories.map((yCat: string, yIndex: number) => (
                                        <div
                                            key={`y-label-${yIndex}`}
                                            style={{
                                                height: '24px',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'flex-end',
                                                fontSize: '0.75rem',
                                                color: '#4B5563',
                                                padding: '4px',
                                                whiteSpace: 'nowrap',
                                                overflow: 'hidden',
                                                textOverflow: 'ellipsis',
                                            }}
                                        >
                                            {yCat}
                                        </div>
                                    ))}
                                </div>

                                {/* Main grid with headers */}
                                <div style={{ display: 'flex', flexDirection: 'column' }}>
                                    {/* X-axis labels row */}
                                    <div
                                        style={{
                                            display: 'flex',
                                            flexDirection: 'row',
                                            height: '24px',
                                            borderBottom: '1px solid #E5E7EB',
                                        }}
                                    >
                                        {xCategories.map((xCat: string, xIndex: number) => (
                                            <div
                                                key={`x-label-${xIndex}`}
                                                style={{
                                                    width: '24px',
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'center',
                                                    fontSize: '0.75rem',
                                                    color: '#4B5563',
                                                    padding: '2px',
                                                    transform: 'rotate(-90deg)',
                                                    transformOrigin: 'center center',
                                                    whiteSpace: 'nowrap',
                                                    overflow: 'hidden',
                                                    textOverflow: 'ellipsis',
                                                }}
                                            >
                                                {xCat}
                                            </div>
                                        ))}
                                    </div>

                                    {/* Main heatmap grid */}
                                    <div
                                        style={{
                                            display: 'flex',
                                            flexDirection: 'column',
                                            border: '1px solid #E5E7EB',
                                            borderRadius: '4px',
                                            overflow: 'hidden',
                                        }}
                                    >
                                        {yCategories.map((yCat: string, yIndex: number) => (
                                            <div
                                                key={`row-${yIndex}`}
                                                style={{
                                                    display: 'flex',
                                                    flexDirection: 'row',
                                                    borderBottom: yIndex < yCategories.length - 1 ? '1px solid #E5E7EB' : 'none',
                                                }}
                                            >
                                                {xCategories.map((xCat: string, xIndex: number) => {
                                                    const cell = heatMapData.find((d: any) => d.x === xCat && d.y === yCat);

                                                    const cellValue = cell?.value;
                                                    const cellColor =
                                                        cellValue !== null && cellValue !== undefined
                                                            ? getHeatMapColor(normalizeValue(cellValue, minValue, maxValue))
                                                            : '#f5f5f5';

                                                    return (
                                                        <div
                                                            key={`cell-${xIndex}-${yIndex}`}
                                                            style={{
                                                                width: '24px',
                                                                height: '24px',
                                                                backgroundColor: cellColor,
                                                                display: 'flex',
                                                                alignItems: 'center',
                                                                justifyContent: 'center',
                                                                fontSize,
                                                                color: getContrastTextColor(cellColor),
                                                                borderRight: xIndex < xCategories.length - 1 ? '1px solid #E5E7EB' : 'none',
                                                                padding: cellPadding,
                                                            }}
                                                            title={`${xCat}, ${yCat}: ${cellValue}`}
                                                        >
                                                            {showValues && cellValue !== null && cellValue !== undefined ? cellValue : ''}
                                                        </div>
                                                    );
                                                })}
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            </div>

                            {/* X-axis label row */}
                            <div
                                style={{
                                    display: 'flex',
                                    justifyContent: 'center',
                                    marginTop: '8px',
                                    fontSize: '0.75rem',
                                    fontWeight: 'bold',
                                    color: '#374151',
                                }}
                            >
                                {xAxisLabel || ''}
                            </div>
                        </div>

                        {/* Color scale legend */}
                        {colorLabel && <ColorLegend min={minValue} max={maxValue} label={colorLabel} />}
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

            {/* Zoom Modal */}
            {isZoomed && (
                <div style={modalOverlayStyle as React.CSSProperties} onClick={toggleZoom}>
                    <div
                        style={{
                            ...(modalContentStyle as React.CSSProperties),
                            overflow: 'auto',
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
