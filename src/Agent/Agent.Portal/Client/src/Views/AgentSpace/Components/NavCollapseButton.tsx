import { Button, Tooltip } from '@fluentui/react-components';
import { PanelLeftContract20Regular, PanelLeftExpand20Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { PortalResources } from '../../../Strings/Resources';

interface NavCollapseButtonProps {
    isNavOpen: boolean;
    onToggle: () => void;
}

export const NavCollapseButton: FC<NavCollapseButtonProps> = ({ isNavOpen, onToggle }) => {
    const intl = useIntl();

    const tooltipContent = isNavOpen
        ? intl.formatMessage(PortalResources.collapseNavigation)
        : intl.formatMessage(PortalResources.expandNavigation);

    return (
        <Tooltip content={tooltipContent} relationship="label" positioning="after">
            <Button
                appearance="transparent"
                icon={isNavOpen ? <PanelLeftContract20Regular /> : <PanelLeftExpand20Regular />}
                onClick={onToggle}
                aria-label={tooltipContent}
            />
        </Tooltip>
    );
};
