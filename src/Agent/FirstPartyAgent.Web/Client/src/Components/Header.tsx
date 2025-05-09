import { mergeStyles, Pivot, PivotItem } from '@fluentui/react';
const Header = () => {
    const headerStyles = mergeStyles({
        backgroundColor: "f8f9fa",
        boxShadow: "0 1px 3px rgba(0, 0, 0, 0.05)",
        padding: "0.75rem, 0"
    });

    return (
        <Pivot className={headerStyles}>
            <PivotItem headerText='Incident Manager' />
        </Pivot>
    );
}

export default Header;