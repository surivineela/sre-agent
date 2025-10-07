import { IChartProps } from '@fluentui/react-charting';
import { describe, expect, it } from 'vitest';
import { convertLineChartToAdaptiveGroupedRanges } from '../Graph';

describe('Graph utilities', () => {
    describe('convertLineChartToAdaptiveGroupedRanges', () => {
        const buildChartProps = (options: {
            days: number;
            legends?: Array<{ name: string; y?: (dayIndex: number) => number }>;
            start?: Date;
            mutate?: (data: any) => void;
        }): IChartProps => {
            const { days, legends = [{ name: 'A', y: () => 1 }], start = new Date(2025, 0, 1), mutate } = options;

            const lineChartData = legends.map(legend => {
                const seriesPoints = Array.from({ length: days }, (_, i) => {
                    const d = new Date(start.getTime());
                    d.setDate(start.getDate() + i);
                    return {
                        x: d,
                        y: legend.y ? legend.y(i) : 1,
                    };
                });
                const series = {
                    legend: legend.name,
                    data: seriesPoints,
                    color: undefined,
                };
                return series;
            });

            const chartProps: IChartProps = {
                lineChartData,
            } as unknown as IChartProps; // Cast since we only provide required parts for our function.

            mutate?.(chartProps);
            return chartProps;
        };

        it('returns undefined for undefined or empty input', () => {
            expect(convertLineChartToAdaptiveGroupedRanges(undefined)).toBeUndefined();
            const empty: IChartProps = { lineChartData: [] } as unknown as IChartProps;
            expect(convertLineChartToAdaptiveGroupedRanges(empty)).toBeUndefined();
        });

        it('returns one group per day when day count <= maxDaysUntilBucket (e.g., 7)', () => {
            const data = buildChartProps({ days: 7 });
            const result = convertLineChartToAdaptiveGroupedRanges(data, 7, 4);
            expect(result).toBeDefined();
            expect(result!.length).toBe(7);
            // Each group should have total = 1 for legend 'A'
            for (const g of result!) {
                expect(g.series[0].data).toBe(1);
            }
        });

        it('splits 8 days into 4 even buckets (2,2,2,2) by default', () => {
            const data = buildChartProps({ days: 8 });
            const result = convertLineChartToAdaptiveGroupedRanges(data); // defaults: maxDays=7, numBuckets=4
            expect(result).toBeDefined();
            expect(result!.length).toBe(4);
            // With y=1 per day, each bucket sum should equal its day count (2).
            result!.forEach(bucket => {
                expect(bucket.series[0].data).toBe(2);
            });
        });

        it('splits 9 days into 4 buckets with remainder distribution (3,2,2,2)', () => {
            const data = buildChartProps({ days: 9 });
            const result = convertLineChartToAdaptiveGroupedRanges(data); // numBuckets=4
            expect(result).toBeDefined();
            expect(result!.length).toBe(4);
            const expected = [3, 2, 2, 2];
            const actual = result!.map(g => g.series[0].data);
            expect(actual).toEqual(expected);
        });

        it('clamps bucket count when numBuckets > number of days (3 days, 10 buckets)', () => {
            const data = buildChartProps({ days: 3 });
            // Set maxDaysUntilBucket to 0 to force bucketing logic (so it doesn't early out into per-day mode just because <= max)
            const result = convertLineChartToAdaptiveGroupedRanges(data, 0, 10);
            expect(result).toBeDefined();
            // Should fall back to one bucket per day (cannot exceed day count)
            expect(result!.length).toBe(3);
            result!.forEach(g => expect(g.series[0].data).toBe(1));
        });

        it('sums duplicate points for the same legend/day', () => {
            const base = new Date(2025, 4, 1);
            const chartProps: IChartProps = {
                lineChartData: [
                    {
                        legend: 'A',
                        color: undefined,
                        data: [
                            { x: new Date(base.getTime()), y: 3 },
                            { x: new Date(base.getTime()), y: 4 }, // same day, duplicate
                        ],
                    },
                ],
            } as unknown as IChartProps;

            const result = convertLineChartToAdaptiveGroupedRanges(chartProps);
            expect(result).toBeDefined();
            expect(result!.length).toBe(1);
            expect(result![0].series[0].data).toBe(7);
        });

        it('ignores non-Date x values', () => {
            const base = new Date(2025, 0, 1);
            const chartProps: IChartProps = {
                lineChartData: [
                    {
                        legend: 'A',
                        color: undefined,
                        data: [
                            { x: new Date(base.getTime()), y: 2 },
                            { x: 'not-a-date', y: 10 },
                        ],
                    },
                ],
            } as unknown as IChartProps;

            const result = convertLineChartToAdaptiveGroupedRanges(chartProps);
            expect(result).toBeDefined();
            expect(result!.length).toBe(1);
            // Only the valid Date point counts
            expect(result![0].series[0].data).toBe(2);
        });
    });
});
