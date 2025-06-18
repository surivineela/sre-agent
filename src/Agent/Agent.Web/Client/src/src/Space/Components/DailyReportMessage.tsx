import { memo } from 'react';
import DailyReport from '../../Common/Components/DailyReport';
import { Message } from '../../Common/Contracts/Azure/SreAgent';

const DailyReportMessage = ({ message }: { message: Message }) => {
    try {
        const dailyReportData = JSON.parse(message.text);
        return <DailyReport data={dailyReportData} timestamp={message.timeStamp} />;
    } catch (e) {
        console.error('Failed to parse daily report:', e);
        return (
            <div>
                <div style={{ color: 'red', marginBottom: '8px' }}>Failed to parse daily report:</div>
                <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{message.text}</pre>
            </div>
        );
    }
};

export default memo(DailyReportMessage);
