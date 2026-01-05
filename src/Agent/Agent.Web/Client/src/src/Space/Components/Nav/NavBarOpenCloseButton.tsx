import { Button } from "@fluentui/react-components";
import { useIntl } from "react-intl";
import { SreAgentResources } from "../../../Strings/SREAgentResources";
import { bundleIcon, PanelLeftContract28Filled, PanelLeftContract28Regular, PanelLeftExpand28Filled, PanelLeftExpand28Regular } from "@fluentui/react-icons";
import { memo } from "react";

interface NavOpenCloseButtonProps {
    isNavOpen: boolean;
    onExpandOrCollapseNavBar: (newState: boolean) => void;
}

const PanelLeftContractIcon = bundleIcon(PanelLeftContract28Filled, PanelLeftContract28Regular);
const PanelRightContractIcon = bundleIcon(PanelLeftExpand28Filled, PanelLeftExpand28Regular);

const NavBarOpenCloseButton: React.FC<NavOpenCloseButtonProps> = ({ isNavOpen, onExpandOrCollapseNavBar }) => {
    const intl = useIntl();

    return (
        <Button
            appearance="transparent"
            aria-label={intl.formatMessage(SreAgentResources.collapse)}
            icon={isNavOpen ? <PanelLeftContractIcon /> : <PanelRightContractIcon />}
            onClick={() => {
                const newState = !isNavOpen;
                onExpandOrCollapseNavBar(newState);
            }}
        />
    )
}

export default memo(NavBarOpenCloseButton);