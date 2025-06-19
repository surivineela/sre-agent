import { memo } from 'react';
import DailyReport from '../../Common/Components/DailyReport';

const DailyReportMessage = ({ text, timeStamp }: { text: string; timeStamp: string }) => {
    try {
        const dailyReportData = JSON.parse(text);
        return <DailyReport data={dailyReportData} timestamp={timeStamp} />;
    } catch (e) {
        console.error('Failed to parse daily report:', e);
        return (
            <div>
                <div style={{ color: 'red', marginBottom: '8px' }}>Failed to parse daily report:</div>
                <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{text}</pre>
            </div>
        );
    }
};

export default memo(DailyReportMessage);
