import { tokens } from '@fluentui/react-components';
import { CircleFilled } from '@fluentui/react-icons';
import { memo } from 'react';

const AgentMessageLoadingComponent = () => {
    const styles = `
        @keyframes jump {
            0%, 80%, 100% {
                transform: translateY(0);
            }
            40% {
                transform: translateY(-5px);
            }
        };`;

    const Dot = ({ delay = '0s' }) => (
        <CircleFilled
            style={{
                fontSize: '6px',
                animation: `jump 1.2s infinite ease-in-out ${delay}`,
                color: tokens.colorNeutralForeground3,
            }}
        />
    );

    return (
        <>
            <style>{styles}</style>
            <div style={{ display: 'flex', gap: tokens.spacingHorizontalXXS }}>
                <Dot />
                <Dot delay="0.2s" />
                <Dot delay="0.4s" />
            </div>
        </>
    );
};

export default memo(AgentMessageLoadingComponent);
