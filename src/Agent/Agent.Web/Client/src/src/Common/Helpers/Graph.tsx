import { DataVizPalette, IChartProps, IGroupedVerticalBarChartData, IGVBarChartSeriesPoint } from '@fluentui/react-charting';

/**
 * Converts Fluent line chart data (IChartProps) into GroupedVerticalBarChart (IGroupedVerticalBarChartData) data
 *
 * If <= 7 days (each data point is always one day) -> each day is a series
 * If > 7 days/data-points -> split into four mostly-equal chunks/series
 */
export const convertLineChartToAdaptiveGroupedRanges = (
    data: IChartProps | undefined,
    maxDaysUntilBucket = 7,
    numBuckets = 4
): IGroupedVerticalBarChartData[] | undefined => {
    if (!data?.lineChartData?.length) return undefined;

    const legendOrder = data.lineChartData.map(series => series.legend);

    const legendColor: Record<string, string> = {};
    const legendDayTotals: Record<string, Map<number, number>> = {};
    const distinctDayKeys = new Set<number>();

    const truncateToDayStartMs = (date: Date) => new Date(date.getFullYear(), date.getMonth(), date.getDate()).getTime();
    const formatDate = (ms: number) => new Date(ms).toLocaleDateString(undefined, { month: 'short', day: 'numeric' });

    for (const series of data.lineChartData) {
        legendColor[series.legend] = series.color || legendColor[series.legend] || '';
        const dayMap = (legendDayTotals[series.legend] = legendDayTotals[series.legend] || new Map());

        series.data.forEach(point => {
            // Skip if X isn't a date (should never happen)
            if (!point || !(point.x instanceof Date)) return;

            const date = point.x;
            const dayKey = truncateToDayStartMs(date);
            distinctDayKeys.add(dayKey);
            // Sum if multiple entries for same day (should never happen)
            dayMap.set(dayKey, (dayMap.get(dayKey) ?? 0) + (point.y ?? 0));
        });
    }

    const days = Array.from(distinctDayKeys).sort((a, b) => a - b);
    const numDays = days.length;

    if (numDays === 0) return undefined;

    let buckets: { start: number; end: number; dayKeys: number[] }[] = [];
    if (numDays <= maxDaysUntilBucket) {
        buckets = days.map(day => ({ start: day, end: day, dayKeys: [day] }));
    } else {
        // Bucket-forming logic
        const flooredNumBuckets = Math.min(numBuckets, numDays); // Don't create more buckets than days
        const base = Math.floor(numDays / flooredNumBuckets);
        const remainder = numDays % flooredNumBuckets;
        let idx = 0;
        for (let bucketIdx = 0; bucketIdx < flooredNumBuckets; bucketIdx++) {
            const size = base + (bucketIdx < remainder ? 1 : 0);
            const slice = days.slice(idx, idx + size);
            idx += size;
            if (slice.length === 0) continue;
            buckets.push({ start: slice[0], end: slice[slice.length - 1], dayKeys: slice });
        }
    }

    const groups: IGroupedVerticalBarChartData[] = buckets.map(bucket => {
        // If a day, show the date. If a bucket, show the date range
        const name = bucket.start === bucket.end ? formatDate(bucket.start) : `${formatDate(bucket.start)} - ${formatDate(bucket.end)}`;

        const seriesPoints: IGVBarChartSeriesPoint[] = legendOrder.map(legend => {
            const dayMap = legendDayTotals[legend];
            const total = dayMap ? bucket.dayKeys.reduce((sum, dayKey) => sum + (dayMap.get(dayKey) ?? 0), 0) : 0;

            return {
                key: `${legend}-${bucket.start}-${bucket.end}`,
                legend,
                color: legendColor[legend] || DataVizPalette.color1,
                data: total,
            };
        });

        return { name, series: seriesPoints };
    });

    return groups;
};
