import { SystemTool } from '../../../Contracts/ExtendedAgentGraph';
import { SystemToolConfigurationPanel, SystemToolTesterPanel } from '../SystemToolTesterPanel';

export const SystemToolPlayground = ({ tool }: { tool: SystemTool }) => {
    return (
        <div style={{ display: 'flex', gap: '20px', overflow: 'hidden', height: '100%' }}>
            <div
                style={{
                    flex: '1 1 auto',
                    width: '50%',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '16px',
                    padding: '0px 16px',
                    overflowY: 'auto',
                }}
            >
                <SystemToolConfigurationPanel tool={tool} />
            </div>
            <div
                style={{
                    flex: '1 1 auto',
                    width: '50%',
                    display: 'flex',
                    flexDirection: 'column',
                    gap: '16px',
                    padding: '16px',
                    overflowY: 'auto',
                }}
            >
                <SystemToolTesterPanel tool={tool} />
            </div>
        </div>
    );
};
