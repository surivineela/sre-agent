import { Button } from '@fluentui/react-components';
import { ChevronDownUp20Regular, ChevronUpDown20Regular } from '@fluentui/react-icons';
import { FC } from 'react';
import { useIntl } from 'react-intl';
import { ThreadTraceResources } from '../../../../../../../../../Strings/SREAgentResources';

interface ExpandCollapseButtonProps {
    isExpanded: boolean;
    setIsExpanded: React.Dispatch<React.SetStateAction<boolean>>;
}

export const ExpandCollapseButton: FC<ExpandCollapseButtonProps> = ({ isExpanded, setIsExpanded }) => {
    const intl = useIntl();
    return (
        <Button
            style={{ marginLeft: 'auto' }}
            icon={isExpanded ? <ChevronDownUp20Regular aria-hidden="true" /> : <ChevronUpDown20Regular aria-hidden="true" />}
            onClick={() => {
                setIsExpanded(!isExpanded);
            }}
            aria-label={isExpanded ? intl.formatMessage(ThreadTraceResources.collapse) : intl.formatMessage(ThreadTraceResources.expand)}
        />
    );
};
