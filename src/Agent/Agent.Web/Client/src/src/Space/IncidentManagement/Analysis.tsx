import { DataVizPalette, getColorFromToken, Sparkline } from '@fluentui/react-charting';
import { useIncidentManagementStyles } from '../Styles/IncidentManagement.styles';

const sl1 = {
    chartTitle: '10.21',
    lineChartData: [
        {
            legend: '19.64',
            color: getColorFromToken(DataVizPalette.color1),
            data: [
                {
                    x: 1,
                    y: 58.13,
                },
                {
                    x: 2,
                    y: 140.98,
                },
                {
                    x: 3,
                    y: 20,
                },
                {
                    x: 4,
                    y: 89.7,
                },
                {
                    x: 5,
                    y: 99,
                },
                {
                    x: 6,
                    y: 13.28,
                },
                {
                    x: 7,
                    y: 31.32,
                },
                {
                    x: 8,
                    y: 10.21,
                },
            ],
        },
    ],
};

const Analysis = () => {
    const styles = useIncidentManagementStyles();

    return (
        <div className={styles.navPanelWrapper}>
            <div className={styles.navPanelContent}>
                <div className={styles.navPanelPadding}>
                    <div>Analysis for stuff and things</div>

                    <Sparkline data={sl1} showLegend />
                </div>
            </div>
        </div>
    );
};

export default Analysis;
