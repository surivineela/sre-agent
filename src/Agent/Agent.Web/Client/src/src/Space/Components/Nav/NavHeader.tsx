import { tokens } from '@fluentui-copilot/react-copilot';
import { CopilotNavDrawerHeader } from '@fluentui-copilot/react-copilot-nav';

const NavHeader = ({ isNavOpen, children }: { isNavOpen: boolean; children?: React.ReactNode }) => {
    return (
        !isNavOpen && (
            <CopilotNavDrawerHeader
                style={{
                    padding: `${tokens.spacingVerticalS} ${tokens.spacingVerticalM}`,
                    display: 'flex',
                    flexDirection: 'column',
                    justifyContent: 'center',
                    alignItems: 'flex-start',
                }}
            >
                {children}
            </CopilotNavDrawerHeader>
        )
    );
};

export default NavHeader;
