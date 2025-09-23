import { memo } from 'react';
import { useIntl } from 'react-intl';
import DailyReport from '../../Common/Components/DailyReport';
import { GenericErrorResources } from '../../Strings/SREAgentResources';

const DailyReportMessage = ({ text, timeStamp }: { text: string; timeStamp: string }) => {
    const intl = useIntl();
    try {
        const dailyReportData = JSON.parse(text);
        return <DailyReport data={dailyReportData} timestamp={timeStamp} />;
    } catch (e) {
        const msg = intl.formatMessage(GenericErrorResources.failedToParseDailyReport);
        console.error(msg, e);
        return (
            <div>
                <div style={{ color: 'red', marginBottom: '8px' }}>{msg}</div>
                <pre style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word' }}>{text}</pre>
            </div>
        );
    }
};

export default memo(DailyReportMessage);
