import { Tooltip } from '@fluentui/react-components';
import { Board20Filled, Board20Regular, bundleIcon } from '@fluentui/react-icons';
import { FC, memo } from 'react';
import { useIntl } from 'react-intl';
import { OverviewResources } from '../../../Strings/SREAgentResources';
import { PrimaryNavItemValues } from '../../Contracts/SreAgentSpace';
import { CopilotNavItem } from './CopilotNavItem';

interface OverviewNavItemProps {
    isNavOpen: boolean;
    selectOverview: () => void;
    children?: React.ReactNode;
}

const OverviewIcon = bundleIcon(Board20Filled, Board20Regular);

const OverviewNavItem: FC<OverviewNavItemProps> = ({ isNavOpen, selectOverview, children }) => {
    const intl = useIntl();

    const label = intl.formatMessage(OverviewResources.overview);

    return (
        <div style={{ display: 'flex', alignItems: 'center' }}>
            {isNavOpen ? (
                <CopilotNavItem icon={<OverviewIcon />} value={PrimaryNavItemValues.Overview} onClick={() => selectOverview()}>
                    {label}
                </CopilotNavItem>
            ) : (
                <Tooltip content={label} relationship="label">
                    <CopilotNavItem
                        icon={<OverviewIcon />}
                        value={PrimaryNavItemValues.Overview}
                        onClick={() => selectOverview()}
                        aria-label={label}
                    />
                </Tooltip>
            )}

            {children}
        </div>
    );
};

export default memo(OverviewNavItem);
